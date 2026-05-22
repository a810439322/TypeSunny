$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$qqHelper = Get-Content -Path (Join-Path $root 'Utils\QQHelper.cs') -Raw
$mainWindow = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw

function Get-BlockText($content, $signature) {
    $start = $content.IndexOf($signature, [System.StringComparison]::Ordinal)
    if ($start -lt 0) {
        throw "Unable to find signature [$signature]."
    }

    $braceStart = $content.IndexOf('{', $start)
    if ($braceStart -lt 0) {
        throw "Unable to find body for [$signature]."
    }

    $depth = 0
    for ($i = $braceStart; $i -lt $content.Length; $i++) {
        if ($content[$i] -eq '{') {
            $depth++
        }
        elseif ($content[$i] -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $content.Substring($start, $i - $start + 1)
            }
        }
    }

    throw "Unable to parse body for [$signature]."
}

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "${name}: expected to find [$needle]"
    }
}

function Assert-NotContains($name, $content, $needle) {
    if ($content.Contains($needle)) {
        throw "${name}: expected not to find [$needle]"
    }
}

$msgRequest = Get-BlockText $qqHelper 'class MsgRequest'
$sendMessage = Get-BlockText $qqHelper 'static private bool SendMessage'
$pasteAndSend = Get-BlockText $qqHelper 'static private bool PasteAndSendMessage'
$activateDocumentInput = Get-BlockText $qqHelper 'static private void ActivateDocumentInput'
$findQQWindow = Get-BlockText $qqHelper 'static private IUIAutomationElement FindQQWindowWithRetry'
$focusTargetGuard = Get-BlockText $qqHelper 'static private bool IsFocusedElementSafeForTargetConversation'
$sendSingle = Get-BlockText $qqHelper 'public static void SendQQMessage '
$singleHelper = Get-BlockText $qqHelper 'private static void SendQQMessageHelper'
$doubleSend = Get-BlockText $qqHelper 'public static void SendQQMessageD'

Assert-Contains 'single-send request should carry message content instead of using clipboard as queue storage' $msgRequest 'msgContent'
Assert-Contains 'single-send scheduler should pass message content into request' $sendSingle 'new MsgRequest(groupName, msgContent, caller)'
Assert-Contains 'single-send scheduler should disable effective delay while keeping the caller argument observable in logs' $sendSingle 'new Timer(SendQQMessageHelper, m, 0, Timeout.Infinite)'
Assert-Contains 'single-send helper should read content from request' $singleHelper 'string msgContent = m.msgContent;'
Assert-NotContains 'single-send helper should not read queued content back from clipboard' $singleHelper 'Win32.Win32GetText(13)'

Assert-Contains 'QQ automation should have a shared serialization lock' $qqHelper 'SendAutomationLock'
Assert-Contains 'single-send helper should serialize QQ window and clipboard automation' $singleHelper 'lock (SendAutomationLock)'
Assert-Contains 'double-send helper should serialize QQ window and clipboard automation' $doubleSend 'lock (SendAutomationLock)'

Assert-Contains 'SendMessage should log before searching for the QQ send button' $sendMessage 'SendMessage FindFirst begin'
Assert-Contains 'SendMessage should log after searching for the QQ send button' $sendMessage 'SendMessage FindFirst done'
Assert-Contains 'SendMessage should log before invoking the QQ send button' $sendMessage 'SendMessage invoke begin'
Assert-Contains 'SendMessage should log after invoking the QQ send button' $sendMessage 'SendMessage invoke done'
Assert-Contains 'SendMessage should report successful invoke to callers' $sendMessage 'return true;'
Assert-Contains 'SendMessage should report timeout/failure to callers' $sendMessage 'return false;'

