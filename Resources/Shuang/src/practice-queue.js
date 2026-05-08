/** last changed: 2026.5.7 */

class ShuangPracticeQueue {
  constructor() {
    this.storageVersion = '1'
    this.storagePrefix = 'shuang-next-word'
    this.scoreStoragePrefix = 'shuang-score'
    this.stateKey = ''
    this.engine = null
    this.completedRound = false
    this.retryWord = null
  }

  isQueueMode(mode = Shuang.app.setting.config.mode) {
    return ['all', 'hard', 'all-random', 'all-order', 'hard-random', 'hard-random-without-pinyin'].includes(mode)
  }

  next(answerResult = null) {
    if (this.completedRound && answerResult == null) {
      this.resetProgress()
    }

    const engine = this.sync()
    if (!engine) return null

    const current = engine.getNextWord()
    if (!current) {
      this.resetProgress()
      return this.currentModel()
    }

    if (typeof answerResult === 'boolean') {
      const answeredWord = this.retryWord || current.word
      if (typeof engine.processKnownWord === 'function') {
        engine.processKnownWord(answeredWord, answerResult)
      } else {
        engine.processAnswer(answeredWord, answerResult)
      }
      this.retryWord = answerResult ? null : answeredWord
      this.save()
      if (!engine.getNextWord()) {
        this.completedRound = true
        return null
      }
    }

    return this.currentModel()
  }

  sync() {
    const range = this.normalizeRange(Shuang.app.setting.config.mode)
    if (!this.isQueueMode(range)) return null

    const stateKey = this.getStateKey(range)
    if (this.engine && this.stateKey === stateKey) {
      return this.engine
    }

    this.stateKey = stateKey
    const pool = this.buildPool(range)
    const state = this.readState(stateKey)
    const words = state ? pool : this.shuffle([...pool])
    this.engine = new Shuang.core.nextWordEngine(words, state)
    this.completedRound = this.engine.getNextWord() == null
    this.retryWord = null
    this.save()
    return this.engine
  }

  reset() {
    return this.resetProgress()
  }

  resetProgress() {
    const range = this.normalizeRange(Shuang.app.setting.config.mode)
    const pool = this.shuffle(this.buildPool(range))
    this.stateKey = this.getStateKey(range)
    this.engine = new Shuang.core.nextWordEngine(pool)
    this.completedRound = false
    this.retryWord = null
    this.save()
    return this.engine
  }

  getProgress() {
    const engine = this.engine || this.sync()
    if (!engine) {
      return { completed: 0, total: 0 }
    }
    return engine.getProgress()
  }

  currentModel() {
    if (!this.engine) return null

    const current = this.retryWord
      ? this.engine.getUnmasteredList().find(item => item.word === this.retryWord)
      : this.engine.getNextWord()
    if (!current) return null

    const [sheng, yun] = this.decode(current.word)
    return new Shuang.core.model(sheng, yun)
  }

  buildPool(range) {
    const normalizedRange = this.normalizeRange(range)
    const words = []
    for (const sheng of Shuang.resource.dict.list) {
      for (const yun of Shuang.resource.dict[sheng].list) {
        const dict = Shuang.resource.dict[sheng][yun]
        if (normalizedRange === 'hard') {
          if (sheng === '' || yun.length === 1) continue
          if (Array.isArray(dict)) continue
        }
        words.push(this.encode(sheng, yun))
      }
    }
    return words
  }

  encode(sheng, yun) {
    return `${sheng}|${yun}`
  }

  decode(word) {
    return word.split('|')
  }

  normalizeRange(range) {
    if (range === 'all-random' || range === 'all-order') return 'all'
    if (range === 'hard-random' || range === 'hard-random-without-pinyin') return 'hard'
    return range === 'all' ? 'all' : 'hard'
  }

  isWithoutPinyin() {
    const config = Shuang.app.setting.config || {}
    return config.withoutPinyin === 'true' || config.mode === 'hard-random-without-pinyin'
  }

  getStateKey(range) {
    const scheme = Shuang.app.setting.config.scheme || 'ziranma'
    return `${this.storagePrefix}:${this.storageVersion}:${scheme}:${this.normalizeRange(range)}`
  }

  readState(key) {
    try {
      const raw = localStorage.getItem(key)
      if (!raw) return null

      const state = JSON.parse(raw)
      if (!state || state.version !== this.storageVersion) return null
      return state
    } catch (_) {
      return null
    }
  }

  save() {
    if (!this.engine || !this.stateKey) return

    try {
      localStorage.setItem(this.stateKey, JSON.stringify(Object.assign({
        version: this.storageVersion,
      }, this.engine.getState())))
    } catch (_) {
      // Browser storage can be disabled; the queue still works for the current page.
    }
  }

  getScore() {
    const state = this.readScoreState()
    return {
      score: state.score,
      combo: state.combo
    }
  }

  recordScore(isCorrect) {
    const state = this.readScoreState()
    let delta

    if (isCorrect) {
      state.combo++
      delta = this.calculateCorrectScore(state.combo)
      state.score += delta
    } else {
      delta = -this.calculateWrongPenalty(state.score)
      state.score = Math.max(0, state.score + delta)
      state.combo = 0
    }

    this.saveScoreState(state)
    return {
      delta,
      score: state.score,
      combo: state.combo
    }
  }

  resetScore() {
    const state = { score: 0, combo: 0 }
    this.saveScoreState(state)
    return state
  }

  calculateCorrectScore(combo) {
    const multiplier = Math.pow(2, Math.floor(combo / 10))
    return Math.min(20, 2 * multiplier)
  }

  calculateWrongPenalty(score) {
    return Math.min(40, Math.max(2, Math.ceil(score * 0.02)))
  }

  getScoreKey() {
    const scheme = Shuang.app.setting.config.scheme || 'ziranma'
    return `${this.scoreStoragePrefix}:${this.storageVersion}:${scheme}`
  }

  readScoreState() {
    try {
      const raw = localStorage.getItem(this.getScoreKey())
      if (!raw) return { score: 0, combo: 0 }

      const state = JSON.parse(raw)
      return {
        score: Math.max(0, Math.floor(Number(state.score) || 0)),
        combo: Math.max(0, Math.floor(Number(state.combo) || 0))
      }
    } catch (_) {
      return { score: 0, combo: 0 }
    }
  }

  saveScoreState(state) {
    try {
      localStorage.setItem(this.getScoreKey(), JSON.stringify({
        score: Math.max(0, Math.floor(Number(state.score) || 0)),
        combo: Math.max(0, Math.floor(Number(state.combo) || 0))
      }))
    } catch (_) {
      // Score display still works for the current page when storage is unavailable.
    }
  }

  shuffle(words) {
    for (let i = words.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1))
      const temp = words[i]
      words[i] = words[j]
      words[j] = temp
    }
    return words
  }
}

if (typeof Shuang !== 'undefined') {
  Shuang.core.practiceQueue = new ShuangPracticeQueue()
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { ShuangPracticeQueue }
}
