using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Interop.UIAutomationClient;
using static TypeSunny.MainWindow;

namespace TypeSunny
{
    class MsgRequest
    {
        public string groupName = "";

        public  Window caller = null;

        public MsgRequest(string groupName, Window caller)
        {
            this.groupName = groupName;
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

        // 保存调试信息到文件
        private static void SaveDebugInfo(string info, string prefix = "")
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logFile = System.IO.Path.Combine(appDir, $"QQ群列表诊断_{timestamp}.txt");
                System.IO.File.WriteAllText(logFile, info, Encoding.UTF8);
            }
            catch { }
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
        static bool IsTimeMarker(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
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
        static private void ActivateDocumentInput(IUIAutomationElement document)
        {
            if (document.CurrentControlType == UIA_ControlTypeIds.UIA_DocumentControlTypeId)
            {
                var docRect = document.CurrentBoundingRectangle;
                int clickX = (int)((docRect.left + docRect.right) / 2);
                int clickY = (int)(docRect.bottom - 50);
                Win32.Click(clickX, clickY);
            }
        }

        /// <summary>
        /// 发送消息（等待发送按钮启用后点击）
        /// </summary>
        static private void SendMessage(IUIAutomationElement q, bool useEnterIfDisabled = false)
        {
            // 等待发送按钮启用（每隔10ms检测一次，最多等待2秒）
            int maxWaitTime = 2000;
            int waitInterval = 10;
            int waitedTime = 0;
            IUIAutomationElement sendButton = null;

            while (waitedTime < maxWaitTime)
            {
                sendButton = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "发送"));
                if (sendButton != null)
                {
                    bool sendButtonEnabled = sendButton.CurrentIsEnabled != 0;
                    if (sendButtonEnabled)
                    {
                        // 按钮已启用，点击发送
                        var sp = sendButton.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                        if (sp != null)
                        {
                            sp.Invoke();
                            return;  // 发送成功，直接返回
                        }
                    }
                }

                // 按钮未找到或未启用，等待后重试
                Win32.Delay(waitInterval);
                waitedTime += waitInterval;
            }

            // 如果等待后仍未成功，降级使用回车键
            if (useEnterIfDisabled)
            {
                Win32.Enter();
            }
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


            Win32SetText(msgContent);

            if (msgContent == "" || groupName == "")
                return;

            try
            {
                MsgRequest m = new MsgRequest(groupName, caller);

                tmSend = new Timer(SendQQMessageHelper, m, delayTime, Timeout.Infinite);
            }
            catch (Exception)
            {

             
            }


        }

        private static void SendQQMessageHelper(object obj)
        {
            System.Text.StringBuilder debugLog = new System.Text.StringBuilder();

            try
            {
                MsgRequest m = (MsgRequest)obj;
                string groupName = m.groupName;
                string msgContent = Win32.Win32GetText(13);
                Window caller = m.caller;

                // debugLog.AppendLine($"========== QQ消息发送开始 ==========");
                // debugLog.AppendLine($"目标群名: [{groupName}]");
                // debugLog.AppendLine($"消息内容: [{msgContent}]");
                // debugLog.AppendLine($"======================================");





                // debugLog.AppendLine($"--- 开始查找QQ主窗口 ---");
                    string MainTitle = "QQ";
                    var q = root.GetRootElement().FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, MainTitle));
                    if (q == null)
                    {
                        // debugLog.AppendLine($"[错误] 未找到QQ主窗口");
                        // ShowDebugLog(debugLog.ToString());
                        return;
                    }
                    // debugLog.AppendLine($"[成功] 找到QQ主窗口 (ClassName={q.CurrentClassName})");

