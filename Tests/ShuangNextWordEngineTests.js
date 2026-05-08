const assert = require('assert')
const { NextWordEngine } = require('../Resources/Shuang/src/next-word-engine.js')
const { ShuangPracticeQueue } = require('../Resources/Shuang/src/practice-queue.js')

function words(engine) {
  return engine.getUnmasteredList().map(item => item.word)
}

function wrongAnswerKeepsSmartReviewOrder() {
  const engine = new NextWordEngine(['a', 'b', 'c', 'd'])

  assert.strictEqual(engine.getNextWord().word, 'a')
  assert.strictEqual(engine.processAnswer('a', false), true)

  assert.deepStrictEqual(words(engine), ['b', 'c', 'a', 'd'])
  assert.strictEqual(engine.getNextWord().word, 'b')
}

function correctAnswerDelaysWordByOffset() {
  const engine = new NextWordEngine(['a', 'b', 'c', 'd', 'e'])

  assert.strictEqual(engine.processAnswer('a', true), true)

  assert.deepStrictEqual(words(engine), ['b', 'c', 'd', 'a', 'e'])
}

function secondCorrectAnswerMarksWordMastered() {
  const engine = new NextWordEngine(['a'])

  assert.strictEqual(engine.processAnswer('a', true), true)
  assert.deepStrictEqual(words(engine), ['a'])
  assert.deepStrictEqual(engine.getMasteredList(), [])

  assert.strictEqual(engine.processAnswer('a', true), true)
  assert.deepStrictEqual(words(engine), [])
  assert.deepStrictEqual(engine.getMasteredList(), ['a'])
  assert.strictEqual(engine.getNextWord(), null)
}

function progressCountsMasteredWords() {
  const engine = new NextWordEngine(['a', 'b'])

  assert.deepStrictEqual(engine.getProgress(), { completed: 0, total: 2 })

  engine.processAnswer('a', true)
  assert.deepStrictEqual(engine.getProgress(), { completed: 0, total: 2 })

  engine.processAnswer('b', true)
  engine.processAnswer('a', true)
  assert.deepStrictEqual(engine.getProgress(), { completed: 1, total: 2 })
}

function queueAutoContinuesAfterCompletionAndKeepsProgressComplete() {
  const storage = {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => { storage[key] = value }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'all', scheme: 'test', withoutPinyin: 'false' }
      }
    },
    resource: {
      dict: {
        '': { a: '啊', list: ['a'] },
        list: ['']
      }
    },
    core: {
      nextWordEngine: NextWordEngine,
      model: function Model(sheng, yun) {
        this.sheng = sheng
        this.yun = yun
      }
    }
  }

  const queue = new ShuangPracticeQueue()

  assert.notStrictEqual(queue.next(), null)
  assert.deepStrictEqual(queue.getProgress(), { completed: 0, total: 1 })
  assert.notStrictEqual(queue.next(true), null)
  assert.deepStrictEqual(queue.getProgress(), { completed: 0, total: 1 })
  assert.notStrictEqual(queue.next(true), null)
  assert.deepStrictEqual(queue.getProgress(), { completed: 1, total: 1 })
  assert.strictEqual(queue.completedRound, false)
  assert.strictEqual(queue.progressCompleted, true)

  assert.notStrictEqual(queue.next(true), null)
  assert.deepStrictEqual(queue.getProgress(), { completed: 1, total: 1 })
  assert.strictEqual(queue.completedRound, false)

  queue.resetProgress()
  assert.deepStrictEqual(queue.getProgress(), { completed: 0, total: 1 })
  assert.strictEqual(queue.progressCompleted, false)
}

