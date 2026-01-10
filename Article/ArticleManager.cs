using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Text.RegularExpressions;
using TypeSunny.Difficulty;

// EPUB支持（需要VersOne.Epub包）
// 如果编译错误，请在Visual Studio中还原NuGet包
// 或者注释掉下面这行和所有EPUB相关代码
#if EPUB_SUPPORT
using VersOne.Epub;
#endif


namespace TypeSunny
{
    public class Article
    { 
        public string Title { get; set; } 
        public StringInfo Text { get; set; }


        public Article(string title, string text)//, int progress = 0)
        {
            Title = title;
            Text = new StringInfo(text.Replace("\n","").Replace("\r", "").Replace("\t", ""));
           // Text = new StringInfo(text.Replace("\r", "").Replace("\t", "").Replace("\n","\t"));
        }
    }  
    internal static class  ArticleManager
    {
        const string FolderPath = "文章";

       



        public static Dictionary <string,Article> Articles = new Dictionary<string,Article> ();
        private static DifficultyDict difficultyDict = new DifficultyDict();


   
        public static void ReadFiles ()
        {
            Articles.Clear();
            DirectoryInfo dir = new DirectoryInfo(FolderPath);

            if (!dir.Exists )
            {
                dir.Create ();
                return;
            }

            // 读取TXT文件
            foreach (FileInfo file in dir.GetFiles("*.txt"))
            {
                string name = file.Name;

                Encoding enc = TxtFileEncoder.GetEncoding(file.FullName);
                string txt = File.ReadAllText(file.FullName,enc);


                Articles.Add(name, new Article(name, txt));


            }

#if EPUB_SUPPORT
            // 读取EPUB文件（需要VersOne.Epub包）
            foreach (FileInfo file in dir.GetFiles("*.epub"))
            {
                try
                {
                    string name = file.Name;
                    string txt = ExtractTextFromEpub(file.FullName);

                    if (!string.IsNullOrEmpty(txt))
                    {
                        Articles.Add(name, new Article(name, txt));
                    }
                }
                catch (Exception ex)
                {
                    // 记录错误但继续处理其他文件
                    System.Diagnostics.Debug.WriteLine($"读取EPUB文件 {file.Name} 失败: {ex.Message}");
                }
            }
#endif


        }

#if EPUB_SUPPORT
        /// <summary>
        /// 从EPUB文件提取纯文本
        /// </summary>
        private static string ExtractTextFromEpub(string epubFilePath)
        {
            try
            {
                StringBuilder fullText = new StringBuilder();

                // 读取EPUB文件
                EpubBook book = EpubReader.ReadBook(epubFilePath);

                // 遍历所有HTML文件
                foreach (var htmlFile in book.Content.Html.Local)
                {
                    // 获取HTML内容
                    string htmlContent = htmlFile.Content;

                    // 将HTML转换为纯文本
                    string plainText = HtmlToPlainText(htmlContent);

                    if (!string.IsNullOrWhiteSpace(plainText))
                    {
                        fullText.AppendLine(plainText);
                    }
                }

                return fullText.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EPUB文本提取失败: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 将HTML内容转换为纯文本
        /// </summary>
        private static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html))
                return "";

            // 移除script和style标签及其内容
            html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);

            // 将<br>、<p>等标签转换为换行符
            html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</div>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</h[1-6]>", "\n", RegexOptions.IgnoreCase);

            // 移除所有HTML标签
            html = Regex.Replace(html, @"<[^>]+>", "");

            // 解码HTML实体
            html = System.Net.WebUtility.HtmlDecode(html);

            // 移除多余的空行
            html = Regex.Replace(html, @"\n\s*\n", "\n");

            // 去除首尾空白
            html = html.Trim();

            return html;
        }
