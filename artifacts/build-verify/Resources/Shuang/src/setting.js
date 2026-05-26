/** last changed: 2024.1.9 */

Shuang.app.setting = {
  config: {},
  modeNoticeTimer: null,
  practiceErrorHintVisible: false,
  reload() {
    /** Reading Storage or Using Default **/
    const legacyMode = readStorage('mode')
    this.config = {
      scheme: readStorage('scheme') || 'ziranma',
      mode: normalizePracticeRange(readStorage('practiceRange') || legacyMode || 'hard'),
      keyboardLayout: readStorage('keyboardLayout') || 'qwerty',
      showPic: readStorage('showPic') || 'true',
      darkMode: readStorage('darkMode') || detectDarkMode().toString(),
      showKeys: readStorage("showKeys") || "true",
      disableMobileKeyboard: readStorage("disableMobileKeyboard") || "false",
      bopomofo: readStorage("bopomofo") || "false",
      withoutPinyin: readStorage("withoutPinyin") || normalizeWithoutPinyin(legacyMode),
    }
    /** Applying Settings :: Changing UI **/
    const { scheme, mode, keyboardLayout, showPic, darkMode, showKeys, disableMobileKeyboard, bopomofo, withoutPinyin } = this.config
    Array.prototype.find.call($('#scheme-select').children,
      schemeOption => Shuang.resource.schemeList[scheme].startsWith(schemeOption.innerText)
    ).selected = true
    $('#mode-select')[Object.keys(Shuang.app.modeList).indexOf(mode)].selected = true
    $('#keyboard-layout-select')[Object.keys(Shuang.resource.keyboardLayoutList).indexOf(keyboardLayout)].selected = true
    $('#pic-switcher').checked = showPic === 'true'
    $('#dark-mode-switcher').checked = darkMode === 'true'
    $('#show-keys').checked = showKeys === 'true'
    $('#disable-mobile-keyboard').checked = disableMobileKeyboard === 'true'
    $('#bopomofo-switcher').checked = bopomofo === 'true'
    $('#without-pinyin-switcher').checked = withoutPinyin === 'true'
    /** Applying Settings :: Invoking Actions  **/
    this.setKeyboardLayout(Shuang.resource.keyboardLayoutList[keyboardLayout])
    this.setScheme(Shuang.resource.schemeList[scheme], false)
    this.setMode(Shuang.app.modeList[mode].name, false)
    this.setPicVisible(showPic)
    this.setDarkMode(darkMode)
    this.setShowKeys(showKeys)
    this.setDisableMobileKeyboard(disableMobileKeyboard)
    this.setBopomofo(bopomofo)
    this.setWithoutPinyin(withoutPinyin, false)
  },
  setScheme(schemeName, next = true) {
    this.config.scheme = Object.keys(Shuang.resource.schemeList)[
      Object.values(Shuang.resource.schemeList)
        .findIndex(scheme => scheme.startsWith(schemeName))
    ]
    importJS('scheme/' + this.config.scheme, () => {
      if (next) Shuang.app.action.next()
      Shuang.core.current.beforeJudge()
      this.updateKeyboardLayout()
      this.updateKeysHint()
      this.updateTips()
    })
    writeStorage('scheme', this.config.scheme)
  },
  setMode(modeName, notify = true) {
    Shuang.core.history = []
    for (const [mode, { name }] of Object.entries(Shuang.app.modeList)) {
      if (name === modeName) {
        this.config.mode = mode
        writeStorage('practiceRange', this.config.mode)
        writeStorage('mode', this.config.mode)
        if (notify) {
          this.showModeNotice(Shuang.app.modeList[mode].desc)
        }
        this.updateQVisibility()
        this.updatePracticeProgress()
        break
      }
    }
  },
  setPicVisible(bool) {
    this.config.showPic = bool.toString()
    writeStorage('showPic', this.config.showPic)
    this.updateKeyboardVisibility()
  },
  setDarkMode(bool) {
    this.config.darkMode = bool.toString()
    if (this.config.darkMode === 'true') {
      $('body').setAttribute('class', 'dark-mode')
    } else if (this.config.darkMode === 'false') {
      $('body').setAttribute('class', '')
    }
    writeStorage('darkMode', this.config.darkMode)
  },
  setShowKeys(bool) {
    this.config.showKeys = bool.toString()
    writeStorage('showKeys', this.config.showKeys)
    this.updateKeysHint()
  },
  setPracticeErrorHintVisible(bool) {
    this.practiceErrorHintVisible = bool === true || bool === 'true'
    this.updateKeyboardVisibility()
    this.updateKeysHint()
  },
  setDisableMobileKeyboard(bool) {
    this.config.disableMobileKeyboard = bool.toString()
    if (this.config.disableMobileKeyboard === 'true') {
      $('#a').setAttribute('inputmode', 'none')
    } else if (this.config.disableMobileKeyboard === 'false') {
      $('#a').setAttribute('inputmode', 'text')
    }
    writeStorage('disableMobileKeyboard', this.config.disableMobileKeyboard)
  },
  setBopomofo(bool) {
    this.config.bopomofo = bool.toString()
    this.updateQAndDict()
    this.updateKeyboardLayout()
    this.updateKeysHint()
    writeStorage('bopomofo', this.config.bopomofo)
  },
  setWithoutPinyin(bool, notify = true) {
    this.config.withoutPinyin = bool.toString()
    writeStorage('withoutPinyin', this.config.withoutPinyin)
    this.updateQVisibility()
    this.updateQAndDict()
    this.updatePracticeProgress()
    if (notify) {
      this.showModeNotice(this.config.withoutPinyin === 'true' ? '已隐藏拼音提示' : '已显示拼音提示')
    }
  },
  updateQVisibility() {
    $('#q').style.display = this.config.withoutPinyin === 'true' ? 'none' : 'block'
  },
  updateKeyboardVisibility() {
    const keyboard = $('#keyboard')
    if (!keyboard) return
    keyboard.style.display = this.config.showPic === 'true' || this.practiceErrorHintVisible ? 'block' : 'none'
    this.updateKeysHintLayoutRatio()
  },
  updateQAndDict() {
    this.updateQVisibility()
    if (!Shuang.core.current || !Shuang.core.current.view) {
      this.updatePracticeProgress()
      return
    }
    $('#q').innerText = this.config.bopomofo === 'false'
      ? Shuang.core.current.view.sheng + Shuang.core.current.view.yun
      : Shuang.resource.bopomofo[Shuang.core.current.view.sheng.toLowerCase()][Shuang.core.current.view.yun]
    $('#dict').innerText = this.config.bopomofo === 'false'
      ? Shuang.core.current.dict
      : Shuang.resource.dictHant[Shuang.core.current.view.sheng.toLowerCase()][Shuang.core.current.view.yun]
    this.updatePracticeProgress()
  },
  updatePracticeProgress(scoreChange = null) {
    const progress = $('#practice-progress')
    const percent = $('#practice-progress-percent')
    const score = $('#practice-score')
    const combo = $('#practice-combo')
    const maxCombo = $('#practice-max-combo')
    if (!progress || !percent || !score || !combo) return

    const queue = Shuang.core.practiceQueue
    const isQueueMode = queue && queue.isQueueMode()
    const { completed, total } = isQueueMode ? queue.getProgress() : { completed: 0, total: 0 }
    const { score: totalScore, combo: currentCombo, maxCombo: bestCombo } = isQueueMode
      ? queue.getScore()
      : { score: 0, combo: 0, maxCombo: 0 }
    const ratio = total > 0 ? Math.floor((completed / total) * 100) : 0

    progress.innerText = `${completed}/${total} 组`
    percent.innerText = `${ratio}%`
    score.innerText = `${totalScore} 分`
    combo.innerText = `${currentCombo} 连击`
    if (maxCombo) {
      maxCombo.innerText = `${bestCombo || 0} 连击`
    }

    if (scoreChange && scoreChange.delta) {
      this.flashPracticeScore(scoreChange.delta)
    }
  },
  flashPracticeScore(delta) {
    const badge = $('#practice-score-delta')
    if (!badge) return
    badge.classList.remove('show', 'positive', 'negative')
    badge.textContent = `${delta > 0 ? '+' : ''}${delta}`
    badge.classList.add(delta > 0 ? 'positive' : 'negative')
    void badge.offsetWidth
    badge.classList.add('show')
    clearTimeout(this.scoreDeltaTimer)
    this.scoreDeltaTimer = setTimeout(() => {
      badge.classList.remove('show')
    }, 800)
  },
  showModeNotice(text) {
    const toast = $('#mode-toast')
    if (!toast) return
    clearTimeout(this.modeNoticeTimer)
    toast.textContent = text || ''
    toast.classList.add('show')
    this.modeNoticeTimer = setTimeout(() => {
      toast.classList.remove('show')
    }, 1100)
  },
  updateKeysHint() {
    if (!Shuang.resource.keyboardLayout[this.config.keyboardLayout]) return
    this.updateSimulateKeyboard()
    this.updateKeysHintLayoutRatio()
    const keys = $$('.key')
    for (const key of keys) {
      key.classList.remove('answer')
    }
    const shouldShowKeys = this.config.showKeys === 'true' || this.practiceErrorHintVisible
    if (!shouldShowKeys) return
    if (!Shuang.core.current || !Shuang.core.current.scheme) return
    const answerKeys = new Set()
    for (const [sheng, yun] of Shuang.core.current.scheme) {
      answerKeys.add(sheng)
      answerKeys.add(yun)
    }
    for (const key of keys) {
      if (answerKeys.has(key.getAttribute('key').toLowerCase())) {
        key.classList.add('answer')
      }
    }
  },
  updateKeysHintLayoutRatio() {
    if ($('body').scrollWidth < 700) {
      const width = $('body').scrollWidth === 310 ? 310 : $('#pic').scrollWidth
      const ratio = 1874 / 1928 * width / 680
      if (ratio < 1) {
        if (navigator && navigator.userAgent && /firefox/i.test(navigator.userAgent)) {
          // Firefox 不支持 zoom
          $('#keys').style.transform = `scale(${ratio})`
          $('#keys').style.transformOrigin = `left top`
        } else {
          $('#keys').style.zoom = ratio
        }
        return
      }
    }
    if (navigator && navigator.userAgent && /firefox/i.test(navigator.userAgent)) {
      $('#keys').style.transform = 'unset'
      $('#keys').style.transformOrigin = 'unset'
    } else {
      $('#keys').style.zoom = 'unset'
    }
  },
  updatePressedKeyHint(k) {
    if (!k) return
    this.updateKeysHintLayoutRatio()
    const keys = $$('.key')
    for (const key of keys) {
      key.classList.remove('pressed')
      if (key.getAttribute('key') && key.getAttribute('key').toLowerCase() === k) {
        key.classList.add('pressed')
        setTimeout(() => {
          key.classList.remove('pressed')
        }, 250)
      }
    }
  },
  updateTips() {
    const tips = $('#tips')
    tips.innerHTML = ''
    const currentScheme = Shuang.resource.scheme[this.config.scheme]
    if (currentScheme.tips) {
      const tipsToView = Array.isArray(currentScheme.tips) ? currentScheme.tips : [currentScheme.tips]
      for (const tip of tipsToView) {
        const newLine = document.createElement('div')
        newLine.classList.add('line')
        newLine.innerHTML = tip
        tips.appendChild(newLine)
      }
    }
  },
  setKeyboardLayout(keyboardLayoutName) {
    this.config.keyboardLayout = Object.keys(Shuang.resource.keyboardLayoutList)[
      Object.values(Shuang.resource.keyboardLayoutList)
        .findIndex(name => keyboardLayoutName === name)
    ]
    importJS('keyboard-layout/' + this.config.keyboardLayout, () => {
      this.updateKeyboardLayout()
    })
    writeStorage('keyboardLayout', this.config.keyboardLayout)
  },
  updateKeyboardLayout() {
    if (this.config.keyboardLayout === 'qwerty') {
      $('#pic').setAttribute('src', `img/${this.config.scheme}${this.config.bopomofo === 'true' ? '.bopomofo' : ''}.svg`)
      $('#keys').classList.remove('fix-left')
      this.updateSimulateKeyboard()
      this.updateKeysHint()
      return
    }
    if (!Shuang.resource.keyboardLayout[this.config.keyboardLayout]) return
    Shuang.core.keyboardLayout.init(
      `img/${this.config.scheme}${this.config.bopomofo === 'true' ? '.bopomofo' : ''}.png`, // svg 在 IE 浏览器下有 Security Error
      Shuang.resource.keyboardLayout[this.config.keyboardLayout],
      (url) => {
        const imgSrc = $('#pic').getAttribute('src')
        if (imgSrc && imgSrc.startsWith('blob:')) {
          URL.revokeObjectURL(imgSrc)
        }
        if (Shuang.core.keyboardLayout.instance.keyboardStyle.fixKeyStart) {
          $('#keys').classList.add('fix-left')
        } else {
          $('#keys').classList.remove('fix-left')
        }
        $('#pic').setAttribute('src', url)
        this.updateSimulateKeyboard()
        this.updateKeysHint()
      }
    )
    // Shuang.core.keyboardLayout.show()
  },
  updateSimulateKeyboard() {
    if (!Shuang.resource.keyboardLayout[this.config.keyboardLayout]) return
    const row1keys = $$('#keys .row-1 .key')
    for (let i = 0; i < row1keys.length; i++) {
      const key = Shuang.resource.keyboardLayout[this.config.keyboardLayout].row1[i]
      row1keys[i].setAttribute('key', key ? key.toUpperCase() : '')
    }
    const row2keys = $$('#keys .row-2 .key')
    for (let i = 0; i < row2keys.length; i++) {
      const key = Shuang.resource.keyboardLayout[this.config.keyboardLayout].row2[i]
      row2keys[i].setAttribute('key', key ? key.toUpperCase() : '')
    }
    const row3keys = $$('#keys .row-3 .key')
    for (let i = 0; i < row3keys.length; i++) {
      const key = Shuang.resource.keyboardLayout[this.config.keyboardLayout].row3[i]
      row3keys[i].setAttribute('key', key ? key.toUpperCase() : '')
    }
  }
}

function detectDarkMode() {
  if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
    return true
  }
  if (new Date().getHours() < 6 || new Date().getHours() > 22) {
    return true
  }
  return false
}

function normalizePracticeRange(mode) {
  if (mode === 'all' || mode === 'all-random' || mode === 'all-order') return 'all'
  return 'hard'
}

function normalizeWithoutPinyin(mode) {
  return mode === 'hard-random-without-pinyin' ? 'true' : 'false'
}

function readStorage(key = '') { return localStorage.getItem(key) }
function writeStorage(key = '', value = '') { localStorage.setItem(key, value) }
