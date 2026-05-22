// 调试 QQ 发送时取消下一行注释以启用 WriteSendDebugLog 所有调用（写 QQ发送调试.log）
// #define DEBUG_QQ_SEND
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Interop.UIAutomationClient;
using static TypeSunny.UI.MainWindow;

namespace TypeSunny.Utils
{
    class MsgRequest
    {
        public string groupName = "";
        public string msgContent = "";

        public  Window caller = null;

        public MsgRequest(string groupName, string msgContent, Window caller)
        {
            this.groupName = groupName;
            this.msgContent = msgContent;
            this.caller = caller;
        }
    }
    internal static class QQHelper
    {
        // 显示调试日志的弹窗（改为写入文件）
        // private static void ShowDebugLog(string log)
        // {
        //     try
        //     {
        //         // 获取程序运行目录
        //         string appDir = AppDomain.CurrentDomain.BaseDirectory;
        //
        //         // 生成带时间戳的日志文件名
        //         string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        //         string logFile = System.IO.Path.Combine(appDir, $"QQ调试日志_{timestamp}.txt");
        //
        //         // 写入日志文件
        //         System.IO.File.WriteAllText(logFile, log, Encoding.UTF8);
        //
        //         // 弹窗提示用户日志文件位置
        //         System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //         {
        //             System.Windows.MessageBox.Show($"调试日志已保存到:\n{logFile}\n\n请打开该文件查看详细日志。", "QQ调试信息", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        //         });
        //     }
        //     catch (Exception ex)
        //     {
        //         // 如果写入文件失败，降级到弹窗显示
        //         try
        //         {
        //             System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //             {
        //                 System.Windows.MessageBox.Show($"日志保存失败: {ex.Message}\n\n{log}", "QQ调试信息", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        //             });
        //         }
        //         catch { }
        //     }
        // }

        #region dll

        [DllImport("user32.dll", EntryPoint = "GetWindowText")]
        public static extern int GetWindowText(int hwnd, StringBuilder lpString, int cch);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumWindows")]
        public static extern int EnumWindows(CallBack x, int y);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetClassName")]
        public static extern int GetClassName(int hWnd, StringBuilder lpClassName, int nMaxCount);

       
        #endregion


