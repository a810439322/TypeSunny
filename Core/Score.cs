using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using System.Globalization;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Linq;
using TypeSunny.Utils;

namespace TypeSunny.Core
{
    public enum ZiciType
    {
        zi,
        ci,
        punct
    };
static internal class Score
    {
        public static int Hit = 0;
        public static int LeftCount = 0;
        public static int RightCount = 0;
        public static int SpaceCount = 0;
        public static double HitRate = 0;
        public static int TotalWordCount = 0;
        public static int InputWordCount = 0;
        public static int CurWord = 0;
        public static double Speed = 0;
        public static int Backs = 0;
        public static double KPW = 0;
  //      public static double Accuracy = 0;
        public static int Wrong = 0;
        public static int More = 0;
        public static int Less = 0;
        public static TimeSpan Time;
        private static Random RND = new Random();
        public static int Paragraph = 0;
        public static string ParagraphString = "";  // 原始段号字符串（如 "tdu"）
        public static string ArticleMark = "";  // 文来接口的mark字段（如 "1-34112"）
        public static string DifficultyText = "";  // 当前文章难度文本（如 "普(1.84)"）
        public static int Correction = 0;
        public static double LRRatio = 0;

        public static int BimeHit = 0;
        public static int BimeBacks = 0;
        public static int BimeCorrection = 0;

        // 废码统计
        public static int WasteCodes = 0;              // 废码次数
        public static bool IsComposing = false;        // 是否正在输入编码（未上屏）
        public static int CompositionStartHit = 0;     // 开始输入编码时的击键数



        //打词率计算
        public static Stack<ZiciType> ZiciStack = new Stack<ZiciType>();


        public static List<long> ImeKeyTime = new List<long>();
        public static List<long> CommitTime = new List<long>();
        public static List<int> ImeKeyValue  = new List<int>();
        public static List<string> CommitStr = new List<string>();
        public static List<int> CommitCharCount = new List<int>();    // 每次上屏的字符数
        public static List<string> CommitText = new List<string>();   // 每次上屏的完整文本

        public static List<long> BiaoDingImeKeyTime = new List<long>();
        public static List<long> BiaoDingCommitTime = new List<long>();
        public static List<int> BiaoDingImeKeyValue = new List<int>();
        public static List<string> BiaoDingCommitStr = new List<string>();


        public static string ExcludePuncts = "~!@#$%^&*()_+|}{\":?><`[]\\;',./~！@#￥%……&*（）——+{}|：“”《》？·、【】；‘’，。";
        public static void AddInputStack(string text)
        {
            var si = new StringInfo(text);
            if (si.LengthInTextElements == 0)
                return;

            bool[] IsPunct = new bool[si.LengthInTextElements];
            int NonePunctCount = 0;
            for (int i=0;i< si.LengthInTextElements;i++)
            {
                if (ExcludePuncts.Contains(si.SubstringByTextElements(i, 1)))
                    IsPunct[i] = true;
                else
                    NonePunctCount++;
            }

            bool IsCi = NonePunctCount >= 2;

            foreach (bool ispunct in IsPunct)
            {
                if (ispunct)
                    ZiciStack.Push(ZiciType.punct);
                else if (IsCi)
                    ZiciStack.Push(ZiciType.ci);
                else
                    ZiciStack.Push(ZiciType.zi);
            }
        }
        public static double GetHit ()
        {
            if (BimeHit > 0)
                return BimeHit;
            else
                return Hit;

          //  return Math.Max(Hit, BimeHit);
        }

        public static double GetBacks()
        {

            return BimeBacks > 0 ? BimeBacks : Backs;
          //  return Math.Max(Backs, BimeBacks);
        }

        public static double GetValidSpeed()
        {
            double rt = Speed;

            if (Wrong > 0)
            {
                rt = (double)(InputWordCount - Wrong * 5) / Time.TotalMinutes;

            }
            else
            {
                rt = Speed;
            }

            return rt;
        }
        public static double GetCorrection()
        {
            return Math.Max(Correction, BimeCorrection);
        }

        public static double GetAccuracy()
        {
    

           return   (GetHit() - GetCorrection() - GetBacks() * 2.0) / (TotalWordCount + GetCorrection()) * TotalWordCount / GetHit();
            

     
        }