                    if (null == q.FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId)))
                    {
                        // debugLog.AppendLine($"[激活] QQ窗口未激活，正在激活窗口...");
                        var wp = q.GetCurrentPattern(UIA_PatternIds.UIA_WindowPatternId) as IUIAutomationWindowPattern;
                        wp.SetWindowVisualState(WindowVisualState.WindowVisualState_Normal);
                        q.SetFocus();
                        // Win32.Delay(1);
                    }
                    else
                    {
                        // debugLog.AppendLine($"[激活] QQ窗口已激活");
                    }


                    //获取消息列表，群列表
                    // debugLog.AppendLine($"--- [会话列表] 开始查找 ---");
                    var grouplist = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "会话列表"));

                    if (grouplist == null)
                    {
                        // debugLog.AppendLine($"[错误] 未找到会话列表");
                        // ShowDebugLog(debugLog.ToString());
                        return;
                    }
                    // debugLog.AppendLine($"[成功] 找到会话列表");

                    // 优化：先检查是否已在目标群（提前检测）
                    IUIAutomationElement edits = null;
                    bool alreadyInTargetGroup = false;

                    // 策略0：优先检查是否已有输入框（最快路径）
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

                    // 如果找到输入框，直接使用，跳过后续所有查找
                    if (edits != null)
                    {
                        // debugLog.AppendLine($"[快速路径] 已找到目标群的输入框，跳过群查找");
                    }

                    // 第二步：如果没找到输入框，去会话列表点击群
                    if (edits == null)
                    {
                        // debugLog.AppendLine($"--- [点击群] 第二步：去会话列表查找并点击群 ---");

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

                                // 快速查找输入框
                                edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, groupName));
                                if (edits == null)
                                {
                                    edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
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
                                        // 检查当前是否已在目标群（优化：复用已有的allEdits）
                                        alreadyInTargetGroup = false;
                                        if (allEdits != null)
                                        {
                                            for (int ei = 0; ei < allEdits.Length; ei++)
                                            {
                                                var edit = allEdits.GetElement(ei);
                                                string editName = edit.CurrentName;
                                                if (!string.IsNullOrWhiteSpace(editName) && editName.StartsWith(groupName))
                                                {
                                                    alreadyInTargetGroup = true;
                                                    break;
                                                }
                                            }
                                        }

                                        if (!alreadyInTargetGroup)
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
                                            var allDocuments = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId));
                                            if (allDocuments != null && allDocuments.Length > 0)
                                            {
                                                for (int di = 0; di < allDocuments.Length; di++)
                                                {
                                                    var doc = allDocuments.GetElement(di);
                                                    bool isOffscreen = doc.CurrentIsOffscreen != 0;

                                                    if (isOffscreen)
                                                        continue;

                                                    edits = doc;
                                                    break;
                                                }
                                            }
                                        }

                                        if (edits == null)
                                        {
                                            edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                        }

                                        if (edits == null)
                                        {
                                            edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId));
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (edits != null)
                    {
                        // debugLog.AppendLine($"--- [发送消息] 找到输入框，开始发送 ---");
                        // debugLog.AppendLine($"[焦点] 设置输入框焦点...");
                        edits.SetFocus();
                        // Win32.Delay(50);  // 等待焦点切换到QQ输入框

                        // 如果使用的是Document控件，需要额外激活输入框
                        // 新版QQ的输入框在Document底部，需要点击Document才能激活
                        ActivateDocumentInput(edits);
                        // Win32.Delay(100);  // 等待输入框激活

                        // debugLog.AppendLine($"[粘贴] 执行 Ctrl+V 粘贴消息内容...");
                        Win32.CtrlV();

                        // Win32.Delay(10);  // 等待粘贴完成
                        // debugLog.AppendLine($"[粘贴] 粘贴完成");
                        if (Config.GetBool("自动发送成绩"))
                        {
                            // 使用优化后的SendMessage方法（内部有等待循环）
                            SendMessage(q);
                        }
                        else
                        {
                            // debugLog.AppendLine($"[发送] 自动发送未启用，跳过发送");
                        }

                        // Win32.Delay(50);  // 等待所有操作完成再切换焦点
                        // debugLog.AppendLine($"[完成] 切换回TypeSunny窗口");
                        caller.Dispatcher.Invoke(() => {
                            MainWindow.Current.FocusInput();
                        });
                        // debugLog.AppendLine($"========== QQ消息发送完成 ==========");
                        // ShowDebugLog(debugLog.ToString());

                    }
                    else
                    {
                        // debugLog.AppendLine($"[错误] 未找到输入框，发送失败");
                        // debugLog.AppendLine($"========== QQ消息发送失败 ==========");
                        // ShowDebugLog(debugLog.ToString());
                    }
            }
            catch (Exception ex)
            {
                // debugLog.AppendLine($"[异常] QQ消息发送出错: {ex.Message}");
                // debugLog.AppendLine($"[异常] 堆栈: {ex.StackTrace}");
                // debugLog.AppendLine($"========== QQ消息发送异常结束 ==========");
                // ShowDebugLog(debugLog.ToString());
            }


        }


        public static void SendQQMessageD(string groupName, string msgContent1, string msgContent2, int delayTime, Window caller)
        {

            Win32SetText(msgContent1);
            if (msgContent1 == "" || msgContent1 == ""  || groupName == "")
                return;


            try
            {

                // Win32.Delay(1);

                string MainTitle = "QQ";
                var q = root.GetRootElement().FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, MainTitle));
                if (q == null)
                    return;

                if (null == q.FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId)))
                {
                    var wp = q.GetCurrentPattern(UIA_PatternIds.UIA_WindowPatternId) as IUIAutomationWindowPattern;
                    wp.SetWindowVisualState(WindowVisualState.WindowVisualState_Normal);
                    q.SetFocus();
                    // Win32.Delay(1);

                }

                var grouplist = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "会话列表"));

                if (grouplist == null)
                {
                    return;
                }

                    // 第一步：查找已打开的输入框（使用前缀匹配，兼容特殊字符）
                    IUIAutomationElement edits = null;
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
                                break;
                            }
                        }
                    }

                    // 第二步：如果没找到输入框，去会话列表点击群
                    if (edits == null)
                    {
                        // 获取会话列表的所有子元素（和GetQunList逻辑一致）
                        var allChildren = grouplist.FindAll(TreeScope.TreeScope_Children, root.CreateTrueCondition());

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

                                var sp = elem.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                                if (sp != null)
                                {
                                    // 在点击群之前，先检测当前是否已经在目标群（优化：复用按钮检查结果）
                                    var allButtonsForCheck = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_ButtonControlTypeId));
                                    bool alreadyInTargetGroup = false;
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
                                        sp.Invoke();
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
                                            break;
                                        }
                                    }

                                    // 策略2：如果没找到合适的Edit，尝试使用Document控件（新版QQ可能用Document作为输入区）
                                    if (edits == null)
                                    {
                                        var allDocumentsForD = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId));
                                        if (allDocumentsForD != null && allDocumentsForD.Length > 0)
                                        {
                                            for (int di = 0; di < allDocumentsForD.Length; di++)
                                            {
                                                var doc = allDocumentsForD.GetElement(di);
                                                bool isOffscreen = doc.CurrentIsOffscreen != 0;

                                                // 跳过不可见的Document
                                                if (isOffscreen)
                                                    continue;

                                                // 使用第一个可见的Document控件
                                                edits = doc;
                                                break;
                                            }
                                        }
                                    }

                                    // 策略3：如果还是失败，降级到原来的逻辑（兼容旧版QQ）
                                    if (edits == null)
                                    {
                                        edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                    }

                                    // 最后尝试：如果还是失败，尝试查找Document
                                    if (edits == null)
                                    {
                                        edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId));
                                    }

                                    break;
                                }
                            }
                        }
                    }


                    if (edits != null)
                    {
                        edits.SetFocus();

                        // 新版QQ的输入框在Document底部，需要点击Document才能激活
                        ActivateDocumentInput(edits);

                        // 第一次：发送msgContent1（成绩）
                        Win32.Win32SetText(msgContent1);
                        Win32.CtrlV();

                        if (Config.GetBool("自动发送成绩"))
                        {
                            SendMessage(q);
                        }

                        // 第二次：发送msgContent2（新文章）
                        if (!string.IsNullOrWhiteSpace(msgContent2))
                        {
                            edits.SetFocus();
                            ActivateDocumentInput(edits);

                            Win32.Win32SetText(msgContent2);
                            Win32.CtrlV();

                            if (Config.GetBool("自动发送成绩"))
                            {
                                SendMessage(q);
                            }
                        }

                        // Win32.Delay(50);  // 等待所有操作完成再切换焦点
                        caller.Dispatcher.Invoke(() => {
                            MainWindow.Current.FocusInput();
                        });

                    }
            }
            catch (Exception)
            {

            }
        }

    }
}