function queueShareRangePoolAcrossPinyinModes() {
  global.localStorage = {
    getItem: () => null,
    setItem: () => {}
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'hard', scheme: 'test', withoutPinyin: 'false' }
      }
    },
    resource: {
      dict: {
        '': { a: '啊', list: ['a'] },
        b: { a: '爸', ai: '白', ia: ['俩'], list: ['a', 'ai', 'ia'] },
        list: ['', 'b']
      }
    },
    core: {
      nextWordEngine: NextWordEngine,
      model: function Model(sheng, yun) {
        this.sheng = sheng
        this.yun = yun
      }
    }
  }

  const queue = new ShuangPracticeQueue()

  assert.deepStrictEqual(queue.buildPool('hard'), ['b|ai'])
  assert.deepStrictEqual(queue.buildPool('all'), ['|a', 'b|a', 'b|ai', 'b|ia'])

  const hardKey = queue.getStateKey('hard')
  const allKey = queue.getStateKey('all')
  assert.notStrictEqual(hardKey, allKey)

  Shuang.app.setting.config.withoutPinyin = 'true'
  assert.strictEqual(queue.getStateKey('hard'), hardKey)
  assert.deepStrictEqual(queue.buildPool('hard'), ['b|ai'])
}

function queueRetriedWrongWordDoesNotCreditNextQueueHead() {
  const storage = {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => { storage[key] = value }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'all', scheme: 'test', withoutPinyin: 'false' }
      }
    },
    resource: {
      dict: {
        s: { a: '萨', b: '色', c: '丝', d: '苏', list: ['a', 'b', 'c', 'd'] },
        list: ['s']
      }
    },
    core: {
      nextWordEngine: NextWordEngine,
      model: function Model(sheng, yun) {
        this.sheng = sheng
        this.yun = yun
      }
    }
  }

  const queue = new ShuangPracticeQueue()
  queue.shuffle = words => words

  let current = queue.next()
  assert.deepStrictEqual({ sheng: current.sheng, yun: current.yun }, { sheng: 's', yun: 'a' })

  current = queue.next(false)
  assert.deepStrictEqual({ sheng: current.sheng, yun: current.yun }, { sheng: 's', yun: 'a' })
  assert.deepStrictEqual(words(queue.engine), ['s|b', 's|c', 's|a', 's|d'])

  current = queue.next(true)
  assert.deepStrictEqual({ sheng: current.sheng, yun: current.yun }, { sheng: 's', yun: 'b' })
  assert.deepStrictEqual(words(queue.engine), ['s|b', 's|c', 's|d', 's|a'])
}

function queueDefersLargeStateWritesDuringAnswerFlow() {
  const storage = {}
  const stateWrites = []
  const pendingTimers = []
  const originalSetTimeout = global.setTimeout
  const originalClearTimeout = global.clearTimeout
  global.setTimeout = (callback) => {
    pendingTimers.push(callback)
    return pendingTimers.length
  }
  global.clearTimeout = () => {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => {
      storage[key] = value
      if (key.startsWith('shuang-next-word:')) {
        stateWrites.push(value)
      }
    }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'all', scheme: 'test', withoutPinyin: 'false' }
      }
    },
    resource: {
      dict: {
        s: { a: '萨', b: '色', list: ['a', 'b'] },
        list: ['s']
      }
    },
    core: {
      nextWordEngine: NextWordEngine,
      model: function Model(sheng, yun) {
        this.sheng = sheng
        this.yun = yun
      }
    }
  }

  try {
    const queue = new ShuangPracticeQueue()
    queue.shuffle = words => words

    queue.next()
    queue.next(true)

    assert.strictEqual(stateWrites.length, 0)
    assert.strictEqual(pendingTimers.length, 1)

    pendingTimers.shift()()

    assert.strictEqual(stateWrites.length, 1)
  } finally {
    global.setTimeout = originalSetTimeout
    global.clearTimeout = originalClearTimeout
  }
}