        public static double GetCiRatio()
        {
            double total = 0;
            double ci = 0;
            foreach (ZiciType type in ZiciStack)
            {
                switch (type)
                {
                    case ZiciType.zi:
                        total++;
                        break;
                    case ZiciType.ci:
                        ci++;
                        total++;
                        break;
                    case ZiciType.punct:
                        break;
                    default:
                        break;

                }
            }



            if (total == 0)
                return 0;
            else
                return ci / total;
        }
        public static void Reset()
        {
            Hit = 0;
            LeftCount = 0;
            RightCount = 0;
            SpaceCount = 0;
            HitRate = 0;
            TotalWordCount = 0;
            InputWordCount = 0;
            CurWord = 0;
            Speed = 0;
            Backs = 0;
            Correction = 0;
            KPW = 0;
        //    Accuracy = 0;
            Time = new TimeSpan(0);
            Wrong = 0;
            More = 0;
            Less = 0;
            BimeHit = 0;
            BimeBacks = 0;
            BimeCorrection = 0;
            LRRatio = 0;
            DifficultyText = "";
            ZiciStack.Clear();
            WasteCodes = 0;
            IsComposing = false;
            CompositionStartHit = 0;

            ImeKeyTime.Clear();
            CommitTime.Clear();
            ImeKeyValue.Clear();
            CommitStr.Clear();
            CommitCharCount.Clear();   // 清空字符数记录
            CommitText.Clear();        // 清空文本记录



            BiaoDingImeKeyTime.Clear();
            BiaoDingCommitTime.Clear();
            BiaoDingImeKeyValue.Clear();
            BiaoDingCommitStr.Clear();

        }

        public static string Progress()
        {
            StringBuilder r = new StringBuilder();

            string SpeedReport = Score.Speed.ToString("F2"); ;
            if (Wrong > 0)
            {
                SpeedReport = ((double)(InputWordCount - Wrong * 5) / Time.TotalMinutes).ToString("F2") + "/" + SpeedReport;

            }

            r.AppendFormat("速:{0} 准:{1} 击:{2} 码:{3}", SpeedReport, GetAccuracy().ToString("P2"), Score.HitRate.ToString("F2"), Score.KPW.ToString("F2"));

            return r.ToString();
        }
        /// <summary>
        /// 解析成绩显示顺序配置，返回规范化后的项目列表
        /// </summary>
        private static readonly string DefaultOrder = "速度,击键,键准,字数,难度,打词率,标顶,重打,码长,总键数,键法,回改,禁用回改,退格,废码,选重,用时,错字,盲打正确率,看打正确率,盲打模式,看打模式,签名";
        private static readonly HashSet<string> ValidItems = new HashSet<string>(DefaultOrder.Split(','));

        public static List<string> GetScoreOrder()
        {
            string raw = Config.GetString("成绩显示顺序");
            if (string.IsNullOrWhiteSpace(raw))
                raw = DefaultOrder;

            var result = new List<string>();
            var seen = new HashSet<string>();
            foreach (string item in raw.Split(','))
            {
                string trimmed = item.Trim();
                if (trimmed.Length > 0 && ValidItems.Contains(trimmed) && !seen.Contains(trimmed))
                {
                    result.Add(trimmed);
                    seen.Add(trimmed);
                }
            }
            // 补尾：默认顺序中有、但用户配置中缺失的项
            foreach (string item in DefaultOrder.Split(','))
            {
                if (!seen.Contains(item))
                {
                    result.Add(item);
                    seen.Add(item);
                }
            }
            return result;
        }

