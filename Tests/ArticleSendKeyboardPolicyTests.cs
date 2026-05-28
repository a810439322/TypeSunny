using System;
using TypeSunny.UI;

namespace TypeSunny.Tests
{
    internal static class ArticleSendKeyboardPolicyTests
    {
        private static int Main()
        {
            try
            {
                EnterSendsWhenNoArticleSelectionIsPending();
                UpThenEnterConfirmsBeforeNextEnterSends();
                DownThenSpaceConfirmsBeforeNextEnterSends();

                Console.WriteLine("All ArticleSendKeyboardPolicy tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void EnterSendsWhenNoArticleSelectionIsPending()
        {
            var policy = new ArticleSendKeyboardPolicy();

            AssertEqual("plain enter", ArticleSendKeyboardAction.SendArticle, policy.HandleKey(ArticleSendKeyboardKey.Enter));
        }

        private static void UpThenEnterConfirmsBeforeNextEnterSends()
        {
            var policy = new ArticleSendKeyboardPolicy();

            AssertEqual("up selects previous article", ArticleSendKeyboardAction.SelectPreviousArticle, policy.HandleKey(ArticleSendKeyboardKey.Up));
            AssertEqual("enter confirms selected article", ArticleSendKeyboardAction.ConfirmArticleSelection, policy.HandleKey(ArticleSendKeyboardKey.Enter));
            AssertEqual("second enter sends article", ArticleSendKeyboardAction.SendArticle, policy.HandleKey(ArticleSendKeyboardKey.Enter));
        }

        private static void DownThenSpaceConfirmsBeforeNextEnterSends()
        {
            var policy = new ArticleSendKeyboardPolicy();

            AssertEqual("down selects next article", ArticleSendKeyboardAction.SelectNextArticle, policy.HandleKey(ArticleSendKeyboardKey.Down));
            AssertEqual("space confirms selected article", ArticleSendKeyboardAction.ConfirmArticleSelection, policy.HandleKey(ArticleSendKeyboardKey.Space));
            AssertEqual("enter after space confirm sends article", ArticleSendKeyboardAction.SendArticle, policy.HandleKey(ArticleSendKeyboardKey.Enter));
        }

        private static void AssertEqual(string name, ArticleSendKeyboardAction expected, ArticleSendKeyboardAction actual)
        {
            if (actual != expected)
                throw new Exception(name + " expected " + expected + ", got " + actual + ".");
        }
    }
}
