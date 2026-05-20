using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TypeSunny.UI;
using TypeSunny.Utils;


namespace TypeSunny.Core
{
    public enum WordStates
    {
        NO_TYPE,
        RIGHT,
        WRONG,
        CURRENT
    };


internal static class TextInfo
    {
        public static string WrongExclude = "……——“”‘’";
        public static string MatchText = ""; //赛文
//        public static string CachedMatchText = "";//赛文
        public static int Paragraph = 0; //缓存段号
        public static TxtSource CacheTxtType = TxtSource.unchange; //赛文
        public static string TextMD5 = "";
        public static int PageNum = 0;
        public static List<TextBlock> Blocks = new List<TextBlock>(); //显示文本UI

        public static List <string> Words = new List<string>();

        public static List<WordStates> wordStates = new List<WordStates>(); //显示文本UI的状态

        public static List<WordStates> BlocksStates = new List<WordStates>(); //显示文本UI的状态

        public static HashSet<int> LineBreaks = new HashSet<int>(); // 需要在此索引前插入视觉换行

        public static int PageStartIndex = 0;

        public static bool Exit = false;

        public static Dictionary<int, string> WrongRec = new Dictionary<int, string>();

        public static Dictionary<int, string> SlowRec = new Dictionary<int, string>();

        public static List<CiTiSegment> CiTiSegments = new List<CiTiSegment>();

        public static List<int> CiTiSegmentIndices = new List<int>();

        // 编码下显标签，与 Blocks 一一对应（无编码下显时为 null）
        public static List<TextBlock> CodeLabels = new List<TextBlock>();

        // 编码下显时的字状态背景层，与 Blocks 一一对应（无编码下显时为 null）
        public static List<Border> StateBackgrounds = new List<Border>();

        // 编码下显的当前输入状态，与全局索引一一对应
        public static Dictionary<int, string> CodeLabelInputs = new Dictionary<int, string>();

     //   public static Dictionary<int, string> WrongCounter = new Dictionary<int, string>();
    //    public static Dictionary<int, string> BackCounter = new Dictionary<int, string>();
    //    public static Dictionary<int, string> CorrectionCounter = new Dictionary<int, string>();
        static public string CalMD5(string text)
        {


            byte[] b = System.Text.Encoding.Default.GetBytes(text);

            b = new System.Security.Cryptography.MD5CryptoServiceProvider().ComputeHash(b);
            string ret = "";
            for (int i = 0; i < b.Length; i++)
            {
                ret += b[i].ToString("x").PadLeft(2, '0');
            }
            return ret;

        }




        public static void Check(string inputTxt)
        {
            if (TextInfo.Words.Count == 0)
                return;

            bool isLooking = MainWindow.Current.IsLookingType;

            var enumerator = StringInfo.GetTextElementEnumerator(inputTxt);
            int inputLen = 0;
            var inputElements = new string[TextInfo.Words.Count];
            while (enumerator.MoveNext() && inputLen < TextInfo.Words.Count)
            {
                inputElements[inputLen++] = enumerator.GetTextElement();
            }

            for (int i = 0; i < TextInfo.Words.Count; i++)
            {
                if (i >= inputLen)
                    TextInfo.wordStates[i] = WordStates.NO_TYPE;
                else if (TextInfo.Words[i] == inputElements[i] || isLooking)
                    TextInfo.wordStates[i] = WordStates.RIGHT;
                else
                    TextInfo.wordStates[i] = WordStates.WRONG;
            }
        }

   
    }
}