        public delegate bool CallBack(int hwnd, int lParam);

        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "SwitchToThisWindow")]
        private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);


        public static List<string> AvailTitle = new List<string>();



        static CUIAutomation root = new CUIAutomation();

        static List<string> QunList = new List<string>();
        public static string LastDebugInfo = "";  // 保存最后的调试信息

        // 缓存：群名到会话列表元素的映射（提升性能）
        static Dictionary<string, IUIAutomationElement> QunElementCache = new Dictionary<string, IUIAutomationElement>();
        static DateTime QunCacheTime = DateTime.MinValue;
        static readonly TimeSpan QunCacheExpiry = TimeSpan.FromSeconds(30); // 缓存30秒
        private static readonly object SendAutomationLock = new object();
        private static readonly object SendDebugLogLock = new object();
        private static int SendAutomationPendingCount = 0;
        private static int DeferredFocusInputRequested = 0;
        private const int QQHoverClearDelayMs = 180;

        public static string SendDebugLogPath
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QQ发送调试.log"); }
        }

        [Conditional("DEBUG_QQ_SEND")]
        public static void WriteSendDebugLog(string message)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [T{Thread.CurrentThread.ManagedThreadId}] {message}\r\n";
                lock (SendDebugLogLock)
                {
                    System.IO.File.AppendAllText(SendDebugLogPath, line, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        public static bool TryDeferFocusInput(string context)
        {
            if (Volatile.Read(ref SendAutomationPendingCount) <= 0)
                return false;

            Interlocked.Exchange(ref DeferredFocusInputRequested, 1);
            WriteSendDebugLog($"FocusInput deferred: context={context}, pending={Volatile.Read(ref SendAutomationPendingCount)}");
            return true;
        }

        private static void BeginSendAutomation(string context)
        {
            int pending = Interlocked.Increment(ref SendAutomationPendingCount);
            WriteSendDebugLog($"Send automation begin: context={context}, pending={pending}");
        }

        private static void EndSendAutomation(Window caller, string context, bool focusCaller)
        {
            int pending = Interlocked.Decrement(ref SendAutomationPendingCount);
            if (pending < 0)
            {
                Interlocked.Exchange(ref SendAutomationPendingCount, 0);
                pending = 0;
            }

            int deferred = Interlocked.Exchange(ref DeferredFocusInputRequested, 0);
            WriteSendDebugLog($"Send automation end: context={context}, pending={pending}, deferredFocus={deferred}, focusCaller={focusCaller}");
            if (pending != 0)
                return;
            if (!focusCaller)
            {
                WriteSendDebugLog($"Send automation keep QQ foreground: context={context}");
                return;
            }

            try
            {
                caller?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    WriteSendDebugLog($"Send automation focus caller begin: context={context}");
                    Current?.FocusInput();
                    WriteSendDebugLog($"Send automation focus caller done: context={context}");
                }), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"Send automation focus caller dispatch failed: context={context}, error={ex.Message}");
            }
        }

        private static string TrimForLog(string text, int maxLength)
        {
            if (text == null)
                return "<null>";

            string normalized = text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized.Substring(0, maxLength) + "...";
        }

        private static string ContentForLog(string name, string content)
        {
            return $"{name}: null={content == null}, empty={string.IsNullOrEmpty(content)}, white={string.IsNullOrWhiteSpace(content)}, len={content?.Length ?? 0}, preview=\"{TrimForLog(content, 80)}\"";
        }

        private static string ElementForLog(IUIAutomationElement element)
        {
            if (element == null)
                return "<null>";

            try
            {
                return $"type={GetControlTypeName(element.CurrentControlType)}, name=\"{TrimForLog(element.CurrentName, 80)}\", class=\"{TrimForLog(element.CurrentClassName, 80)}\", enabled={element.CurrentIsEnabled}, offscreen={element.CurrentIsOffscreen}, {RectForLog(element)}";
            }
            catch (Exception ex)
            {
                return $"<element read failed: {ex.Message}>";
            }
        }

        private static string RectForLog(IUIAutomationElement element)
        {
            if (element == null)
                return "rect=<null>";

            try
            {
                var rect = element.CurrentBoundingRectangle;
                return $"rect=({rect.left},{rect.top},{rect.right},{rect.bottom}), size=({rect.right - rect.left}x{rect.bottom - rect.top})";
            }
            catch (Exception ex)
            {
                return $"rect=<read failed: {ex.Message}>";
            }
        }

        // 保存调试信息到文件（追加模式）
        private static void SaveDebugInfo(string info, string prefix = "QQ发送")
        {
            // 日志已禁用
        }

        // 辅助函数：将ControlType ID转换为可读名称
        static string GetControlTypeName(int controlType)
        {
            switch (controlType)
            {
                case 50000: return "Button";
                case 50001: return "Calendar";
                case 50002: return "CheckBox";
                case 50003: return "ComboBox";
                case 50004: return "Edit";
                case 50005: return "Hyperlink";
                case 50006: return "Image";
                case 50007: return "ListItem";
                case 50008: return "List";
                case 50009: return "Menu";
                case 50010: return "MenuBar";
                case 50011: return "MenuItem";
                case 50012: return "ProgressBar";
                case 50013: return "RadioButton";
                case 50014: return "ScrollBar";
                case 50015: return "Slider";
                case 50016: return "Spinner";
                case 50017: return "StatusBar";
                case 50018: return "Tab";
                case 50019: return "TabItem";
                case 50020: return "Text";
                case 50021: return "ToolBar";
                case 50022: return "ToolTip";
                case 50023: return "Tree";
                case 50024: return "TreeItem";
                case 50025: return "Custom";
                case 50026: return "Group";
                case 50027: return "Thumb";
                case 50028: return "DataGrid";
                case 50029: return "DataItem";
                case 50030: return "Document";
                case 50031: return "SplitButton";
                case 50032: return "Window";
                case 50033: return "Pane";
                case 50034: return "Header";
                case 50035: return "HeaderItem";
                case 50036: return "Table";
                case 50037: return "TitleBar";
                case 50038: return "Separator";
                default: return $"Unknown({controlType})";
            }
        }

        // 判断是否是时间标记
        public static bool IsTimeMarker(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // 如果包含赛文格式标记，不是时间标记
            if (text.Contains("-----第"))
                return false;

            // 时间关键词
            string[] timeKeywords = { "昨天", "今天", "星期", "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
            foreach (var keyword in timeKeywords)
            {
                if (text.Contains(keyword))
                    return true;
            }

            // 时间格式：包含冒号且长度较短（如 "09:44"、"上午"、"下午"）
            if (text.Contains(":") && text.Length <= 10)
                return true;

            // 上午/下午标记
            if (text.Contains("上午") || text.Contains("下午"))
                return true;

            return false;
        }

        // ========== 公共方法：QQ自动化操作 ==========

        /// <summary>
        /// 检查当前是否已在目标群
        /// </summary>
        static private bool IsAlreadyInGroup(IUIAutomationElement q, string groupName)
        {
            var allButtons = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_ButtonControlTypeId));
            if (allButtons != null)
            {
                for (int bi = 0; bi < allButtons.Length; bi++)
                {
                    var btn = allButtons.GetElement(bi);
                    string btnName = btn.CurrentName;
                    if (!string.IsNullOrWhiteSpace(btnName) && btnName == groupName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 激活Document输入框（点击Document底部）
        /// </summary>
        static private void LogFocusedElement(string context)
        {
            try
            {
                WriteSendDebugLog($"{context} focused element: {ElementForLog(root.GetFocusedElement())}");
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"{context} focused element read failed: {ex.Message}");
            }
        }

        static private bool IsSameElement(IUIAutomationElement first, IUIAutomationElement second)
        {
            if (first == null || second == null)
                return false;

            try
            {
                return root.CompareElements(first, second) != 0;
            }
            catch
            {
                return false;
            }
        }

        static private bool IsElementInSubtree(IUIAutomationElement ancestor, IUIAutomationElement element)
        {
            if (ancestor == null || element == null)
                return false;

            try
            {
                var walker = root.RawViewWalker;
                var current = element;
                for (int i = 0; current != null && i < 64; i++)
                {
                    if (IsSameElement(ancestor, current))
                        return true;

                    current = walker.GetParentElement(current);
                }
            }
            catch
            {
            }

            return false;
        }

        static private bool IsElementWithinConversationListBounds(IUIAutomationElement element, IUIAutomationElement groupList)
        {
            if (element == null || groupList == null)
                return false;

            var elementRect = element.CurrentBoundingRectangle;
            var groupListRect = groupList.CurrentBoundingRectangle;

            if (elementRect.right <= elementRect.left || elementRect.bottom <= elementRect.top)
                return false;
            if (groupListRect.right <= groupListRect.left || groupListRect.bottom <= groupListRect.top)
                return false;

            const double margin = 4;
            double centerX = (elementRect.left + elementRect.right) / 2.0;
            double centerY = (elementRect.top + elementRect.bottom) / 2.0;
            return centerX >= groupListRect.left - margin
                   && centerX <= groupListRect.right + margin
                   && centerY >= groupListRect.top - margin
                   && centerY <= groupListRect.bottom + margin;
        }

        static private bool IsNameCompatibleWithTargetGroup(string focusedName, string groupName)
        {
            if (string.IsNullOrWhiteSpace(focusedName) || string.IsNullOrWhiteSpace(groupName))
                return true;

            string name = focusedName.Trim();
            string target = groupName.Trim();
            return name == target
                   || name.StartsWith(target + " ")
                   || name.StartsWith(target + "\t")
                   || name.StartsWith(target + "\r")
                   || name.StartsWith(target + "\n")
                   || name.StartsWith(target + "　");
        }

        static private bool IsFocusedElementNameSafeForTarget(IUIAutomationElement focused, IUIAutomationElement groupList, string groupName)
        {
            if (focused == null || groupList == null)
                return true;

            string focusedName = focused.CurrentName;
            if (string.IsNullOrWhiteSpace(focusedName))
                return true;

            var focusedRect = focused.CurrentBoundingRectangle;
            var groupListRect = groupList.CurrentBoundingRectangle;
            if (focusedRect.right <= focusedRect.left || focusedRect.bottom <= focusedRect.top)
                return true;
            if (groupListRect.right <= groupListRect.left || groupListRect.bottom <= groupListRect.top)
                return true;

            int controlType = focused.CurrentControlType;
            double height = focusedRect.bottom - focusedRect.top;
            bool looksLikeRightConversationPane = focusedRect.left >= groupListRect.right - 5
                                                  && height >= 100
                                                  && (controlType == UIA_ControlTypeIds.UIA_GroupControlTypeId
                                                      || controlType == UIA_ControlTypeIds.UIA_PaneControlTypeId
                                                      || controlType == UIA_ControlTypeIds.UIA_DocumentControlTypeId);
            if (!looksLikeRightConversationPane)
                return true;

            return IsNameCompatibleWithTargetGroup(focusedName, groupName);
        }

        static private bool IsFocusedElementSafeForTargetConversation(IUIAutomationElement q, IUIAutomationElement groupList, string groupName, string context)
        {
            try
            {
                var focused = root.GetFocusedElement();
                bool isDescendantOfQQ = IsElementInSubtree(q, focused);
                bool isInConversationList = IsElementInSubtree(groupList, focused) || IsElementWithinConversationListBounds(focused, groupList);
                bool targetNameSafe = IsFocusedElementNameSafeForTarget(focused, groupList, groupName);
                bool safe = isDescendantOfQQ && !isInConversationList && targetNameSafe;
                WriteSendDebugLog($"{context} target focus safe={safe}, isDescendantOfQQ={isDescendantOfQQ}, isInConversationList={isInConversationList}, targetNameSafe={targetNameSafe}: {ElementForLog(focused)}");
                return safe;
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"{context} target focus safe read failed: {ex.Message}");
                return false;
            }
        }

        static private string ExtractConversationListItemName(IUIAutomationElement element)
        {
            if (element == null)
                return "";

            string itemName = element.CurrentName;
            string extractedName = "";
            if (string.IsNullOrWhiteSpace(itemName))
            {
                var descendants = element.FindAll(TreeScope.TreeScope_Descendants, root.CreateTrueCondition());
                if (descendants != null && descendants.Length > 0)
                {
                    System.Text.StringBuilder nameBuilder = new System.Text.StringBuilder();
                    for (int j = 0; j < descendants.Length; j++)
                    {
                        var desc = descendants.GetElement(j);
                        string descName = desc.CurrentName;
                        int descControlType = desc.CurrentControlType;

                        if (string.IsNullOrWhiteSpace(descName))
                            continue;

                        if (IsTimeMarker(descName))
                            break;

                        if (descControlType == UIA_ControlTypeIds.UIA_TextControlTypeId)
                            nameBuilder.Append(descName);
                    }

                    extractedName = nameBuilder.ToString();
                }
            }
            else
            {
                extractedName = itemName;
            }

            extractedName = extractedName.Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');
            int timeIndex = -1;
            for (int i = 1; i < extractedName.Length - 3; i++)
            {
                if (extractedName[i - 1] == ' ' && char.IsDigit(extractedName[i]) && extractedName[i + 1] == ':')
                {
                    if (i + 2 < extractedName.Length && char.IsDigit(extractedName[i + 2]))
                    {
                        timeIndex = i;
                        break;
                    }
                }
            }

            if (timeIndex > 0)
                extractedName = extractedName.Substring(0, timeIndex).Trim();

            return extractedName;
        }

        static private bool ConversationNameMatchesTarget(string candidateName, string groupName)
        {
            if (string.IsNullOrWhiteSpace(candidateName) || string.IsNullOrWhiteSpace(groupName))
                return false;

            return candidateName == groupName
                   || candidateName.StartsWith(groupName)
                   || candidateName.Contains(groupName);
        }

        static private bool TryReactivateTargetGroup(IUIAutomationElement q, IUIAutomationElement groupList, string groupName, string context)
        {
            if (q == null || groupList == null)
                return false;

            try
            {
                var allChildren = groupList.FindAll(TreeScope.TreeScope_Children, root.CreateTrueCondition());
                WriteSendDebugLog($"{context} target group recovery scan: count={allChildren?.Length ?? 0}, group=\"{TrimForLog(groupName, 80)}\"");
                if (allChildren == null || allChildren.Length <= 0)
                    return false;

                for (int i = 0; i < allChildren.Length; i++)
                {
                    var child = allChildren.GetElement(i);
                    string extractedName = ExtractConversationListItemName(child);
                    if (!ConversationNameMatchesTarget(extractedName, groupName))
                        continue;

                    WriteSendDebugLog($"{context} target group recovery matched child[{i}]: extracted=\"{TrimForLog(extractedName, 120)}\"");
                    var sp = child.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                    if (sp == null)
                    {
                        WriteSendDebugLog($"{context} target group recovery failed: invoke pattern missing.");
                        return false;
                    }

                    sp.Invoke();
                    Win32.Delay(120);
                    LogFocusedElement($"{context} after target group recovery invoke");
                    return true;
                }
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"{context} target group recovery failed: {ex.Message}");
            }

            return false;
        }

        static private void ClearQQHoverBeforeClick(string context, int clickX, int clickY)
        {
            WriteSendDebugLog($"{context} clear hover before click: x={clickX}, y={clickY}, delay={QQHoverClearDelayMs}ms");
            Win32.MoveCursor(clickX, clickY);
            Win32.Delay(QQHoverClearDelayMs);
        }

        static private void ActivateDocumentInput(IUIAutomationElement q, IUIAutomationElement document, int attemptIndex = 1)
        {
            if (document.CurrentControlType == UIA_ControlTypeIds.UIA_DocumentControlTypeId)
            {
                IntPtr qHwnd = GetQQNativeWindowHandle(q);
                var docRect = document.CurrentBoundingRectangle;
                double width = docRect.right - docRect.left;
                double height = docRect.bottom - docRect.top;

                double[] xFractions = attemptIndex <= 1
                    ? new[] { 0.72, 0.72 }
                    : new[] { 0.78, 0.72, 0.70 };
                int[] yOffsets = attemptIndex <= 1
                    ? new[] { 80, 60 }
                    : new[] { 80, 60, 95 };

                for (int i = 0; i < xFractions.Length; i++)
                {
                    int clickX = (int)(docRect.left + width * xFractions[i]);
                    int clickY = height < 160
                        ? (int)((docRect.top + docRect.bottom) / 2)
                        : (int)(docRect.bottom - yOffsets[i]);

                    if (clickX < docRect.left + 5)
                        clickX = (int)docRect.left + 5;
                    if (clickX > docRect.right - 5)
                        clickX = (int)docRect.right - 5;
                    if (clickY < docRect.top + 5)
                        clickY = (int)docRect.top + 5;
                    if (clickY > docRect.bottom - 5)
                        clickY = (int)docRect.bottom - 5;

                    if (qHwnd != IntPtr.Zero)
                    {
                        IntPtr fg = IntPtr.Zero;
                        try { fg = Win32.GetForegroundWindow(); } catch { }
                        if (fg != qHwnd)
                        {
                            WriteSendDebugLog($"ActivateDocumentInput skip click: attempt={attemptIndex}, index={i + 1}, foreground=0x{fg.ToInt64():X}, qHwnd=0x{qHwnd.ToInt64():X}");
                            return;
                        }
                    }

                    WriteSendDebugLog($"ActivateDocumentInput click: attempt={attemptIndex}, index={i + 1}, {RectForLog(document)}, x={clickX}, y={clickY}, xFraction={xFractions[i]:0.00}, yOffset={yOffsets[i]}");
                    ClearQQHoverBeforeClick($"ActivateDocumentInput attempt={attemptIndex}, index={i + 1}", clickX, clickY);
                    Win32.ClickCurrentPosition();
                    if (i + 1 < xFractions.Length)
                        Win32.Delay(30);
                }
            }
            else
            {
                WriteSendDebugLog($"ActivateDocumentInput skipped: control is {ElementForLog(document)}");
            }
        }

        static private IUIAutomationElement FindBottomVisibleDocument(IUIAutomationElement q, string context)
        {
            try
            {
                var docCond = root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId);
                var firstDoc = q.FindFirst(TreeScope.TreeScope_Descendants, docCond);
                if (firstDoc != null)
                {
                    try
                    {
                        bool isOffscreen = firstDoc.CurrentIsOffscreen != 0;
                        var rect = firstDoc.CurrentBoundingRectangle;
                        bool rectValid = rect.right > rect.left && rect.bottom > rect.top;
                        if (!isOffscreen && rectValid)
                        {
                            WriteSendDebugLog($"{context} document fast path hit: {ElementForLog(firstDoc)}");
                            return firstDoc;
                        }
                        WriteSendDebugLog($"{context} document fast path rejected (offscreen={isOffscreen}, rectValid={rectValid}), falling back to FindAll: {ElementForLog(firstDoc)}");
                    }
                    catch (Exception ex)
                    {
                        WriteSendDebugLog($"{context} document fast path probe failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"{context} document fast path threw: {ex.Message}");
            }

            var allDocuments = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId));
            WriteSendDebugLog($"{context} document scan: count={allDocuments?.Length ?? 0}");

            IUIAutomationElement best = null;
            double bestBottom = double.MinValue;

            if (allDocuments != null && allDocuments.Length > 0)
            {
                for (int i = 0; i < allDocuments.Length; i++)
                {
                    var doc = allDocuments.GetElement(i);
                    bool isOffscreen = doc.CurrentIsOffscreen != 0;
                    string docInfo = ElementForLog(doc);
                    WriteSendDebugLog($"{context} Document[{i}]: offscreen={isOffscreen}, {docInfo}");

                    if (isOffscreen)
                        continue;

                    var rect = doc.CurrentBoundingRectangle;
                    if (rect.right <= rect.left || rect.bottom <= rect.top)
                        continue;

                    if (best == null || rect.bottom > bestBottom)
                    {
                        best = doc;
                        bestBottom = rect.bottom;
                    }
                }
            }

            WriteSendDebugLog($"{context} selected document: {ElementForLog(best)}");
            return best;
        }

        static private void ActivateQQWindow(IUIAutomationElement q, string context)
        {
            if (q == null)
                return;

            try
            {
                WriteSendDebugLog($"{context} QQ activate begin: {ElementForLog(q)}");
                var wp = q.GetCurrentPattern(UIA_PatternIds.UIA_WindowPatternId) as IUIAutomationWindowPattern;
                wp?.SetWindowVisualState(WindowVisualState.WindowVisualState_Normal);
                q.SetFocus();
                TryEnsureQQForeground(q, $"{context} after QQ activate");
                LogFocusedElement($"{context} after QQ activate");
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"{context} QQ activate failed: {ex.Message}");
            }
        }

        static private IntPtr GetQQNativeWindowHandle(IUIAutomationElement q)
        {
            if (q == null)
                return IntPtr.Zero;
            try
            {
                return q.CurrentNativeWindowHandle;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        static private bool TryEnsureQQForeground(IUIAutomationElement q, string context)
        {
            IntPtr qHwnd = GetQQNativeWindowHandle(q);
            return TryEnsureQQForeground(qHwnd, context);
        }

        static private bool TryEnsureQQForeground(IntPtr qHwnd, string context)
        {
            if (qHwnd == IntPtr.Zero)
            {
                WriteSendDebugLog($"{context} foreground ensure skipped: qHwnd is zero");
                return false;
            }

            IntPtr before = IntPtr.Zero;
            try { before = Win32.GetForegroundWindow(); } catch { }

            if (before == qHwnd)
            {
                WriteSendDebugLog($"{context} foreground already QQ: hwnd=0x{qHwnd.ToInt64():X}");
                return true;
            }

            bool setResult = false;
            try { setResult = Win32.SetForegroundWindow(qHwnd); } catch (Exception ex)
            {
                WriteSendDebugLog($"{context} SetForegroundWindow threw: {ex.Message}");
            }

            IntPtr after = IntPtr.Zero;
            try { after = Win32.GetForegroundWindow(); } catch { }

            bool ok = after == qHwnd;
            WriteSendDebugLog($"{context} foreground switch: before=0x{before.ToInt64():X}, after=0x{after.ToInt64():X}, qHwnd=0x{qHwnd.ToInt64():X}, setResult={setResult}, ok={ok}");
            return ok;
        }

        static private IUIAutomationElement FindQQWindowOnce()
        {
            string mainTitle = "QQ";
            var rootElement = root.GetRootElement();
            var exact = rootElement.FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, mainTitle));
            if (exact != null)
                return exact;

            var candidates = rootElement.FindAll(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ClassNamePropertyId, "Chrome_WidgetWin_1"));
            IUIAutomationElement groupListCandidate = null;

            if (candidates != null)
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates.GetElement(i);
                    string name = candidate.CurrentName;
                    if (!string.IsNullOrWhiteSpace(name) && name.Trim() == mainTitle)
                        return candidate;

                    if (groupListCandidate == null)
                    {
                        var groupList = candidate.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "会话列表"));
                        if (groupList != null)
                            groupListCandidate = candidate;
                    }
                }
            }

            return groupListCandidate;
        }

        static private IUIAutomationElement FindQQWindowWithRetry(string context, int maxWaitTime = 1500)
        {
            DateTime startTime = DateTime.Now;
            int waitedTime = 0;
            int lastDetailedLogTime = -1000;

            while (waitedTime <= maxWaitTime)
            {
                bool shouldLog = waitedTime == 0 || waitedTime - lastDetailedLogTime >= 300;
                if (shouldLog)
                {
                    lastDetailedLogTime = waitedTime;
                    WriteSendDebugLog($"{context} QQ window Find begin: waited={waitedTime}ms");
                }

                var q = FindQQWindowOnce();
                if (shouldLog || q != null)
                    WriteSendDebugLog($"{context} QQ window Find done: waited={waitedTime}ms, window={ElementForLog(q)}");

                if (q != null)
                    return q;

                Win32.Delay(10);
                waitedTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            }

            WriteSendDebugLog($"{context} QQ window timeout: waited={waitedTime}ms");
            return null;
        }

        static private IUIAutomationElement FindGroupListWithRetry(IUIAutomationElement q, string context, int maxWaitTime = 1500)
        {
            DateTime startTime = DateTime.Now;
            int waitedTime = 0;
            int lastDetailedLogTime = -1000;

            while (waitedTime <= maxWaitTime)
            {
                bool shouldLog = waitedTime == 0 || waitedTime - lastDetailedLogTime >= 300;
                if (shouldLog)
                {
                    lastDetailedLogTime = waitedTime;
                    WriteSendDebugLog($"{context} group list FindFirst begin: waited={waitedTime}ms");
                }

                var grouplist = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "会话列表"));
                if (shouldLog || grouplist != null)
                    WriteSendDebugLog($"{context} group list FindFirst done: waited={waitedTime}ms, groupList={ElementForLog(grouplist)}");

                if (grouplist != null)
                    return grouplist;

                if (waitedTime == 0)
                    ActivateQQWindow(q, $"{context} group list missing");

                Win32.Delay(10);
                waitedTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            }

            WriteSendDebugLog($"{context} group list timeout: waited={waitedTime}ms");
            return null;
        }

        /// <summary>
        /// 发送消息（等待发送按钮启用后点击）
        /// </summary>
        static private bool IsAlreadyInTargetGroup(IUIAutomationElement q, string groupName, string context)
        {
            if (q == null || string.IsNullOrWhiteSpace(groupName))
                return false;
            try
            {
                var c1 = root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_ButtonControlTypeId);
                var c2 = root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, groupName);
                var cond = root.CreateAndCondition(c1, c2);
                var btn = q.FindFirst(TreeScope.TreeScope_Descendants, cond);
                bool found = btn != null;
                WriteSendDebugLog($"{context} alreadyInTargetGroup={found} via FindFirst(Button name=\"{groupName}\")");
                return found;
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"{context} IsAlreadyInTargetGroup threw: {ex.Message}");
                return false;
            }
        }

        static private IUIAutomationElement TryFindSendButton(IUIAutomationElement q, string context)
        {
            if (q == null)
                return null;
            try
            {
                var btn = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "发送"));
                WriteSendDebugLog($"{context} prefind send button: {ElementForLog(btn)}");
                return btn;
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"{context} prefind send button threw: {ex.Message}");
                return null;
            }
        }

        static private bool TryInvokeCachedSendButton(IUIAutomationElement cachedButton, int maxWaitTime, string context)
        {
            if (cachedButton == null)
                return false;

            int waitInterval = 10;
            DateTime startTime = DateTime.Now;
            int waitedTime = 0;
            int lastDetailedLogTime = -1000;

            while (waitedTime < maxWaitTime)
            {
                bool shouldLog = waitedTime == 0 || waitedTime - lastDetailedLogTime >= 500;
                bool enabled;
                try
                {
                    enabled = cachedButton.CurrentIsEnabled != 0;
                }
                catch (Exception ex)
                {
                    WriteSendDebugLog($"{context} cached send button stale: {ex.Message}, waited={waitedTime}ms");
                    return false;
                }
                if (shouldLog)
                {
                    lastDetailedLogTime = waitedTime;
                    WriteSendDebugLog($"{context} cached send enabled read: waited={waitedTime}ms, enabled={enabled}");
                }
                if (enabled)
                {
                    try
                    {
                        var sp = cachedButton.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                        if (sp != null)
                        {
                            WriteSendDebugLog($"{context} cached send invoke begin: waited={waitedTime}ms");
                            sp.Invoke();
                            WriteSendDebugLog($"{context} cached send invoke done: waited={waitedTime}ms");
                            return true;
                        }
                        WriteSendDebugLog($"{context} cached send invoke pattern missing: waited={waitedTime}ms");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        WriteSendDebugLog($"{context} cached send invoke threw: {ex.Message}, waited={waitedTime}ms");
                        return false;
                    }
                }

                Win32.Delay(waitInterval);
                waitedTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            }

            WriteSendDebugLog($"{context} cached send timeout: waited={waitedTime}ms");
            return false;
        }

        static private bool SendMessage(IUIAutomationElement q, bool useEnterIfDisabled = false, int maxWaitTime = 5000)
        {
            // System.Text.StringBuilder sendLog = new System.Text.StringBuilder();
            // sendLog.AppendLine($"[SendMessage] 开始查找发送按钮...");
            WriteSendDebugLog($"SendMessage begin: useEnterIfDisabled={useEnterIfDisabled}, maxWaitTime={maxWaitTime}");

            // 等待发送按钮启用（每隔10ms检测一次，最多等待5秒）
            int waitInterval = 10;
            DateTime startTime = DateTime.Now;
            int waitedTime = 0;
            int lastDetailedLogTime = -1000;
            IUIAutomationElement sendButton = null;

            while (waitedTime < maxWaitTime)
            {
                bool shouldLog = waitedTime == 0 || waitedTime - lastDetailedLogTime >= 500;
                if (shouldLog)
                {
                    lastDetailedLogTime = waitedTime;
                    WriteSendDebugLog($"SendMessage FindFirst begin: waited={waitedTime}ms");
                }
                sendButton = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "发送"));
                if (shouldLog)
                    WriteSendDebugLog($"SendMessage FindFirst done: waited={waitedTime}ms, button={ElementForLog(sendButton)}");
                if (sendButton != null)
                {
                    if (shouldLog)
                        WriteSendDebugLog($"SendMessage enabled read begin: waited={waitedTime}ms");
                    bool sendButtonEnabled = sendButton.CurrentIsEnabled != 0;
                    if (shouldLog || sendButtonEnabled)
                        WriteSendDebugLog($"SendMessage enabled read done: waited={waitedTime}ms, enabled={sendButtonEnabled}");
                    // sendLog.AppendLine($"[SendMessage] 找到发送按钮 (Enabled={sendButtonEnabled}, Waited={waitedTime}ms)");
                    if (sendButtonEnabled)
                    {
                        // 按钮已启用，点击发送
                        WriteSendDebugLog($"SendMessage invoke pattern begin: waited={waitedTime}ms");
                        var sp = sendButton.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                        WriteSendDebugLog($"SendMessage invoke pattern done: waited={waitedTime}ms, exists={sp != null}");
                        if (sp != null)
                        {
                            // sendLog.AppendLine($"[SendMessage] 点击发送按钮成功");
                            // SaveDebugInfo(sendLog.ToString(), "QQ发送");
                            WriteSendDebugLog($"SendMessage invoke begin: waited={waitedTime}ms");
                            sp.Invoke();
                            WriteSendDebugLog($"SendMessage invoke done: waited={waitedTime}ms");
                            return true;  // 发送成功，直接返回
                        }
                    }
                }

                // 按钮未找到或未启用，等待后重试
                Win32.Delay(waitInterval);
                waitedTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            }

            // sendLog.AppendLine($"[SendMessage] 等待超时({maxWaitTime}ms)，降级处理");
            // SaveDebugInfo(sendLog.ToString(), "QQ发送");
            WriteSendDebugLog($"SendMessage timeout: waited={waitedTime}ms, useEnterIfDisabled={useEnterIfDisabled}");

            // 如果等待后仍未成功，降级使用回车键
            if (useEnterIfDisabled)
            {
                // sendLog.AppendLine($"[SendMessage] 使用回车键发送");
                // SaveDebugInfo(sendLog.ToString(), "QQ发送");
                WriteSendDebugLog("SendMessage Enter begin.");
                Win32.Enter();
                WriteSendDebugLog("SendMessage Enter done.");
            }
            WriteSendDebugLog("SendMessage end without invoke.");
            return false;
        }

        static private bool WaitForSendButtonDisabled(IUIAutomationElement q, string context, IUIAutomationElement cachedSendButton = null, int maxWaitTime = 1500)
        {
            DateTime startTime = DateTime.Now;
            int waitedTime = 0;
            int lastDetailedLogTime = -1000;
            IUIAutomationElement sendButton = cachedSendButton;
            bool usingCached = sendButton != null;

            while (waitedTime < maxWaitTime)
            {
                bool shouldLog = waitedTime == 0 || waitedTime - lastDetailedLogTime >= 300;

                if (!usingCached)
                {
                    if (shouldLog)
                    {
                        lastDetailedLogTime = waitedTime;
                        WriteSendDebugLog($"{context} wait send disabled FindFirst begin: waited={waitedTime}ms");
                    }
                    sendButton = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "发送"));
                    if (shouldLog)
                        WriteSendDebugLog($"{context} wait send disabled FindFirst done: waited={waitedTime}ms, button={ElementForLog(sendButton)}");
                }

                if (sendButton == null)
                {
                    WriteSendDebugLog($"{context} wait send disabled result: button missing, waited={waitedTime}ms");
                    return true;
                }

                bool enabled;
                try
                {
                    enabled = sendButton.CurrentIsEnabled != 0;
                }
                catch (Exception ex)
                {
                    WriteSendDebugLog($"{context} wait send disabled cached button stale: {ex.Message}, waited={waitedTime}ms");
                    usingCached = false;
                    sendButton = null;
                    Win32.Delay(10);
                    waitedTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
                    continue;
                }

                if (shouldLog && usingCached)
                {
                    lastDetailedLogTime = waitedTime;
                    WriteSendDebugLog($"{context} wait send disabled cached read: waited={waitedTime}ms, enabled={enabled}");
                }

                if (!enabled)
                {
                    WriteSendDebugLog($"{context} wait send disabled result: disabled=True, waited={waitedTime}ms");
                    return true;
                }

                Win32.Delay(10);
                waitedTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            }

            WriteSendDebugLog($"{context} wait send disabled timeout: waited={waitedTime}ms");
            return false;
        }

        static private bool PasteAndSendMessage(IUIAutomationElement q, IUIAutomationElement groupList, IUIAutomationElement input, string groupName, string msgContent, string context, ref IUIAutomationElement cachedSendButton)
        {
            IntPtr qHwnd = GetQQNativeWindowHandle(q);
            if (cachedSendButton == null)
                cachedSendButton = TryFindSendButton(q, $"{context} prepaste");

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                int sendWaitTime = attempt == 1 ? 1200 : 5000;
                WriteSendDebugLog($"{context} paste/send attempt {attempt} begin: sendWaitTime={sendWaitTime}, {ContentForLog("msg", msgContent)}");

                input.SetFocus();
                WriteSendDebugLog($"{context} attempt {attempt} input focus set.");
                bool focusSafeAfterSetFocus = IsFocusedElementSafeForTargetConversation(q, groupList, groupName, $"{context} attempt {attempt} after SetFocus");
                if (!focusSafeAfterSetFocus)
                {
                    WriteSendDebugLog($"{context} attempt {attempt} focus is not in target conversation after SetFocus; recovering target group before paste.");
                    TryReactivateTargetGroup(q, groupList, groupName, $"{context} attempt {attempt}");
                    var refreshedInput = FindBottomVisibleDocument(q, $"{context} attempt {attempt} after target recovery");
                    if (refreshedInput != null)
                        input = refreshedInput;

                    if (attempt < 2)
                        continue;

                    return false;
                }

                bool foregroundOk = TryEnsureQQForeground(qHwnd, $"{context} attempt {attempt} pre-paste");
                if (!foregroundOk)
                {
                    WriteSendDebugLog($"{context} attempt {attempt} QQ is not foreground before paste; re-activating QQ and recovering target group.");
                    ActivateQQWindow(q, $"{context} attempt {attempt} foreground re-activate");
                    foregroundOk = TryEnsureQQForeground(qHwnd, $"{context} attempt {attempt} after re-activate");
                    if (!foregroundOk)
                    {
                        WriteSendDebugLog($"{context} attempt {attempt} QQ still not foreground after re-activate; skip paste.");
                        if (attempt < 2)
                        {
                            TryReactivateTargetGroup(q, groupList, groupName, $"{context} attempt {attempt}");
                            var refreshedInput = FindBottomVisibleDocument(q, $"{context} attempt {attempt} after foreground recovery");
                            if (refreshedInput != null)
                                input = refreshedInput;
                            continue;
                        }
                        return false;
                    }

                    input.SetFocus();
                    WriteSendDebugLog($"{context} attempt {attempt} input focus re-set after foreground recovery.");
                    if (!IsFocusedElementSafeForTargetConversation(q, groupList, groupName, $"{context} attempt {attempt} after SetFocus retry"))
                    {
                        WriteSendDebugLog($"{context} attempt {attempt} focus still not safe after foreground recovery; skip paste.");
                        if (attempt < 2)
                        {
                            TryReactivateTargetGroup(q, groupList, groupName, $"{context} attempt {attempt}");
                            var refreshedInput = FindBottomVisibleDocument(q, $"{context} attempt {attempt} after foreground recovery focus retry");
                            if (refreshedInput != null)
                                input = refreshedInput;
                            continue;
                        }
                        return false;
                    }
                }
                else
                {
                    WriteSendDebugLog($"{context} attempt {attempt} skipping physical click: SetFocus safe and QQ already foreground.");
                }

                Win32.Win32SetText(msgContent);
                WriteSendDebugLog($"{context} attempt {attempt} copied to clipboard: {ContentForLog("msg", msgContent)}");

                Win32.CtrlV();
                WriteSendDebugLog($"{context} attempt {attempt} CtrlV done.");
                if (!IsFocusedElementSafeForTargetConversation(q, groupList, groupName, $"{context} attempt {attempt} after CtrlV"))
                {
                    WriteSendDebugLog($"{context} attempt {attempt} focus is not in target conversation after CtrlV; skip send.");
                    if (attempt < 2)
                    {
                        TryReactivateTargetGroup(q, groupList, groupName, $"{context} attempt {attempt}");
                        var refreshedInput = FindBottomVisibleDocument(q, $"{context} attempt {attempt} after target recovery");
                        if (refreshedInput != null)
                            input = refreshedInput;
                        cachedSendButton = TryFindSendButton(q, $"{context} attempt {attempt} refind after CtrlV recovery");
                        continue;
                    }

                    return false;
                }

                bool sent = TryInvokeCachedSendButton(cachedSendButton, sendWaitTime, $"{context} attempt {attempt}");
                if (!sent)
                {
                    if (cachedSendButton != null)
                        WriteSendDebugLog($"{context} attempt {attempt} cached send button failed; falling back to SendMessage.");
                    sent = SendMessage(q, false, sendWaitTime);
                }
                WriteSendDebugLog($"{context} paste/send attempt {attempt} result: sent={sent}");
                if (sent)
                    return true;

                cachedSendButton = TryFindSendButton(q, $"{context} attempt {attempt} refind after send failure");

                if (attempt < 2)
                    WriteSendDebugLog($"{context} first paste did not enable/send QQ message; retrying input activation once.");
            }

            return false;
        }

        // ================================================

        static public List<string> GetQunList()
        {
            try
            {
                QunList.Clear();
                LastDebugInfo = "";

                string MainTitle = "QQ";
                var q = root.GetRootElement().FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, MainTitle));

                if (q == null)
                {
                    return QunList;
                }

                var grouplist = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "会话列表"));

                if (grouplist == null)
                {
                    return QunList;
                }

                // 获取会话列表的所有子元素
                var allChildren = grouplist.FindAll(TreeScope.TreeScope_Children, root.CreateTrueCondition());

                // 详细的诊断信息
                System.Text.StringBuilder debugInfo = new System.Text.StringBuilder();
                debugInfo.AppendLine($"========== QQ群列表诊断 ==========");
                debugInfo.AppendLine($"会话列表子元素数量: {allChildren.Length}");
                debugInfo.AppendLine($"======================================");

                if (allChildren.Length > 0)
                {
                    // 只分析前3个群，避免日志太长
                    int analyzeCount = Math.Min(3, allChildren.Length);
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        var elem = allChildren.GetElement(i);
                        string name = elem.CurrentName;

                        // 如果顶层元素Name为空，查找它的子元素
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            // 查找这个Group下的所有后代元素
                            var descendants = elem.FindAll(TreeScope.TreeScope_Descendants, root.CreateTrueCondition());

                            debugInfo.AppendLine($"\n--- 群[{i}] 顶层Name为空，后代元素{descendants?.Length ?? 0}个 ---");

                             if (descendants != null && descendants.Length > 0)
                            {
                                // 提取群名：从第一个元素开始拼接，遇到时间就停止
                                System.Text.StringBuilder groupNameBuilder = new System.Text.StringBuilder();

                                for (int j = 0; j < descendants.Length; j++)
                                {
                                    var desc = descendants.GetElement(j);
                                    string descName = desc.CurrentName;
                                    int descControlType = desc.CurrentControlType;

                                    if (!string.IsNullOrWhiteSpace(descName) && i < analyzeCount)
                                    {
                                        debugInfo.AppendLine($"  [{j}] Name=\"{descName}\"");
                                        debugInfo.AppendLine($"      ControlType={GetControlTypeName(descControlType)} ({descControlType})");
                                        debugInfo.AppendLine($"      IsTimeMarker={IsTimeMarker(descName)}");
                                    }

                                    if (string.IsNullOrWhiteSpace(descName))
                                        continue;

                                    // 检查是否是时间标记（停止条件）
                                    // 时间格式：包含":"、"昨天"、"星期"、"今天"等
                                    if (IsTimeMarker(descName))
                                    {
                                        debugInfo.AppendLine($"  >>> 遇到时间标记，停止拼接 <<<");
                                        break; // 遇到时间就停止
                                    }

                                    // 只收集Text类型的元素
                                    if (descControlType == UIA_ControlTypeIds.UIA_TextControlTypeId)
                                    {
                                        groupNameBuilder.Append(descName);
                                    }
                                }

                                string title = groupNameBuilder.ToString().Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');

                                if (!string.IsNullOrWhiteSpace(title))
                                {
                                    debugInfo.AppendLine($"  >>> 最终提取群名: \"{title}\" <<<");
                                    QunList.Add(title);
                                }
                            }
                        }
                        else
                        {
                            // 顶层元素有Name，需要清理消息内容
                            string title = name.Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');

                            if (i < analyzeCount)
                            {
                                debugInfo.AppendLine($"\n--- 群[{i}] 顶层Name不为空 ---");
                                debugInfo.AppendLine($"  原始Name: \"{name}\"");
                            }

                            // 清理消息内容：检测时间标记和消息前缀
                            // 格式如： "群名 22:01 某某：消息内容" 或 "群名 22:01 消息内容"

                            // 检测时间格式：空格+HH:MM 或 空格+H:MM（如 " 22:01"、" 9:30"）
                            int timeIndex = -1;
                            for (int j = 1; j < title.Length - 3; j++)  // 从1开始，确保可以检查j-1
                            {
                                // 检测" 数字:数字"模式（注意：数字前必须是空格）
                                if (title[j - 1] == ' ' && char.IsDigit(title[j]) && title[j + 1] == ':')
                                {
                                    // 检查冒号后面是否有数字
                                    if (j + 2 < title.Length && char.IsDigit(title[j + 2]))
                                    {
                                        timeIndex = j;
                                        break;
                                    }
                                }
                            }

                            // 如果找到时间标记，截取时间之前的部分（包括空格）
                            if (timeIndex > 0)
                            {
                                string cleaned = title.Substring(0, timeIndex).Trim();
                                if (i < analyzeCount)
                                {
                                    debugInfo.AppendLine($"  检测到时间标记在位置{timeIndex}，截取前: \"{cleaned}\"");
                                }
                                title = cleaned;
                            }

                            if (i < analyzeCount)
                            {
                                debugInfo.AppendLine($"  提取后: \"{title}\"");
                            }

                            if (!string.IsNullOrWhiteSpace(title))
                            {
                                QunList.Add(title);
                            }
                        }
                    }
                }

                debugInfo.AppendLine($"\n======================================");
                debugInfo.AppendLine($"最终提取到 {QunList.Count} 个群:");
                for (int k = 0; k < QunList.Count; k++)
                {
                    debugInfo.AppendLine($"  [{k}] \"{QunList[k]}\"");
                }
                debugInfo.AppendLine($"======================================");

                // 保存诊断信息到文件（已关闭）
                // SaveDebugInfo(debugInfo.ToString());
                LastDebugInfo = debugInfo.ToString();

                return QunList;
            }
            catch (Exception ex)
            {
                LastDebugInfo = $"获取群列表出错:\n{ex.Message}\n\n{ex.StackTrace}";
            }

            return QunList;
        }

        static Timer tmSend;

        public static void SendQQMessage (string groupName, string msgContent, int delayTime, Window caller)
        {
            WriteSendDebugLog($"SendQQMessage schedule: group=\"{TrimForLog(groupName, 80)}\", delayArg={delayTime}, effectiveDelay=0, {ContentForLog("msg", msgContent)}");


            if (msgContent == "" || groupName == "")
            {
                WriteSendDebugLog("SendQQMessage return: empty message or group.");
                return;
            }

            BeginSendAutomation("SendQQMessage schedule");
            bool timerScheduled = false;
            try
            {
                MsgRequest m = new MsgRequest(groupName, msgContent, caller);

                tmSend = new Timer(SendQQMessageHelper, m, 0, Timeout.Infinite);
                timerScheduled = true;
            }
            catch (Exception ex)
            {
                WriteSendDebugLog($"SendQQMessage schedule exception swallowed: {ex.Message}");

             
            }
            finally
            {
                if (!timerScheduled)
                    EndSendAutomation(caller, "SendQQMessage schedule failed", false);
            }


        }

        private static void SendQQMessageHelper(object obj)
        {
            System.Text.StringBuilder debugLog = new System.Text.StringBuilder();
            MsgRequest m = (MsgRequest)obj;
            string groupName = m.groupName;
            string msgContent = m.msgContent;
            Window caller = m.caller;
            bool focusCallerAfterAutomation = false;
            WriteSendDebugLog($"SendQQMessageHelper queued: group=\"{TrimForLog(groupName, 80)}\", {ContentForLog("msg", msgContent)}");

            WriteSendDebugLog("SendQQMessageHelper waiting for send automation lock.");
            try
            {
                lock (SendAutomationLock)
                {
                    WriteSendDebugLog("SendQQMessageHelper acquired send automation lock.");
                    try
                    {
                        // 保存当前鼠标位置
                        Win32.SaveCursorPos();
                        WriteSendDebugLog($"SendQQMessageHelper start: group=\"{TrimForLog(groupName, 80)}\", {ContentForLog("msg", msgContent)}");

                // debugLog.AppendLine($"========== QQ消息发送开始 ==========");
                // debugLog.AppendLine($"目标群名: [{groupName}]");
                // debugLog.AppendLine($"消息内容: [{msgContent}]");
                // debugLog.AppendLine($"======================================");





                // debugLog.AppendLine($"--- 开始查找QQ主窗口 ---");
                    var q = FindQQWindowWithRetry("SendQQMessageHelper");
                    WriteSendDebugLog($"SendQQMessageHelper QQ window: {ElementForLog(q)}");
                    if (q == null)
                    {
                        // debugLog.AppendLine($"[错误] 未找到QQ主窗口");
                        // ShowDebugLog(debugLog.ToString());
                        WriteSendDebugLog("SendQQMessageHelper return: QQ window not found.");
                        return;
                    }
                    // debugLog.AppendLine($"[成功] 找到QQ主窗口 (ClassName={q.CurrentClassName})");

                    ActivateQQWindow(q, "SendQQMessageHelper");


                    //获取消息列表，群列表
                    // debugLog.AppendLine($"--- [会话列表] 开始查找 ---");
                    var grouplist = FindGroupListWithRetry(q, "SendQQMessageHelper");

                    if (grouplist == null)
                    {
                        // debugLog.AppendLine($"[错误] 未找到会话列表");
                        // ShowDebugLog(debugLog.ToString());
                        WriteSendDebugLog("SendQQMessageHelper return: group list not found.");
                        return;
                    }
                    // debugLog.AppendLine($"[成功] 找到会话列表");

                    // debugLog.AppendLine($"--- [步骤1] 检测是否已在目标群 ---");
                    // SaveDebugInfo(debugLog.ToString(), "QQ发送");

                    // 优化：先检查是否已在目标群（提前检测）
                    IUIAutomationElement edits = null;
                    bool alreadyInTargetGroup = IsAlreadyInTargetGroup(q, groupName, "SendQQMessageHelper");
                    // SaveDebugInfo(debugLog.ToString(), "QQ发送");

                    // 如果已在目标群，直接查找输入框，跳过点击群
                    if (alreadyInTargetGroup)
                    {
                        // 策略1：优先查找Edit控件（旧版QQ）
                        var allEdits = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                        if (allEdits != null && allEdits.Length > 0)
                        {
                            for (int i = 0; i < allEdits.Length; i++)
                            {
                                var edit = allEdits.GetElement(i);
                                string editName = edit.CurrentName;
                                // 跳过搜索框
                                if (!string.IsNullOrWhiteSpace(editName) && editName.Contains("搜索"))
                                {
                                    continue;
                                }
                                edits = edit;
                                break;
                            }
                        }

                        // 策略2：如果没找到Edit，查找Document控件（新版QQ）
                        if (edits == null)
                        {
                            edits = FindBottomVisibleDocument(q, "SendQQMessageHelper already-in-group");
                        }
                    }
                    else
                    {
                        // 不在目标群，使用前缀匹配查找输入框（兼容旧逻辑）
                        var allEdits = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                        if (allEdits != null && allEdits.Length > 0)
                        {
                            for (int i = 0; i < allEdits.Length; i++)
                            {
                                var edit = allEdits.GetElement(i);
                                string editName = edit.CurrentName;

                                // 使用前缀匹配查找输入框
                                if (!string.IsNullOrWhiteSpace(editName) && editName.StartsWith(groupName))
                                {
                                    edits = edit;
                                    alreadyInTargetGroup = true;
                                    break;
                                }
                            }
                        }
                    }

                    // 如果找到输入框，直接使用，跳过后续所有查找
                    if (edits != null)
                    {
                        // debugLog.AppendLine($"[快速路径] 已找到输入框，跳过群查找 (alreadyInTargetGroup={alreadyInTargetGroup})");
                        // SaveDebugInfo(debugLog.ToString(), "QQ发送");
                    }
                    else if (alreadyInTargetGroup)
                    {
                        // debugLog.AppendLine($"[错误] 已在目标群但找不到输入框!");
                        // SaveDebugInfo(debugLog.ToString(), "QQ发送");
                    }

                    // 第二步：如果没找到输入框且不在目标群，去会话列表点击群
                    if (edits == null && !alreadyInTargetGroup)
                    {
                        // debugLog.AppendLine($"--- [步骤2] 不在目标群，去会话列表查找并点击群 ---");
                        // SaveDebugInfo(debugLog.ToString(), "QQ发送");

                        // 优化：检查缓存是否有效
                        IUIAutomationElement cachedGroupElem = null;
                        bool cacheValid = (DateTime.Now - QunCacheTime) < QunCacheExpiry && QunElementCache.TryGetValue(groupName, out cachedGroupElem);

                        if (cacheValid && cachedGroupElem != null)
                        {
                            // debugLog.AppendLine($"[缓存命中] 使用缓存的群元素");
                            // 直接使用缓存的元素点击
                            var sp = cachedGroupElem.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                            if (sp != null)
                            {
                                sp.Invoke();

                                // 快速查找输入框。不要按群名找元素，那会命中左侧会话/标题 Text。
                                edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                if (edits == null)
                                {
                                    edits = FindBottomVisibleDocument(q, "SendQQMessageHelper cached-group-click");
                                }
                            }
                        }

                        // 缓存未命中，使用原始逻辑
                        if (edits == null)
                        {
                            // 获取会话列表的所有子元素（和GetQunList逻辑一致）
                            var allChildren = grouplist.FindAll(TreeScope.TreeScope_Children, root.CreateTrueCondition());
                            // debugLog.AppendLine($"[会话列表] 找到 {allChildren.Length} 个子元素");

                            if (allChildren.Length > 0)
                            {
                                for (int i = 0; i < allChildren.Length; i++)
                                {
                                    var elem = allChildren.GetElement(i);
                                    string itemName = elem.CurrentName;

                                    // 优化：先尝试简单匹配，避免复杂的群名提取
                                    bool quickMatch = false;
                                    if (!string.IsNullOrWhiteSpace(itemName))
                                    {
                                        // 快速匹配：顶层Name直接匹配（清理消息内容后）
                                        string quickName = itemName.Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');
                                        int timeIndex = quickName.IndexOf(' ');
                                        if (timeIndex > 0)
                                        {
                                            quickName = quickName.Substring(0, timeIndex);
                                        }

                                        if (quickName == groupName || quickName.StartsWith(groupName))
                                        {
                                            quickMatch = true;
                                        }
                                    }

                                    // 如果顶层元素Name为空，查找它的子元素（和GetQunList逻辑一致）
                                    string extractedName = "";
                                    if (!quickMatch && string.IsNullOrWhiteSpace(itemName))
                                    {
                                        var descendants = elem.FindAll(TreeScope.TreeScope_Descendants, root.CreateTrueCondition());
                                        if (descendants != null && descendants.Length > 0)
                                        {
                                            System.Text.StringBuilder nameBuilder = new System.Text.StringBuilder();

                                            for (int j = 0; j < descendants.Length; j++)
                                            {
                                                var desc = descendants.GetElement(j);
                                                string descName = desc.CurrentName;
                                                int descControlType = desc.CurrentControlType;

                                                if (string.IsNullOrWhiteSpace(descName))
                                                    continue;

                                                if (IsTimeMarker(descName))
                                                    break;

                                                if (descControlType == UIA_ControlTypeIds.UIA_TextControlTypeId)
                                                {
                                                    nameBuilder.Append(descName);
                                                }
                                            }

                                            extractedName = nameBuilder.ToString().Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');
                                        }
                                    }
                                    else if (!quickMatch)
                                    {
                                        extractedName = itemName.Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');

                                        int timeIndex = -1;
                                        for (int j = 1; j < extractedName.Length - 3; j++)
                                        {
                                            if (extractedName[j - 1] == ' ' && char.IsDigit(extractedName[j]) && extractedName[j + 1] == ':')
                                            {
                                                if (j + 2 < extractedName.Length && char.IsDigit(extractedName[j + 2]))
                                                {
                                                    timeIndex = j;
                                                    break;
                                                }
                                            }
                                        }

                                        if (timeIndex > 0)
                                        {
                                            extractedName = extractedName.Substring(0, timeIndex).Trim();
                                        }
                                    }

                                    // 使用快速匹配或提取的群名
                                    string targetName = quickMatch ? itemName : extractedName;

                                    // 智能匹配
                                    bool isMatch = false;
                                    if (!string.IsNullOrWhiteSpace(targetName))
                                    {
                                        if (targetName == groupName || targetName.StartsWith(groupName) || targetName.Contains(groupName))
                                        {
                                            isMatch = true;
                                        }
                                    }

                                    if (!isMatch)
                                        continue;

                                    // 找到匹配的群，准备点击
                                    var sp = elem.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                                    if (sp != null)
                                    {
                                        // 检查当前是否已在目标群（重新获取allEdits检查）
                                        bool currentlyInGroup = false;
                                        var checkEdits = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                        if (checkEdits != null && checkEdits.Length > 0)
                                        {
                                            for (int ei = 0; ei < checkEdits.Length; ei++)
                                            {
                                                var edit = checkEdits.GetElement(ei);
                                                string editName = edit.CurrentName;
                                                if (!string.IsNullOrWhiteSpace(editName) && editName.StartsWith(groupName))
                                                {
                                                    currentlyInGroup = true;
                                                    break;
                                                }
                                            }
                                        }

                                        if (!currentlyInGroup)
                                        {
                                            sp.Invoke();

                                            // 缓存这个群元素
                                            if ((DateTime.Now - QunCacheTime) >= QunCacheExpiry)
                                            {
                                                QunElementCache.Clear();
                                                QunCacheTime = DateTime.Now;
                                            }
                                            QunElementCache[groupName] = elem;
                                        }

                                        // 快速查找输入框
                                        edits = null;
                                        var allEditsAfterClick = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                        if (allEditsAfterClick != null && allEditsAfterClick.Length > 0)
                                        {
                                            for (int ei = 0; ei < allEditsAfterClick.Length; ei++)
                                            {
                                                var eedit = allEditsAfterClick.GetElement(ei);
                                                string eName = eedit.CurrentName;

                                                if (!string.IsNullOrWhiteSpace(eName) && eName.Contains("搜索"))
                                                {
                                                    continue;
                                                }

                                                edits = eedit;
                                                break;
                                            }
                                        }

                                        if (edits == null)
                                        {
                                            edits = FindBottomVisibleDocument(q, "SendQQMessageHelper after-group-click");
                                        }

                                        if (edits == null)
                                        {
                                            edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                        }

                                        if (edits == null)
                                        {
                                            edits = FindBottomVisibleDocument(q, "SendQQMessageHelper fallback");
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (edits != null)
                    {
                        WriteSendDebugLog($"SendQQMessageHelper input found: {ElementForLog(edits)}");
                        // debugLog.AppendLine($"--- [步骤3] 找到输入框，开始发送 ---");
                        // debugLog.AppendLine($"[输入框类型] {GetControlTypeName(edits.CurrentControlType)} (Name=\"{edits.CurrentName}\")");
                        // SaveDebugInfo(debugLog.ToString(), "QQ发送");

                        // debugLog.AppendLine($"[发送] 激活输入框、粘贴并发送...");
                        // SaveDebugInfo(debugLog.ToString(), "QQ发送");
                        // SendQQMessage用于发送文章内容，始终发送
                        IUIAutomationElement cachedSendButton = null;
                        bool sent = PasteAndSendMessage(q, grouplist, edits, groupName, msgContent, "SendQQMessageHelper", ref cachedSendButton);
                        WriteSendDebugLog($"SendQQMessageHelper SendMessage final result: sent={sent}");
                        focusCallerAfterAutomation = sent;

                        // debugLog.AppendLine($"[完成] 切换回TypeSunny窗口");
                        // debugLog.AppendLine($"========== QQ消息发送成功 ==========");
                        // SaveDebugInfo(debugLog.ToString(), "QQ发送");

                    }
                    else
                    {
                        // debugLog.AppendLine($"[错误] 未找到输入框，发送失败");
                        // debugLog.AppendLine($"========== QQ消息发送失败 ==========");
                        // SaveDebugInfo(debugLog.ToString(), "QQ发送");
                        WriteSendDebugLog("SendQQMessageHelper failed: input not found.");
                    }
                }
                catch (Exception ex)
                {
                    // debugLog.AppendLine($"[异常] QQ消息发送出错: {ex.Message}");
                    // debugLog.AppendLine($"[异常] 堆栈: {ex.StackTrace}");
                    // debugLog.AppendLine($"========== QQ消息发送异常结束 ==========");
                    // ShowDebugLog(debugLog.ToString());
                    WriteSendDebugLog($"SendQQMessageHelper exception: {ex}");
                }
                finally
                {
                    // 无论成功或失败，都恢复鼠标位置
                    Win32.RestoreCursorPos();
                    WriteSendDebugLog("SendQQMessageHelper released send automation lock.");
                }
                }
            }
            finally
            {
                EndSendAutomation(caller, "SendQQMessageHelper", focusCallerAfterAutomation);
            }
        }


        public static void SendQQMessageD(string groupName, string msgContent1, string msgContent2, int delayTime, Window caller)
        {
            WriteSendDebugLog($"SendQQMessageD start: group=\"{TrimForLog(groupName, 80)}\", delayArg={delayTime}, {ContentForLog("score", msgContent1)}, {ContentForLog("article", msgContent2)}");

            if (msgContent1 == "" || msgContent2 == ""  || groupName == "")
            {
                WriteSendDebugLog("SendQQMessageD return: empty score/article/group.");
                return;
            }

            BeginSendAutomation("SendQQMessageD");
            bool focusCallerAfterAutomation = false;
            try
            {
                WriteSendDebugLog("SendQQMessageD waiting for send automation lock.");
                lock (SendAutomationLock)
                {
                    WriteSendDebugLog("SendQQMessageD acquired send automation lock.");
                    // 保存当前鼠标位置
                    Win32.SaveCursorPos();
                    WriteSendDebugLog("SendQQMessageD cursor saved.");

                    try
                    {

                // Win32.Delay(1);

                var q = FindQQWindowWithRetry("SendQQMessageD");
                WriteSendDebugLog($"SendQQMessageD QQ window: {ElementForLog(q)}");
                if (q == null)
                {
                    WriteSendDebugLog("SendQQMessageD return: QQ window not found.");
                    return;
                }

                ActivateQQWindow(q, "SendQQMessageD");

                var grouplist = FindGroupListWithRetry(q, "SendQQMessageD");

                if (grouplist == null)
                {
                    WriteSendDebugLog("SendQQMessageD return: group list not found.");
                    return;
                }

                    // 第一步：智能检测是否已在目标群（兼容新旧版本QQ）
                    IUIAutomationElement edits = null;
                    bool alreadyInTargetGroup = IsAlreadyInTargetGroup(q, groupName, "SendQQMessageD");

                    // 如果已在目标群，直接查找输入框，跳过点击群
                    if (alreadyInTargetGroup)
                    {
                        // 策略1：优先查找Edit控件（旧版QQ）
                        var allEdits = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                        if (allEdits != null && allEdits.Length > 0)
                        {
                            for (int i = 0; i < allEdits.Length; i++)
                            {
                                var edit = allEdits.GetElement(i);
                                string editName = edit.CurrentName;
                                // 跳过搜索框
                                if (!string.IsNullOrWhiteSpace(editName) && editName.Contains("搜索"))
                                {
                                    continue;
                                }
                                edits = edit;
                                WriteSendDebugLog($"SendQQMessageD input found while already in group via Edit[{i}]: {ElementForLog(edits)}");
                                break;
                            }
                        }

                        // 策略2：如果没找到Edit，查找Document控件（新版QQ）
                        if (edits == null)
                        {
                            edits = FindBottomVisibleDocument(q, "SendQQMessageD already-in-group");
                            WriteSendDebugLog($"SendQQMessageD input found while already in group via bottom Document: {ElementForLog(edits)}");
                        }
                    }
                    else
                    {
                        // 不在目标群，使用前缀匹配查找输入框（兼容旧逻辑）
                        var allEdits = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                        WriteSendDebugLog($"SendQQMessageD not in target group; editCountBeforeClick={allEdits?.Length ?? 0}");
                        if (allEdits != null && allEdits.Length > 0)
                        {
                            for (int i = 0; i < allEdits.Length; i++)
                            {
                                var edit = allEdits.GetElement(i);
                                string editName = edit.CurrentName;

                                // 使用前缀匹配查找输入框
                                if (!string.IsNullOrWhiteSpace(editName) && editName.StartsWith(groupName))
                                {
                                    edits = edit;
                                    alreadyInTargetGroup = true;
                                    WriteSendDebugLog($"SendQQMessageD input matched by Edit name prefix[{i}]: {ElementForLog(edits)}");
                                    break;
                                }
                            }
                        }
                    }

                    // 第二步：如果没找到输入框，去会话列表点击群
                    if (edits == null)
                    {
                        // 获取会话列表的所有子元素（和GetQunList逻辑一致）
                        var allChildren = grouplist.FindAll(TreeScope.TreeScope_Children, root.CreateTrueCondition());
                        WriteSendDebugLog($"SendQQMessageD scanning group list children: count={allChildren?.Length ?? 0}");

                        if (allChildren.Length > 0)
                        {
                            for (int i = 0; i < allChildren.Length; i++)
                            {
                                var elem = allChildren.GetElement(i);
                                string itemName = elem.CurrentName;

                                // 如果顶层元素Name为空，查找它的子元素（和GetQunList逻辑一致）
                                string extractedName = "";
                                if (string.IsNullOrWhiteSpace(itemName))
                                {
                                    var descendants = elem.FindAll(TreeScope.TreeScope_Descendants, root.CreateTrueCondition());
                                    if (descendants != null && descendants.Length > 0)
                                    {
                                        // 提取群名：从第一个元素开始拼接，遇到时间就停止
                                        System.Text.StringBuilder nameBuilder = new System.Text.StringBuilder();

                                        for (int j = 0; j < descendants.Length; j++)
                                        {
                                            var desc = descendants.GetElement(j);
                                            string descName = desc.CurrentName;
                                            int descControlType = desc.CurrentControlType;

                                            if (string.IsNullOrWhiteSpace(descName))
                                                continue;

                                            // 检查是否是时间标记（停止条件）
                                            if (IsTimeMarker(descName))
                                            {
                                                break;
                                            }

                                            // 只收集Text类型的元素
                                            if (descControlType == UIA_ControlTypeIds.UIA_TextControlTypeId)
                                            {
                                                nameBuilder.Append(descName);
                                            }
                                        }

                                        extractedName = nameBuilder.ToString().Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');
                                    }
                                }
                                else
                                {
                                    // 顶层元素有Name，需要清理消息内容（和GetQunList逻辑一致）
                                    extractedName = itemName.Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');

                                    // 清理消息内容：检测时间格式（如 " 22:01"）
                                    int timeIndex = -1;
                                    for (int j = 1; j < extractedName.Length - 3; j++)
                                    {
                                        // 检测" 数字:数字"模式（数字前必须是空格）
                                        if (extractedName[j - 1] == ' ' && char.IsDigit(extractedName[j]) && extractedName[j + 1] == ':')
                                        {
                                            // 检查冒号后面是否有数字
                                            if (j + 2 < extractedName.Length && char.IsDigit(extractedName[j + 2]))
                                            {
                                                timeIndex = j;
                                                break;
                                            }
                                        }
                                    }

                                    // 如果找到时间标记，截取时间之前的部分
                                    if (timeIndex > 0)
                                    {
                                        extractedName = extractedName.Substring(0, timeIndex).Trim();
                                    }
                                }

                                // 使用更智能的匹配：先尝试精确匹配，再尝试前缀匹配，最后尝试包含匹配
                                bool isMatch = false;
                                if (!string.IsNullOrWhiteSpace(extractedName))
                                {
                                    // 精确匹配
                                    if (extractedName == groupName)
                                    {
                                        isMatch = true;
                                    }
                                    // 前缀匹配（提取名称以群名开头）
                                    else if (extractedName.StartsWith(groupName))
                                    {
                                        isMatch = true;
                                    }
                                    // 包含匹配（提取名称包含群名，用于处理提取名称被污染的情况）
                                    else if (extractedName.Contains(groupName))
                                    {
                                        isMatch = true;
                                    }
                                }

                                if (!isMatch)
                                    continue;
                                WriteSendDebugLog($"SendQQMessageD matched group child[{i}]: raw=\"{TrimForLog(itemName, 120)}\", extracted=\"{TrimForLog(extractedName, 120)}\"");

                                var sp = elem.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                                if (sp != null)
                                {
                                    // 在点击群之前，先检测当前是否已经在目标群（优化：复用按钮检查结果）
                                    var allButtonsForCheck = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_ButtonControlTypeId));
                                    alreadyInTargetGroup = false;  // 重置状态
                                    if (allButtonsForCheck != null)
                                    {
                                        for (int bi = 0; bi < allButtonsForCheck.Length; bi++)
                                        {
                                            var btn = allButtonsForCheck.GetElement(bi);
                                            string btnName = btn.CurrentName;
                                            // 检查是否有按钮的Name等于目标群名
                                            if (!string.IsNullOrWhiteSpace(btnName) && btnName == groupName)
                                            {
                                                alreadyInTargetGroup = true;
                                                break;
                                            }
                                        }
                                    }

                                    // 如果已经在目标群，跳过点击
                                    if (!alreadyInTargetGroup)
                                    {
                                        WriteSendDebugLog("SendQQMessageD invoking matched group item.");
                                        sp.Invoke();
                                    }
                                    else
                                    {
                                        WriteSendDebugLog("SendQQMessageD matched group already active; skip invoking group item.");
                                    }

                                    // 点击后查找任意可编辑的输入框
                                    // 策略1：排除搜索框，找到Name不包含"搜索"的Edit控件
                                    var allEditsForD = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                    edits = null;

                                    if (allEditsForD != null && allEditsForD.Length > 0)
                                    {
                                        for (int ei = 0; ei < allEditsForD.Length; ei++)
                                        {
                                            var eedit = allEditsForD.GetElement(ei);
                                            string eName = eedit.CurrentName;

                                            // 跳过明显的搜索框
                                            if (!string.IsNullOrWhiteSpace(eName) && eName.Contains("搜索"))
                                            {
                                                continue;
                                            }

                                            // 使用第一个非搜索框的Edit控件
                                            edits = eedit;
                                            WriteSendDebugLog($"SendQQMessageD input found after group click via Edit[{ei}]: {ElementForLog(edits)}");
                                            break;
                                        }
                                    }

                                    // 策略2：如果没找到合适的Edit，尝试使用Document控件（新版QQ可能用Document作为输入区）
                                    if (edits == null)
                                    {
                                        edits = FindBottomVisibleDocument(q, "SendQQMessageD after-group-click");
                                        WriteSendDebugLog($"SendQQMessageD input found after group click via bottom Document: {ElementForLog(edits)}");
                                    }

                                    // 策略3：如果还是失败，降级到原来的逻辑（兼容旧版QQ）
                                    if (edits == null)
                                    {
                                        edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                        WriteSendDebugLog($"SendQQMessageD input fallback Edit: {ElementForLog(edits)}");
                                    }

                                    // 最后尝试：如果还是失败，尝试查找Document
                                    if (edits == null)
                                    {
                                        edits = FindBottomVisibleDocument(q, "SendQQMessageD fallback");
                                        WriteSendDebugLog($"SendQQMessageD input fallback Document: {ElementForLog(edits)}");
                                    }

                                    break;
                                }
                            }
                        }
                    }


                    if (edits != null)
                    {
                        WriteSendDebugLog($"SendQQMessageD final input: {ElementForLog(edits)}");

                        // 新版QQ的发送成绩逻辑
                        IUIAutomationElement cachedSendButton = null;
                        if (Config.GetBool("自动发送成绩"))
                        {
                            bool scoreSent = PasteAndSendMessage(q, grouplist, edits, groupName, msgContent1, "SendQQMessageD score", ref cachedSendButton);
                            WriteSendDebugLog($"SendQQMessageD score final result: sent={scoreSent}");
                            focusCallerAfterAutomation = focusCallerAfterAutomation || scoreSent;
                            if (scoreSent)
                                WaitForSendButtonDisabled(q, "SendQQMessageD score after send", cachedSendButton);
                        }
                        else
                        {
                            WriteSendDebugLog("SendQQMessageD score send skipped: 自动发送成绩=false.");
                        }

                        // 第二次：发送msgContent2（新文章）
                        if (!string.IsNullOrWhiteSpace(msgContent2))
                        {
                            bool articleSent = PasteAndSendMessage(q, grouplist, edits, groupName, msgContent2, "SendQQMessageD article", ref cachedSendButton);
                            WriteSendDebugLog($"SendQQMessageD article final result: sent={articleSent}");
                            focusCallerAfterAutomation = focusCallerAfterAutomation || articleSent;
                        }
                        else
                        {
                            WriteSendDebugLog("SendQQMessageD article send skipped: article is white-space.");
                        }

                    }
                    else
                    {
                        WriteSendDebugLog("SendQQMessageD failed: input not found.");
                    }
                }
                catch (Exception ex)
                {
                    WriteSendDebugLog($"SendQQMessageD exception: {ex}");

                }
                finally
                {
                    // 无论成功或失败，都恢复鼠标位置
                    Win32.RestoreCursorPos();
                    WriteSendDebugLog("SendQQMessageD finally cursor restored; releasing send automation lock.");
                }
                }
            }
            finally
            {
                EndSendAutomation(caller, "SendQQMessageD", focusCallerAfterAutomation);
            }
        }

    }
}