#endif

        public static int SectionSize
        {
            get
            {
                return ArticleConfig.GetInt("每段字数");
            }
            set
            {
                ArticleConfig.Set("每段字数", value);
                UpdateWindows();
                ArticleConfig.WriteConfig(500);
            }
        }


        public  static string GetCurrentSection()
        {

            string rt;
            if (!Articles.ContainsKey(Title))
                return "";

            if (Progress >= TotalSize)
                return "没了";

            if (Progress + SectionSize < TotalSize)
                rt =  Articles[Title].Text.SubstringByTextElements((Index - 1) * SectionSize, SectionSize);
            else
                rt = Articles[Title].Text.SubstringByTextElements((Index - 1) * SectionSize);



            if (EnableFilter)
            {
                //            txt = Filter.ProcFilter(txt);

                rt = Filter.ProcFilter(rt);
            }

            if (RemoveSpace)
                rt = rt.Replace(" ", "").Replace("　", "");
            else //去除首末空格
            {
                if (rt.Length >= 1 && rt.Substring(0, 1) == " ")
                    rt = rt.Substring(1, rt.Length - 1);

                if (rt.Length >= 1 && rt.Substring(rt.Length - 1, 1) == " ")
                    rt = rt.Substring(0, rt.Length - 1);
            }
            return rt;
        }

        public static void NextSection()
        {
            if (!Articles.ContainsKey(Title))
                return;

            if (Progress >= TotalSize)
                return;

            Progress = Math.Min(Progress + SectionSize, TotalSize);


            UpdateWindows();
        }

        public static void PrevSection()
        {
            if (!Articles.ContainsKey(Title))
                return;

            if (Progress ==  0)
                return;
            Progress = Math.Max(Progress - SectionSize, 0);
            UpdateWindows();
        }

        public static string GetFormattedCurrentSection()
        {
            if (!Articles.ContainsKey(Title))
                return "";

            string txt = GetCurrentSection();

            // 计算难度
            double difficulty = difficultyDict.Calc(txt);
            string difficultyText = difficultyDict.DiffText(difficulty);

            StringBuilder sb = new StringBuilder();

            // 第一行：[难度xx]标题 [字数xx]
            sb.Append("[难度");
            sb.Append(difficultyText);
            sb.Append("]");
            sb.Append(Title.Replace(".txt","").Replace(".Txt", "").Replace(".TXT", "").Replace(".epub","").Replace(".Epub", "").Replace(".EPUB", ""));
            sb.Append(" [字数");
            sb.Append(new StringInfo(txt).LengthInTextElements);
            sb.Append("]");
            sb.AppendLine();

            // 内容
            sb.Append(txt);
            sb.AppendLine();

            // 尾部信息
            sb.Append("-----第");
            sb.Append(Index);
            sb.Append("段");


            sb.Append("-");

            sb.Append(" 共");
            sb.Append(MaxIndex);
            sb.Append("段 ");

            sb.Append(" 进度 ");
            sb.Append((Index - 1) * SectionSize);
            sb.Append("/");
            sb.Append(TotalSize);
            sb.Append("字 ");

            sb.Append(" 本段");
            sb.Append(new StringInfo(txt).LengthInTextElements);
            sb.Append("字 ");

            sb.Append("晴发文");
            return sb.ToString();

        }

        public static string GetFormattedNextSection()
        {

            string rt;
            if (!Articles.ContainsKey(Title))
                return "";
            else
            {
                rt = GetFormattedCurrentSection();
                NextSection();
            }
            return rt; 
        }

        public static int Index
        {
            get
            {
                if (!Articles.ContainsKey(Title))
                    return 1;
                else
                {
                    int counter = 0;


                    while (true)
                    {
                        if (SectionSize * counter > Progress)
                            break;

                        counter++;
                    }


                    return counter;
                }


            }


        }

        public static int TotalSize
        {
            get
            {
                if (!Articles.ContainsKey(Title))
                    return 0;
                else
                    return Articles[Title].Text.LengthInTextElements;
            }
        }

        public static int MaxIndex
        {
            get
            {
                if (!Articles.ContainsKey(Title))
                    return 1;
                else
                {
                    int counter = 0;

                    while (true)
                    {
                        if (SectionSize * counter >= TotalSize)
                            break;

                        counter++;
                    }

                    return counter;
                }

            }



        }

        public static string Title
        {
            get
            {
                return ArticleConfig.GetString("当前文章");
            }
            set
            {
                if (!Articles.ContainsKey(value) || ArticleConfig.GetString("当前文章") == value)
                    return;
                else
                {
                    ArticleConfig.Set("当前文章", value);
                    UpdateWindows();
                }

            }
        }

        private static void  UpdateWindows()
        {
            ((MainWindow)App.Current.Windows[0]).UpdateButtonProgress();
        
            ((MainWindow)App.Current.Windows[0]).winArticle?.UpdateDisplay();

        }
        public static int Progress
        {
            get
            {
                if (!Articles.ContainsKey(Title))
                    return 0;
                else
                    return ArticleConfig.GetInt("进度_" + Title);
            }
            set
            {
                if (Articles.ContainsKey(Title))
                {
                    int v = Math.Min( Math.Max(0, value), TotalSize - 1);

                    ArticleConfig.Set("进度_" + Title, v);
                    UpdateWindows();
                    ArticleConfig.WriteConfig(500);
    
                }

            }
        }

        public static bool EnableFilter
        {
            get
            {

                    return ArticleConfig.GetBool("字集过滤");
            }
            set
            {

                    ArticleConfig.Set("字集过滤" , value);
                    ArticleConfig.WriteConfig(500);


            }
        }

        public static bool RemoveSpace
        {
            get
            {

                return ArticleConfig.GetBool("去除空格");
            }
            set
            {

                ArticleConfig.Set("去除空格", value);
                ArticleConfig.WriteConfig(500);


            }
        }


        public static int  Search(string text, int startIndex)
        {
            int rt = -1;
            
            rt = Articles[Title].Text.String.IndexOf(text,startIndex);
            if (rt > 0)
                rt = new StringInfo( Articles[Title].Text.String.Substring(0, rt)).LengthInTextElements;

            return rt;
        }

        static ArticleManager()
        {
            ArticleConfig.SetDefault
            (
                "每段字数", "200",
                "字集过滤", "是",
                "去除空格", "是"
                
            );

            ArticleConfig.ReadConfig();

            ArticleManager.ReadFiles();
        }
    }
}