function queueFlushesDeferredStateBeforeReplacingEngine() {
  const storage = {}
  const stateWrites = []
  const pendingTimers = []
  const originalSetTimeout = global.setTimeout
  const originalClearTimeout = global.clearTimeout
  global.setTimeout = (callback) => {
    pendingTimers.push(callback)
    return pendingTimers.length
  }
  global.clearTimeout = () => {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => {
      storage[key] = value
      if (key.startsWith('shuang-next-word:')) {
        stateWrites.push({ key, value })
      }
    }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'all', scheme: 'one', withoutPinyin: 'false' }
      }
    },
    resource: {
      dict: {
        s: { a: '萨', b: '色', list: ['a', 'b'] },
        list: ['s']
      }
    },
    core: {
      nextWordEngine: NextWordEngine,
      model: function Model(sheng, yun) {
        this.sheng = sheng
        this.yun = yun
      }
    }
  }

  try {
    const queue = new ShuangPracticeQueue()
    queue.shuffle = words => words

    queue.next()
    queue.next(true)
    assert.strictEqual(stateWrites.length, 0)

    Shuang.app.setting.config.scheme = 'two'
    queue.sync()

    assert.strictEqual(stateWrites.length, 1)
    assert.strictEqual(stateWrites[0].key, 'shuang-next-word:1:one:all')
  } finally {
    global.setTimeout = originalSetTimeout
    global.clearTimeout = originalClearTimeout
  }
}

function resetProgressStartsCurrentRangeAgain() {
  const storage = {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => { storage[key] = value }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'all', scheme: 'test', withoutPinyin: 'false' }
      }
    },
    resource: {
      dict: {
        '': { a: '啊', list: ['a'] },
        list: ['']
      }
    },
    core: {
      nextWordEngine: NextWordEngine,
      model: function Model(sheng, yun) {
        this.sheng = sheng
        this.yun = yun
      }
    }
  }

  const queue = new ShuangPracticeQueue()

  queue.next()
  queue.next(true)
  queue.next(true)
  assert.deepStrictEqual(queue.getProgress(), { completed: 1, total: 1 })

  queue.resetProgress()

  assert.deepStrictEqual(queue.getProgress(), { completed: 0, total: 1 })
  assert.strictEqual(queue.completedRound, false)
  assert.notStrictEqual(queue.next(), null)
}

function scoreIsStoredPerSchemeWithComboRules() {
  const storage = {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => { storage[key] = value }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'hard', scheme: 'one', withoutPinyin: 'false' }
      }
    },
    resource: { dict: { list: [] } },
    core: { nextWordEngine: NextWordEngine }
  }
  const queue = new ShuangPracticeQueue()

  for (let i = 1; i <= 9; i++) {
    assert.deepStrictEqual(queue.recordScore(true), { delta: 2, score: i * 2, combo: i })
  }
  assert.deepStrictEqual(queue.recordScore(true), { delta: 4, score: 22, combo: 10 })
  assert.deepStrictEqual(queue.getScore(), { score: 22, combo: 10, maxCombo: 10 })

  assert.deepStrictEqual(queue.recordScore(false), { delta: -2, score: 20, combo: 0 })
  assert.deepStrictEqual(queue.getScore(), { score: 20, combo: 0, maxCombo: 10 })

  Shuang.app.setting.config.scheme = 'two'
  assert.deepStrictEqual(queue.getScore(), { score: 0, combo: 0, maxCombo: 0 })
  assert.deepStrictEqual(queue.recordScore(true), { delta: 2, score: 2, combo: 1 })

  Shuang.app.setting.config.scheme = 'one'
  assert.deepStrictEqual(queue.getScore(), { score: 20, combo: 0, maxCombo: 10 })
}

function scoreBonusCapsAtTwentyPoints() {
  const storage = {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => { storage[key] = value }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'hard', scheme: 'test', withoutPinyin: 'false' }
      }
    },
    resource: { dict: { list: [] } },
    core: { nextWordEngine: NextWordEngine }
  }
  const queue = new ShuangPracticeQueue()
  let result = null

  for (let i = 0; i < 50; i++) {
    result = queue.recordScore(true)
  }

  assert.strictEqual(result.delta, 20)
  assert.strictEqual(result.combo, 50)
  assert.strictEqual(queue.getScore().maxCombo, 50)
}

function maxComboSurvivesMistakesAndMigratesStoredCombo() {
  const storage = {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => { storage[key] = value }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'hard', scheme: 'test', withoutPinyin: 'false' }
      }
    },
    resource: { dict: { list: [] } },
    core: { nextWordEngine: NextWordEngine }
  }
  const queue = new ShuangPracticeQueue()

  queue.recordScore(true)
  queue.recordScore(true)
  queue.recordScore(false)
  queue.recordScore(true)

  assert.deepStrictEqual(queue.getScore(), { score: 4, combo: 1, maxCombo: 2 })

  storage[queue.getScoreKey()] = JSON.stringify({ score: 100, combo: 7 })
  assert.deepStrictEqual(queue.getScore(), { score: 100, combo: 7, maxCombo: 7 })
}

