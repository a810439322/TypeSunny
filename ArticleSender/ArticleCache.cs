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
        private string articleDifficulty;  // 保存文章的难度

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
            articleDifficulty = article.Difficulty ?? "";  // 保存难度
            segmentLength = Config.GetInt("文来字数");

            if (segmentLength <= 0)
                segmentLength = 500; // 默认500字

            // 将文章分段
            segments = SplitIntoSegments(article.FullContent, segmentLength);
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
        /// 获取当前文章的难度（来自文来接口的difficulty字段）
        /// </summary>
        /// <returns>难度，如 "一般(2.05)"</returns>
        public string GetCurrentDifficulty()
        {
            return articleDifficulty;
        }
    }
}
