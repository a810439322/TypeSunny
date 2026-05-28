namespace TypeSunny.UI
{
    internal enum ArticleSendKeyboardAction
    {
        None,
        SelectPreviousArticle,
        SelectNextArticle,
        ConfirmArticleSelection,
        SendArticle
    }

    internal sealed class ArticleSendKeyboardPolicy
    {
        private bool _awaitingArticleSelectionConfirmation;

        public ArticleSendKeyboardAction HandleKey(ArticleSendKeyboardKey key)
        {
            switch (key)
            {
                case ArticleSendKeyboardKey.Up:
                    _awaitingArticleSelectionConfirmation = true;
                    return ArticleSendKeyboardAction.SelectPreviousArticle;

                case ArticleSendKeyboardKey.Down:
                    _awaitingArticleSelectionConfirmation = true;
                    return ArticleSendKeyboardAction.SelectNextArticle;

                case ArticleSendKeyboardKey.Enter:
                    if (_awaitingArticleSelectionConfirmation)
                    {
                        _awaitingArticleSelectionConfirmation = false;
                        return ArticleSendKeyboardAction.ConfirmArticleSelection;
                    }
                    return ArticleSendKeyboardAction.SendArticle;

                case ArticleSendKeyboardKey.Space:
                    if (_awaitingArticleSelectionConfirmation)
                    {
                        _awaitingArticleSelectionConfirmation = false;
                        return ArticleSendKeyboardAction.ConfirmArticleSelection;
                    }
                    return ArticleSendKeyboardAction.None;

                default:
                    return ArticleSendKeyboardAction.None;
            }
        }
    }

    internal enum ArticleSendKeyboardKey
    {
        Up,
        Down,
        Enter,
        Space
    }
}