function practiceProgressDisplaysMaxCombo() {
  const elements = {
    '#practice-progress': { innerText: '' },
    '#practice-progress-percent': { innerText: '' },
    '#practice-score': { innerText: '' },
    '#practice-combo': { innerText: '' },
    '#practice-max-combo': { innerText: '' }
  }
  global.document = {
    querySelector: selector => elements[selector] || null,
    querySelectorAll: () => []
  }
  global.$ = selector => global.document.querySelector(selector)
  global.$$ = selector => global.document.querySelectorAll(selector)
  global.localStorage = {
    getItem: () => null,
    setItem: () => {}
  }
  global.window = {}
  global.Shuang = {
    app: { setting: {}, modeList: {} },
    core: {
      practiceQueue: {
        isQueueMode: () => true,
        getProgress: () => ({ completed: 3, total: 3 }),
        getScore: () => ({ score: 28, combo: 2, maxCombo: 8 })
      }
    },
    resource: { keyboardLayout: {} }
  }

  delete require.cache[require.resolve('../Resources/Shuang/src/setting.js')]
  require('../Resources/Shuang/src/setting.js')

  Shuang.app.setting.updatePracticeProgress()

  assert.strictEqual(elements['#practice-progress'].innerText, '3/3 组')
  assert.strictEqual(elements['#practice-progress-percent'].innerText, '100%')
  assert.strictEqual(elements['#practice-score'].innerText, '28 分')
  assert.strictEqual(elements['#practice-combo'].innerText, '2 连击')
  assert.strictEqual(elements['#practice-max-combo'].innerText, '8 连击')
}

function wrongAnswerRevealRespectsUserVisibilitySettings() {
  const storage = {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => { storage[key] = value }
  }

  const keyboard = { style: { display: 'none' } }
  const input = { value: '' }
  const button = { onclick: null, innerText: '' }
  const body = { scrollWidth: 700 }
  const keys = [{
    attrs: { key: 'S' },
    classList: createClassList(),
    getAttribute(name) {
      return this.attrs[name] || ''
    },
    setAttribute(name, value) {
      this.attrs[name] = value
    }
  }]
  const elements = {
    '#keyboard': keyboard,
    '#a': input,
    '#btn': button,
    '#pic': { scrollWidth: 700 },
    '#keys': {},
    'body': body
  }

  global.document = {
    querySelector: selector => elements[selector] || null,
    querySelectorAll: selector => selector === '.key' ? keys : []
  }
  global.$ = selector => global.document.querySelector(selector)
  global.$$ = selector => global.document.querySelectorAll(selector)
  global.window = {}
  global.navigator = { userAgent: 'test' }

  const currentModel = {
    scheme: new Set(['sh']),
    beforeJudge: () => {},
    judge: () => false
  }

  global.Shuang = {
    app: {
      setting: {},
      action: {},
      importedJS: [],
      modeList: {
        hard: { name: '基础', desc: '练习范围：基础' },
        all: { name: '全部', desc: '练习范围：全部' }
      }
    },
    core: {
      current: currentModel,
      practiceQueue: {
        completedRound: false,
        isQueueMode: () => false,
        next: () => currentModel
      }
    },
    resource: {
      keyboardLayout: {
        qwerty: { row1: ['s'], row2: ['h'], row3: [] }
      },
      emoji: { right: '✔️', wrong: '❌' }
    }
  }

  delete require.cache[require.resolve('../Resources/Shuang/src/setting.js')]
  delete require.cache[require.resolve('../Resources/Shuang/src/action.js')]
  require('../Resources/Shuang/src/setting.js')
  require('../Resources/Shuang/src/action.js')

  const setting = global.Shuang.app.setting
  const action = global.Shuang.app.action

  setting.config.keyboardLayout = 'qwerty'
  setting.updateQAndDict = () => {}
  setting.updatePracticeProgress = () => {}
  setting.updateKeysHintLayoutRatio = () => {}
  setting.updateSimulateKeyboard = () => {}

  setting.setPicVisible(false)
  setting.setShowKeys(false)
  input.value = 'zz'

  assert.strictEqual(typeof action.judge, 'function')
  action.judge()
  assert.strictEqual(keyboard.style.display, 'block')
  assert.strictEqual(keys[0].classList.contains('answer'), true)

  action.submitAnswer(false, true)
  assert.strictEqual(keyboard.style.display, 'block')
  assert.strictEqual(keys[0].classList.contains('answer'), true)

  action.submitAnswer(true, true)
  assert.strictEqual(keyboard.style.display, 'none')
  assert.strictEqual(keys[0].classList.contains('answer'), false)

  setting.setPicVisible(true)
  setting.setShowKeys(true)
  input.value = 'zz'
  action.judge()
  action.submitAnswer(false, true)
  action.submitAnswer(true, true)

  assert.strictEqual(keyboard.style.display, 'block')
  assert.strictEqual(keys[0].classList.contains('answer'), true)
}

