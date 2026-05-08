/** last changed: 2025.1.9 */

Shuang.app.action = {
  currentWrongCounted: false,
  pendingNext: false,
  init() {
    /** Update Resources **/
    if (navigator && navigator.userAgent && /windows|linux/i.test(navigator.userAgent)) {
      Shuang.resource.emoji = { right: '✔️', wrong: '❌' }
    }

    /** Rendering **/
    function renderSelect(target, options, callback) {
      options.forEach(option => {
        const opt = document.createElement('option')
        if (option.disabled) opt.setAttribute('disabled', 'disabled')
        opt.innerText = option.text || option
        target.appendChild(opt)
      })
      target.onchange = e => {
        callback(e.target.value)
      }
    }

    const schemeList = Object.values(Shuang.resource.schemeList)
    const schemes = {
      common: schemeList.filter(scheme => !scheme.endsWith('*')),
      uncommon: schemeList
        .filter(scheme => scheme.endsWith('*') && !scheme.endsWith('**'))
        .map(scheme => scheme.slice(0, -1))
      ,
      rare: schemeList
        .filter(scheme => scheme.endsWith('**'))
        .map(scheme => scheme.slice(0, -2))
    }
    const schemeOptions = [
      { disabled: true, text: '常见' },
      ...schemes.common,
      { disabled: true, text: '小众' },
      ...schemes.uncommon,
      { disabled: true, text: '爱好者' },
      ...schemes.rare,
    ]
    renderSelect($('#scheme-select'), schemeOptions, value => {
      Shuang.app.setting.setScheme(value)
    })
    renderSelect($('#mode-select'), Object.values(Shuang.app.modeList).map(mode => mode.name), value => {
      Shuang.app.setting.setMode(value)
      this.next()
    })
    const keyboardLayoutOptions = Object.values(Shuang.resource.keyboardLayoutList)
    renderSelect($('#keyboard-layout-select'), keyboardLayoutOptions, (value) => {
      Shuang.app.setting.setKeyboardLayout(value)
    })

    /** Setting First Question **/
    Shuang.core.current = new Shuang.core.model('sh', 'uang')

    /** Reset Configs **/
    Shuang.app.setting.reload()

    /** Listen Events **/
    document.addEventListener('keydown', e => {
      if (['Backspace', 'Tab', 'Enter', ' '].includes(e.key)) {
        if (e.preventDefault) {
          e.preventDefault()
        } else {
          event.returnValue = false
        }
      }
    })
    document.addEventListener('keyup', e => {
      this.keyPressed(e)
    })
    $('#pic-switcher').addEventListener('change', e => {
      Shuang.app.setting.setPicVisible(e.target.checked)
    })
    $('#show-keys').addEventListener('change', e => {
      Shuang.app.setting.setShowKeys(e.target.checked)
    })
    $('#dark-mode-switcher').addEventListener('change', e => {
      Shuang.app.setting.setDarkMode(e.target.checked)
    })
    $('#more-settings-switcher').addEventListener('click', e => {
      Shuang.app.action.toggleMoreSettingsVisible()
    })
    $('#disable-mobile-keyboard').addEventListener('change', e => {
      Shuang.app.setting.setDisableMobileKeyboard(e.target.checked)
    })
    $('#bopomofo-switcher').addEventListener('change', e => {
      Shuang.app.setting.setBopomofo(e.target.checked)
    })
    $('#without-pinyin-switcher').addEventListener('change', e => {
      Shuang.app.setting.setWithoutPinyin(e.target.checked)
      this.next()
    })
    $('#reset-practice-progress').addEventListener('click', () => {
      if (window.confirm('重置当前方案的练习进度？')) {
        Shuang.core.practiceQueue.resetProgress()
        Shuang.app.setting.updatePracticeProgress()
        this.next()
      }
    })
    $('#dict').addEventListener('click', () => {
      if (!Shuang.core.current) {
        this.next()
        return
      }
      Shuang.core.current.beforeJudge()
      $('#a').value = Shuang.core.current.scheme.values().next().value
      this.judge()
    })
    window.addEventListener('resize', Shuang.app.setting.updateKeysHintLayoutRatio)
    window.addEventListener('beforeunload', () => {
      if (Shuang.core.practiceQueue && typeof Shuang.core.practiceQueue.flushSave === 'function') {
        Shuang.core.practiceQueue.flushSave()
      }
    })
    window.resizeTo(window.outerWidth, window.outerHeight)

    /** Simulate Keyboard */
    const keys = $$('.key')
    for (let i = 0; i < keys.length; i++) {
      // IE 不支持实例化 KeyboardEvent
      if (navigator && navigator.userAgent && /msie|trident/i.test(navigator.userAgent))
        break
      keys[i].addEventListener('click', (e) => {
        const key = e.target.getAttribute('key')
        if (!key) return
        const event = new KeyboardEvent('keyup', { key: key.toLowerCase() })
        event.simulated = true
        document.dispatchEvent(event)
      })
    }

    /** All Done **/
    this.next()
  },
  keyPressed(e) {
    if (!Shuang.core.current) {
      if (['Enter', ' '].includes(e.key)) {
        this.next()
      }
      return
    }
    switch (e.key) {
      case 'Backspace':
        this.redo()
        break
      case 'Tab':
        Shuang.core.current.beforeJudge()
        $('#a').value = Shuang.core.current.scheme.values().next().value
        this.judge()
        break
      case 'Enter':
      case ' ':
        if ($('#a').value.length === 2) {
          this.submitAnswer(this.judge())
        } else {
          this.redo()
        }
        break
      default:
        if (e.simulated) {
          $('#a').value += e.key.toLowerCase()
        }
        $('#a').value = $('#a').value
          .slice(0, 2)
          .replace(/[^a-zA-Z;]/g, '')
          .split('')
          .map((c, i) => i === 0 ? c.toUpperCase() : c.toLowerCase())
          .join('')
        Shuang.app.setting.updatePressedKeyHint(e.key)
        const canAuto = $('#a').value.length === 2
        const isRight = this.judge()
        if (canAuto) {
          if (isRight) {
            this.submitAnswer(true, e.simulated)
          } else if (!this.currentWrongCounted) {
            this.currentWrongCounted = true
            const scoreChange = Shuang.core.practiceQueue && Shuang.core.practiceQueue.isQueueMode()
              ? Shuang.core.practiceQueue.recordScore(false)
              : null
            Shuang.app.setting.updatePracticeProgress(scoreChange)
          }
        }
    }
  },
  judge() {
    const input = $('#a')
    const btn = $('#btn')
    const [sheng, yun] = input.value
    if (!yun) {
      btn.onclick = () => this.redo(true)
      btn.innerText = ''
      return false
    }
    if (Shuang.core.current.judge(sheng, yun)) {
      btn.onclick = () => this.submitAnswer(true, true)
      btn.innerText = Shuang.resource.emoji.right
      return true
    }
    btn.onclick = () => this.submitAnswer(false, true)
    btn.innerText = Shuang.resource.emoji.wrong
    Shuang.app.setting.setPracticeErrorHintVisible(true)
    return false
  },
  submitAnswer(isCorrect, noFocus) {
    if (!isCorrect) {
      Shuang.app.setting.setPracticeErrorHintVisible(true)
      let scoreChange = null
      if (!this.currentWrongCounted) {
        this.currentWrongCounted = true
        scoreChange = Shuang.core.practiceQueue && Shuang.core.practiceQueue.isQueueMode()
          ? Shuang.core.practiceQueue.recordScore(false)
          : null
      }
      Shuang.app.setting.updatePracticeProgress(scoreChange)
      return
    }
    const hadWrong = this.currentWrongCounted
    const scoreChange = !hadWrong && Shuang.core.practiceQueue && Shuang.core.practiceQueue.isQueueMode()
      ? Shuang.core.practiceQueue.recordScore(true)
      : null
    this.next(noFocus, true, scoreChange)
  },
  redo(noFocus) {
    $('#a').value = ''
    if (!noFocus) $('#a').focus()
    $('#btn').onclick = () => this.redo(noFocus)
    $('#btn').innerText = ''
  },
  next(noFocus, answerResult = null, scoreChange = null) {
    $('#a').value = ''
    if (!noFocus) $('#a').focus()
    Shuang.app.setting.setPracticeErrorHintVisible(false)
    this.currentWrongCounted = false
    switch (Shuang.app.setting.config.mode) {
      case 'all':
      case 'hard':
        Shuang.core.current = Shuang.core.practiceQueue.next(answerResult)
        break
    }
    if (!Shuang.core.current) return

    // Update Keys Hint
    Shuang.app.setting.updateQAndDict()
    Shuang.core.current.beforeJudge()
    Shuang.app.setting.updateKeysHint()
    Shuang.app.setting.updatePracticeProgress(scoreChange)
  },
  qrShow(targetId) {
    $('#' + targetId).style.display = 'block'
  },
  qrHide(target) {
    target.style.display = 'none'
  },
  toggleMoreSettingsVisible() {
    $('#more-settings').style.display = $('#more-settings').style.display === 'block' ? 'none' : 'block'
    $('#more-settings-switcher') .innerText = $('#more-settings').style.display === 'block' ? '收起更多' : '展开更多'
  }
}
