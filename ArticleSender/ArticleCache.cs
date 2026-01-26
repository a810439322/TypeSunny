using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TypeSunny.ArticleSender
{
    /// <summary>
    /// 文章缓存管理器，负责分段和导航
    /// </summary>
    public class ArticleCache
    {
        private ArticleData currentArticle;
        private List<string> segments;
        private int currentSegmentIndex;
        private int segmentLength;
        private string articleMark;  // 保存文章的mark标记
        private string articleDifficulty;  // 保存文章的难度描述
        private string articleDifficultyName;  // 保存文章的难度名称（如"简"、"普"、"难"）
        private int bookId;  // 书籍ID
        private int sortNum;  // 当前段号
        private int difficultyId;  // 难度ID，来自custom_difficulty字段

        /// <summary>
        /// 当前段落索引（从1开始）
        /// </summary>
        public int CurrentSegmentIndex
        {
            get { return currentSegmentIndex + 1; }
        }

        public ArticleCache()
        {
            segments = new List<string>();
            currentSegmentIndex = 0;
        }

        /// <summary>
        /// 加载新文章
        /// </summary>
        public void LoadArticle(ArticleData article)
        {
            currentArticle = article;
            articleMark = article.Mark ?? "";  // 保存mark标记
            articleDifficulty = article.Difficulty ?? "";  // 保存难度描述
            articleDifficultyName = article.DifficultyName ?? "";  // 保存难度名称
            bookId = article.BookId;  // 保存书籍ID
            sortNum = article.SortNum;  // 保存段号
            difficultyId = article.DifficultyId;  // 保存难度ID（来自custom_difficulty）
            segmentLength = Config.GetInt("文来字数");

            if (segmentLength <= 0)
                segmentLength = 500; // 默认500字

            // 判断是否是文来的文章（有 bookId 和 sortNum）
            bool isWenlaiArticle = article.BookId > 0 && article.SortNum > 0;

            if (isWenlaiArticle)
            {
                // 文来文章：服务端已根据 length 和 strict_length 参数处理，直接使用返回的内容
                segments = new List<string> { article.Content };
            }
            else
            {
                // 其他文章：需要按段长度分段
                segments = SplitIntoSegments(article.FullContent, segmentLength);
            }
            currentSegmentIndex = 0;
        }

        /// <summary>
        /// 获取当前段落
        /// </summary>
        public string GetCurrentSegment()
        {
            if (segments.Count == 0)
                return "";

            if (currentSegmentIndex < 0)
                currentSegmentIndex = 0;

            if (currentSegmentIndex >= segments.Count)
                currentSegmentIndex = segments.Count - 1;

            return segments[currentSegmentIndex];
        }

        /// <summary>
        /// 获取下一段
        /// </summary>
        public string GetNextSegment()
        {
            if (segments.Count == 0)
                return "";

            currentSegmentIndex++;

            if (currentSegmentIndex >= segments.Count)
            {
                // 已经是最后一段，返回空字符串表示需要换新文章
                currentSegmentIndex = segments.Count - 1;
                return "";
            }

            return segments[currentSegmentIndex];
        }

        /// <summary>
        /// 获取上一段
        /// </summary>
        public string GetPreviousSegment()
        {
            if (segments.Count == 0)
                return "";

            currentSegmentIndex--;

            if (currentSegmentIndex < 0)
            {
                currentSegmentIndex = 0;
            }

            return segments[currentSegmentIndex];
        }

        /// <summary>
        /// 获取当前文章标题
        /// </summary>
        public string GetCurrentTitle()
        {
            return currentArticle?.Title ?? "";
        }

        /// <summary>
        /// 获取进度信息
        /// </summary>
        public string GetProgress()
        {
            if (segments.Count == 0)
                return "0/0";

            // 如果是文来文章（有 mark 字段），使用 mark 中的段数信息
            if (!string.IsNullOrEmpty(articleMark))
            {
                // mark 格式："1-34112" 表示第1段/共34112段
                // 直接返回 sortNum/总段数
                if (articleMark.Contains("-"))
                {
                    string[] parts = articleMark.Split('-');
                    if (parts.Length == 2)
                    {
                        // parts[0] 是当前段号，parts[1] 是总段数
                        return $"{sortNum}/{parts[1]}";
                    }
                }
            }

            // 非文来文章，使用本地分段
            return $"{currentSegmentIndex + 1}/{segments.Count}";
        }

        /// <summary>
        /// 将文章分段
        /// </summary>
        private List<string> SplitIntoSegments(string content, int segmentLength)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrEmpty(content))
                return result;

            StringInfo si = new StringInfo(content);
            int totalLength = si.LengthInTextElements;
            int offset = 0;

            while (offset < totalLength)
            {
                int length = Math.Min(segmentLength, totalLength - offset);
                string segment = si.SubstringByTextElements(offset, length);
                result.Add(segment);
                offset += length;
            }

            return result;
        }

        /// <summary>
        /// 是否有文章
        /// </summary>
        public bool HasArticle()
        {
            return currentArticle != null && segments.Count > 0;
        }

        /// <summary>
        /// 是否是最后一段
        /// </summary>
        public bool IsLastSegment()
        {
            return currentSegmentIndex >= segments.Count - 1;
        }

        /// <summary>
        /// 是否是第一段
        /// </summary>
        public bool IsFirstSegment()
        {
            return currentSegmentIndex <= 0;
        }

        /// <summary>
        /// 获取当前段落的标记信息（来自文来接口的mark字段）
        /// </summary>
        /// <returns>段落标记，如 "1-34112"</returns>
        public string GetCurrentMark()
        {
            return articleMark;
        }

        /// <summary>
        /// 获取当前文章的难度描述（来自文来接口的difficulty字段）
        /// </summary>
        /// <returns>难度描述，如 "一般(2.05)"</returns>
        public string GetCurrentDifficulty()
        {
            return articleDifficulty;
        }

        /// <summary>
        /// 获取当前文章的难度名称（来自文来接口的custom_difficulty字段查询）
        /// </summary>
        /// <returns>难度名称，如 "简"、"普"、"难"</returns>
        public string GetCurrentDifficultyName()
        {
            return articleDifficultyName;
        }

        /// <summary>
        /// 获取当前书籍的ID（用于获取下一段/上一段）
        /// </summary>
        public int GetBookId()
        {
            return bookId;
        }

        /// <summary>
        /// 获取当前段号（用于获取下一段/上一段）
        /// </summary>
        public int GetSortNum()
        {
            return sortNum;
        }

        /// <summary>
        /// 获取当前难度ID（用于获取下一段/上一段）
        /// </summary>
        public int GetDifficultyId()
        {
            return difficultyId;
        }

        /// <summary>
        /// 根据难度ID获取难度名称（从API接口获取）
        /// </summary>
        /// <returns>难度名称，如"简"、"普"、"难"等</returns>
        public string GetDifficultyName()
        {
            if (difficultyId > 0)
            {
                // 使用 ArticleFetcher 获取难度列表
                var difficulties = ArticleFetcher.GetDifficulties();

                // 如果缓存为空，尝试同步加载
                if (difficulties.Count == 0)
                {
                    // 同步加载难度列表
                    var task = ArticleFetcher.GetDifficultiesAsync();
                    task.Wait();  // 等待异步完成
                    difficulties = task.Result;
                }

                var difficulty = difficulties.FirstOrDefault(d => d.Id == difficultyId);
                if (difficulty != null)
                {
                    return difficulty.Name;
                }
            }

            // 如果没有难度ID，返回空字符串
            return "";
        }
    }
}