        public static List<string> ReportItems()
        {
            List<string> report = new List<string>();

            string SpeedReport = Math.Round( Score.Speed , 2).ToString("F2"); ;

            if (Config.GetBool("看打模式"))
            {
                int wr = Math.Max(More, Less);
                if (wr > 0)
                    SpeedReport = Math.Round(((double)(TotalWordCount - wr * 5) / Time.TotalMinutes), 2).ToString("F2") + "/" + SpeedReport;
            }
            else if (Wrong > 0)
            {
                SpeedReport = Math.Round(((double)(TotalWordCount - Wrong * 5) / Time.TotalMinutes),2).ToString("F2") + "/" + SpeedReport;

            }

            bool isBime =  BimeHit > 0;
            bool notBime = !isBime;

            string paragraphLabel;
            if (!string.IsNullOrEmpty(ArticleMark))
            {
                paragraphLabel = "段" + ArticleMark;
            }
            else if (!string.IsNullOrEmpty(ParagraphString))
            {
                paragraphLabel = "第" + ParagraphString + "段";
            }
            else
            {
                paragraphLabel = "第" + Paragraph + "段";
            }
            report.Add(paragraphLabel);

            int TypeCount = RetypeCounter.Get(TextInfo.TextMD5);

            foreach (string item in GetScoreOrder())
            {
                switch (item)
                {
                    case "难度":
                        if (Config.GetBool("显示_难度") && !string.IsNullOrWhiteSpace(DifficultyText))
                            report.Add(DifficultyText);
                        break;
                    case "速度":
                        if (Config.GetBool("显示_速度"))
                            report.Add("速度" + SpeedReport);
                        break;
                    case "击键":
                        if (Config.GetBool("显示_击键"))
                        {
                            string hitStr = "击键" + HitRate.ToString("F2");
                            if (StateManager.txtSource == TxtSource.trainer)
                                hitStr += " /" + WinTrainer.TargetHit.ToString("F2");
                            report.Add(hitStr);
                        }
                        break;
                    case "码长":
                        if (Config.GetBool("显示_码长"))
                            report.Add("码长" + Score.KPW.ToString("F2"));
                        break;
                    case "字数":
                        if (Config.GetBool("显示_字数"))
                            report.Add("字数" + TotalWordCount.ToString());
                        break;
                    case "重打":
                        if (Config.GetBool("显示_重打"))
                        {
                            if (TypeCount > 1)
                                report.Add("重打" + (TypeCount - 1).ToString());
                            else
                                report.Add("【首打】");
                        }
                        break;
                    case "总键数":
                        if (Config.GetBool("显示_总键数"))
                            report.Add("总键数" + GetHit().ToString("F0"));
                        break;
                    case "键法":
                        if (Config.GetBool("显示_键法") && BimeHit == 0)
                        {
                            if (RightCount == 0)
                                LRRatio = 1;
                            else
                                LRRatio = (double)LeftCount / (double)RightCount;
                            report.Add("键法" + LRRatio.ToString("p2") + " (左" + LeftCount + "右" + RightCount + "空格" + SpaceCount + ")");
                        }
                        break;
                    case "回改":
                        if (Config.GetBool("显示_回改"))
                            report.Add("回改" + Score.GetCorrection().ToString("F0"));
                        break;
                    case "禁用回改":
                        if (Config.GetBool("显示_禁用回改") && Config.GetBool("禁用回改")
                            && StateManager.txtSource != TxtSource.raceApi
                            && StateManager.txtSource != TxtSource.jbs
                            && StateManager.txtSource != TxtSource.jisucup)
                            report.Add("【禁用回改】");
                        break;
                    case "退格":
                        if (Config.GetBool("显示_退格"))
                            report.Add("退格" + GetBacks().ToString("F0"));
                        break;
                    case "键准":
                        if (Config.GetBool("显示_键准"))
                            report.Add("键准" + (GetAccuracy() * 100).ToString("F2") + "%");
                        break;
                    case "废码":
                        if (Config.GetBool("显示_废码") && notBime && WasteCodes > 0)
                            report.Add("废码" + WasteCodes.ToString());
                        break;
                    case "打词率":
                        if (Config.GetBool("显示_打词率") && notBime)
                            report.Add("打词率" + (GetCiRatio() * 100).ToString("F2") + "%");
                        break;
                    case "选重":
                        if (Config.GetBool("显示_选重") && notBime)
                            report.Add("选重" + GetChoose().ToString());
                        break;
                    case "标顶":
                        if (Config.GetBool("显示_标顶") && notBime)
                            report.Add("标顶" + GetBiaoDing().ToString());
                        break;
                    case "用时":
                        if (Config.GetBool("显示_用时"))
                        {
                            string t = Score.Time.ToString();
                            int semi = t.LastIndexOf(":");
                            if (t.Length > semi + 6)
                                t = t.Substring(0, semi + 6);
                            if (t.Length > 3 && t.Substring(0, 3) == "00:")
                                t = t.Substring(3);
                            report.Add("用时" + t);
                        }
                        break;
                    case "错字":
                        if (Config.GetBool("显示_错字"))
                        {
                            if (Config.GetBool("看打模式"))
                            {
                                if (Less > 0 && More > 0)
                                    report.Add("少" + Less + "多" + More);
                                else if (More > 0)
                                    report.Add("多" + More);
                                else if (Less > 0)
                                    report.Add("少" + Less);
                            }
                            else
                            {
                                if (Wrong > 0)
                                    report.Add("错字" + Wrong);
                            }
                        }
                        break;
                    case "盲打正确率":
                        if (Config.GetBool("显示_盲打正确率") && Config.GetBool("盲打模式"))
                        {
                            int wr = Math.Max(More, Less);
                            double ratio = Math.Round((double)(TotalWordCount - wr) / (double)TotalWordCount, 4);
                            report.Add("盲打正确率" + (ratio * 100).ToString("F2") + "%");
                        }
                        break;
                    case "看打正确率":
                        if (Config.GetBool("显示_看打正确率") && Config.GetBool("看打模式") && !Config.GetBool("盲打模式"))
                        {
                            int wr = Math.Max(More, Less);
                            double ratio = Math.Round((double)(TotalWordCount - wr) / (double)TotalWordCount, 4);
                            report.Add("看打正确率" + (ratio * 100).ToString("F2") + "%");
                        }
                        break;
                    case "盲打模式":
                        if (Config.GetBool("显示_盲打模式") && Config.GetBool("盲打模式"))
                            report.Add("【盲打模式】");
                        break;
                    case "看打模式":
                        if (Config.GetBool("显示_看打模式") && Config.GetBool("看打模式") && !Config.GetBool("盲打模式"))
                            report.Add("【看打模式】");
                        break;
                    case "签名":
                        if (Config.GetBool("显示_签名"))
                            report.Add(Config.GetString("成绩签名"));
                        break;
                }
            }

            report.Add(StateManager.Version);
            return report;
        }