Assert-Contains 'single-send helper should retry paste/send when the QQ input did not receive focus' $singleHelper 'PasteAndSendMessage(q, grouplist, edits, groupName, msgContent, "SendQQMessageHelper", ref cachedSendButton)'
Assert-Contains 'double-send helper should send the score through the same verified paste/send path' $doubleSend 'PasteAndSendMessage(q, grouplist, edits, groupName, msgContent1, "SendQQMessageD score", ref cachedSendButton)'
Assert-Contains 'double-send helper should send the article through the same verified paste/send path' $doubleSend 'PasteAndSendMessage(q, grouplist, edits, groupName, msgContent2, "SendQQMessageD article", ref cachedSendButton)'
Assert-Contains 'double-send helper should wait for the score send to clear before pasting the article' $doubleSend 'WaitForSendButtonDisabled(q, "SendQQMessageD score after send", cachedSendButton)'
Assert-Contains 'single-send helper should give paste/send the conversation list and target group for focus validation' $singleHelper 'PasteAndSendMessage(q, grouplist, edits, groupName, msgContent, "SendQQMessageHelper", ref cachedSendButton)'
Assert-Contains 'double-send helper should give score paste/send the conversation list and target group for focus validation' $doubleSend 'PasteAndSendMessage(q, grouplist, edits, groupName, msgContent1, "SendQQMessageD score", ref cachedSendButton)'
Assert-Contains 'double-send helper should give article paste/send the conversation list and target group for focus validation' $doubleSend 'PasteAndSendMessage(q, grouplist, edits, groupName, msgContent2, "SendQQMessageD article", ref cachedSendButton)'
Assert-Contains 'paste/send should verify focus is in the target conversation before using Ctrl+V' $pasteAndSend 'IsFocusedElementSafeForTargetConversation(q, groupList, groupName,'
Assert-Contains 'paste/send should re-check target conversation focus after Ctrl+V before clicking send' $pasteAndSend 'after CtrlV")'
Assert-Contains 'target conversation guard should reject focus outside the QQ automation tree' $focusTargetGuard 'isDescendantOfQQ'
Assert-Contains 'target conversation guard should reject focus in the conversation list' $focusTargetGuard 'isInConversationList'
Assert-Contains 'target conversation guard should reject named right-pane groups that do not match the target group' $focusTargetGuard 'targetNameSafe'
Assert-NotContains 'paste/send should not rely on broad window-rectangle overlap for focus validation' $pasteAndSend 'IsFocusedElementInsideWindow'
Assert-NotContains 'QQ focus validation should not use rectangle intersection as proof of QQ ownership' $qqHelper 'IsElementInsideWindow'
Assert-Contains 'QQ document activation should clear hover previews before clicking' $activateDocumentInput 'ClearQQHoverBeforeClick'
Assert-Contains 'QQ document activation should wait after moving the cursor away from a hovered conversation' $qqHelper 'QQHoverClearDelayMs'
Assert-NotContains 'QQ document activation should avoid mid-pane click points covered by hover conversation previews' $activateDocumentInput '0.50'
Assert-NotContains 'QQ document activation should avoid old center-left click points covered by hover conversation previews' $activateDocumentInput '0.58'

Assert-Contains 'QQ helper should mark queued/running sends before the timer thread starts' $sendSingle 'BeginSendAutomation("SendQQMessage schedule")'
Assert-Contains 'single-send helper should release the queued/running send marker after automation ends' $singleHelper 'EndSendAutomation(caller, "SendQQMessageHelper", focusCallerAfterAutomation)'
Assert-Contains 'double-send helper should mark synchronous sends as active' $doubleSend 'BeginSendAutomation("SendQQMessageD")'
Assert-Contains 'double-send helper should release its active marker after automation ends' $doubleSend 'EndSendAutomation(caller, "SendQQMessageD", focusCallerAfterAutomation)'
Assert-Contains 'FocusInput should not steal foreground focus during QQ automation' $mainWindow 'QQHelper.TryDeferFocusInput("MainWindow.FocusInput")'
Assert-Contains 'QQ window lookup should retry transient top-level UIAutomation misses' $findQQWindow 'while (waitedTime <= maxWaitTime)'
Assert-Contains 'single-send helper should use retrying QQ window lookup' $singleHelper 'FindQQWindowWithRetry("SendQQMessageHelper")'
Assert-Contains 'double-send helper should use retrying QQ window lookup' $doubleSend 'FindQQWindowWithRetry("SendQQMessageD")'
Assert-Contains 'single-send helper should wait for QQ conversation list readiness instead of failing immediately' $singleHelper 'FindGroupListWithRetry(q, "SendQQMessageHelper")'
Assert-Contains 'double-send helper should wait for QQ conversation list readiness instead of failing immediately' $doubleSend 'FindGroupListWithRetry(q, "SendQQMessageD")'
Assert-Contains 'send automation should leave QQ foreground visible when no message was sent' $qqHelper 'Send automation keep QQ foreground'
Assert-NotContains 'send automation should not delay focus return after successful sends' $qqHelper 'FocusCallerDelayAfterSendMs'
Assert-NotContains 'send automation should not keep cancelable delayed focus-return epochs' $qqHelper 'SendAutomationFocusEpoch'
Assert-NotContains 'send automation should not queue delayed focus return work items' $qqHelper 'ThreadPool.QueueUserWorkItem'
Assert-NotContains 'send automation should not log delayed focus-return skips' $qqHelper 'Send automation focus caller skipped'

Write-Host 'All QQ send serialization tests passed.'