function createClassList() {
  const classes = new Set()
  return {
    add(...names) {
      for (const name of names) {
        classes.add(name)
      }
    },
    remove(...names) {
      for (const name of names) {
        classes.delete(name)
      }
    },
    contains(name) {
      return classes.has(name)
    }
  }
}

function wrongPenaltyUsesTwoPercent() {
  const storage = {}
  global.localStorage = {
    getItem: key => Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null,
    setItem: (key, value) => { storage[key] = value }
  }
  global.Shuang = {
    app: {
      setting: {
        config: { mode: 'hard', scheme: 'one', withoutPinyin: 'false' }
      }
    },
    resource: { dict: { list: [] } },
    core: { nextWordEngine: NextWordEngine }
  }
  const queue = new ShuangPracticeQueue()
  storage[queue.getScoreKey()] = JSON.stringify({ score: 100, combo: 3 })

  assert.deepStrictEqual(queue.recordScore(false), { delta: -2, score: 98, combo: 0 })
  assert.deepStrictEqual(queue.getScore(), { score: 98, combo: 0, maxCombo: 3 })
}

function run(name, test) {
  try {
    test()
    console.log('PASS: ' + name)
  } catch (error) {
    console.error('FAIL: ' + name)
    console.error(error.stack || error.message)
    process.exitCode = 1
  }
}

run('wrong answer keeps smart review order', wrongAnswerKeepsSmartReviewOrder)
run('correct answer delays word by offset', correctAnswerDelaysWordByOffset)
run('second correct answer marks word mastered', secondCorrectAnswerMarksWordMastered)
run('progress counts mastered words', progressCountsMasteredWords)
run('queue auto-continues after completion and keeps progress complete', queueAutoContinuesAfterCompletionAndKeepsProgressComplete)
run('queue shares range pool across pinyin modes', queueShareRangePoolAcrossPinyinModes)
run('queue retried wrong word does not credit next queue head', queueRetriedWrongWordDoesNotCreditNextQueueHead)
run('queue defers large state writes during answer flow', queueDefersLargeStateWritesDuringAnswerFlow)
run('queue flushes deferred state before replacing engine', queueFlushesDeferredStateBeforeReplacingEngine)
run('reset progress starts current range again', resetProgressStartsCurrentRangeAgain)
run('score is stored per scheme with combo rules', scoreIsStoredPerSchemeWithComboRules)
run('score bonus caps at twenty points', scoreBonusCapsAtTwentyPoints)
run('max combo survives mistakes and migrates stored combo', maxComboSurvivesMistakesAndMigratesStoredCombo)
run('practice progress displays max combo', practiceProgressDisplaysMaxCombo)
run('wrong answer reveal respects user visibility settings', wrongAnswerRevealRespectsUserVisibilitySettings)
run('wrong penalty uses two percent', wrongPenaltyUsesTwoPercent)