        public static string Report()
        {
            return string.Join("  ", ReportItems());
        }

        public static int GetDisplayWidth(string s)
        {
            int width = 0;
            foreach (char c in s)
                width += c > 127 ? 2 : 1;
            return width;
        }

        public static string PadRightByDisplayWidth(string s, int totalWidth)
        {
            int pad = totalWidth - GetDisplayWidth(s);
            return pad > 0 ? s + new string(' ', pad) : s;
        }

        private static readonly string[] ScoreItemPrefixes = new string[]
        {
            "速度", "击键", "码长", "字数", "重打", "总键数", "键法", "回改",
            "退格", "键准", "废码", "打词率", "选重", "标顶", "用时",
            "错字", "少", "多", "盲打正确率", "看打正确率",
            "【禁用回改】", "【盲打模式】", "【看打模式】", "【首打】",
            "普(", "易(", "中(", "难(", "超难(", "水(", "虐(",
            "无(", "轻松(", "容易(", "一般(", "稍难(", "困难(", "极难(", "地狱(",
        };

        public static List<string> ParseReportLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return new List<string>();

            line = line.Trim();

            // 先尝试双空格拆分（新格式）
            var parts = line.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 3)
            {
                var result = new List<string>();
                foreach (var p in parts)
                    result.Add(p.Trim());
                return result;
            }

            // 老格式：单空格分隔，用前缀识别拆分
            var items = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                // 跳过空格
                while (i < line.Length && line[i] == ' ') i++;
                if (i >= line.Length) break;

                // 找下一个成绩项的起始位置
                int nextStart = line.Length;
                for (int j = i + 1; j < line.Length; j++)
                {
                    // 检查是否有空格+前缀的模式
                    if (line[j - 1] == ' ')
                    {
                        foreach (var prefix in ScoreItemPrefixes)
                        {
                            if (j + prefix.Length <= line.Length && line.Substring(j, prefix.Length) == prefix)
                            {
                                nextStart = j;
                                goto found;
                            }
                        }
                        // 检查版本签名（末尾的 xxxVersion 或中文签名）
                    }
                }
                found:

                string item = line.Substring(i, nextStart - i).Trim();
                if (!string.IsNullOrEmpty(item))
                    items.Add(item);
                i = nextStart;
            }

            return items;
        }

        public static string FormatRows(List<List<string>> rows)
        {
            if (rows.Count == 0) return "";
            int cols = 0;
            foreach (var row in rows)
                if (row.Count > cols) cols = row.Count;
            int[] maxWidths = new int[cols];
            foreach (var row in rows)
                for (int i = 0; i < row.Count; i++)
                {
                    int w = GetDisplayWidth(row[i]);
                    if (w > maxWidths[i]) maxWidths[i] = w;
                }
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < rows.Count; r++)
            {
                if (r > 0) sb.AppendLine();
                var row = rows[r];
                for (int i = 0; i < row.Count; i++)
                {
                    if (i > 0) sb.Append("  ");
                    if (i < row.Count - 1)
                        sb.Append(PadRightByDisplayWidth(row[i], maxWidths[i]));
                    else
                        sb.Append(row[i]);
                }
            }
            return sb.ToString();
        }

        static List<Key> KeysLeft = new List<Key>
        {
           Key.Oem3,
            Key.D1,
            Key.D2,
            Key.D3,
            Key.D4,
            Key.D5,
            Key.Tab,
            Key.Q,
            Key.W,
            Key.E,
            Key.R,
            Key.T,
            Key.Capital,
            Key.A,
            Key.S,
            Key.D,
            Key.F,
            Key.G,
            Key.LeftShift,
            Key.Z,
            Key.X,
            Key.C,
            Key.V,
            Key.B,
            Key.LeftCtrl,
            Key.LWin

        };

        static List<Key> KeysRight = new List<Key>
        {
            Key.D6,
            Key.D7,
            Key.D8,
            Key.D9,
            Key.D0,
            Key.OemMinus,
            Key.OemPlus,
            Key.Back,
            Key.Y,
            Key.U,
            Key.I,
            Key.O,
            Key.P,
            Key.OemOpenBrackets,
            Key.Oem6,
            Key.Oem5,
            Key.H,
            Key.J,
            Key.K,
            Key.L,
            Key.Oem1,
            Key.OemQuotes,
            Key.N,
            Key.M,
            Key.OemComma,
            Key.OemPeriod,
            Key.OemQuestion,
            Key.RightShift,
            Key.Return,
            Key.RightCtrl,

        };

        static public int GetChoose()
        {
            int choose = 0;

            long thresh = 20;
            


            for (int i = 0; i< ImeKeyTime.Count; i++)
            {
                for (int j = 0; j< CommitTime.Count; j++)
                {
                    if (CommitTime[j] > ImeKeyTime[i] + thresh)
                        break;

                    if (Math.Abs(CommitTime[j] - ImeKeyTime[i]) <= thresh)
                        if (IntStringDict.Selection.ContainsKey(ImeKeyValue[i]) && !IntStringDict.Selection[ImeKeyValue[i]].Contains(CommitStr[j])) 
                        {
                            choose++;
                            break;
                        }

                }
            }




            return choose;
        }



        static public int GetBiaoDing()
        {
            int bd= 0;

            long thresh = 20;



            for (int i = 0; i < BiaoDingImeKeyTime.Count; i++)
            {
                for (int j = 0; j < BiaoDingCommitTime.Count; j++)
                {
                    if (BiaoDingCommitTime[j] > BiaoDingImeKeyTime[i] + thresh)
                        break;

                    if (Math.Abs(BiaoDingCommitTime[j] - BiaoDingImeKeyTime[i]) <= thresh)
                        if (IntStringDict.BiaoDing.ContainsKey(BiaoDingImeKeyValue[i]) && IntStringDict.BiaoDing[BiaoDingImeKeyValue[i]].Contains(BiaoDingCommitStr[j]))
                        {
                            bd++;
                            break;
                        }

                }
            }




            return bd;
        }

        static public void SetJianFa(Key key)
        {
            if (key  == Key.Space)
                SpaceCount++;
           if (KeysRight.Contains(key))
                RightCount++;
           else if (KeysLeft.Contains(key))
                LeftCount++;

        }

        /// <summary>
        /// 格式化时间显示（秒 -> 时分秒）
        /// </summary>
        /// <param name="seconds">总秒数</param>
        /// <returns>格式化后的时间字符串</returns>
        static public string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            if (time.TotalHours >= 1)
            {
                return time.ToString(@"h\时m\分s\秒");
            }
            else if (time.TotalMinutes >= 1)
            {
                return time.ToString(@"m\分s\秒");
            }
            else
            {
                return time.ToString(@"s\秒");
            }
        }
    }
}
