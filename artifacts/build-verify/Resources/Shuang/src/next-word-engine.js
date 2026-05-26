/** last changed: 2026.5.7 */

class NextWordEngine {
  constructor(words = [], state = null) {
    this.pool = NextWordEngine.unique(words)
    const restored = this.restoreState(state)
    this.unmasteredList = restored.unmasteredList
    this.masteredList = restored.masteredList
  }

  getNextWord() {
    return this.unmasteredList.length > 0 ? this.unmasteredList[0] : null
  }

  processAnswer(word, isCorrect) {
    const current = this.getNextWord()
    if (!current || word == null || current.word !== word) return false

    return this.processKnownWord(word, isCorrect)
  }

  processKnownWord(word, isCorrect) {
    const index = this.unmasteredList.findIndex(item => item.word === word)
    if (index < 0) return false

    if (isCorrect) {
      this.handleCorrectAnswer(index)
    } else {
      this.handleWrongAnswer(index)
    }
    return true
  }

  handleCorrectAnswer(index = 0) {
    const item = this.unmasteredList.splice(index, 1)[0]
    item.correctCount++

    if (item.correctCount >= NextWordEngine.masteredCount) {
      if (!this.masteredList.includes(item.word)) {
        this.masteredList.push(item.word)
      }
      return
    }

    const position = Math.min(this.calculateOffset(item.correctCount), this.unmasteredList.length)
    this.unmasteredList.splice(position, 0, item)
  }

  handleWrongAnswer(index = 0) {
    const item = this.unmasteredList.splice(index, 1)[0]
    item.correctCount = 0

    const position = Math.min(NextWordEngine.wrongAnswerPosition, this.unmasteredList.length)
    this.unmasteredList.splice(position, 0, item)
  }

  calculateOffset(correctCount) {
    return Math.floor(NextWordEngine.offsetBase * (Math.pow(2, correctCount) - 1))
  }

  getUnmasteredList() {
    return this.unmasteredList.map(item => ({ word: item.word, correctCount: item.correctCount }))
  }

  getMasteredList() {
    return [...this.masteredList]
  }

  getProgress() {
    return {
      completed: this.masteredList.length,
      total: this.pool.length
    }
  }

  getState() {
    return {
      unmasteredList: this.getUnmasteredList(),
      masteredList: this.getMasteredList()
    }
  }

  restoreState(state) {
    if (!state || !Array.isArray(state.unmasteredList) || !Array.isArray(state.masteredList)) {
      return {
        unmasteredList: this.pool.map(word => ({ word, correctCount: 0 })),
        masteredList: []
      }
    }

    const validWords = new Set(this.pool)
    const seen = new Set()
    const unmasteredList = []
    const masteredList = []

    for (const item of state.unmasteredList) {
      if (!item || !validWords.has(item.word) || seen.has(item.word)) continue
      seen.add(item.word)
      unmasteredList.push({
        word: item.word,
        correctCount: Math.max(0, Number(item.correctCount) || 0)
      })
    }

    for (const word of state.masteredList) {
      if (!validWords.has(word) || seen.has(word)) continue
      seen.add(word)
      masteredList.push(word)
    }

    for (const word of this.pool) {
      if (!seen.has(word)) {
        unmasteredList.push({ word, correctCount: 0 })
      }
    }

    return { unmasteredList, masteredList }
  }

  static unique(words) {
    const result = []
    const seen = new Set()
    for (const word of words) {
      if (!word || seen.has(word)) continue
      seen.add(word)
      result.push(word)
    }
    return result
  }
}

NextWordEngine.masteredCount = 2
NextWordEngine.offsetBase = 3
NextWordEngine.wrongAnswerPosition = 2

if (typeof Shuang !== 'undefined') {
  Shuang.core.nextWordEngine = NextWordEngine
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { NextWordEngine }
}
