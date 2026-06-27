namespace TypeSunny.UI
{
    internal enum HomeBottomToolbarLayoutMode
    {
        Normal,
        Compact,
        SuperCompact
    }

    internal sealed class HomeBottomToolbarLayoutPlan
    {
        public HomeBottomToolbarLayoutPlan(
            HomeBottomToolbarLayoutMode mode,
            double toolbarReservedHeight,
            double collapsedBottomBorderHeight,
            double collapsedWindowFooterHeight,
            bool isToolbarPanelVisible,
            bool useCompactToggleHost)
        {
            Mode = mode;
            ToolbarReservedHeight = toolbarReservedHeight;
            CollapsedBottomBorderHeight = collapsedBottomBorderHeight;
            CollapsedWindowFooterHeight = collapsedWindowFooterHeight;
            IsToolbarPanelVisible = isToolbarPanelVisible;
            UseCompactToggleHost = useCompactToggleHost;
        }

        public HomeBottomToolbarLayoutMode Mode { get; }

        public double ToolbarReservedHeight { get; }

        public double CollapsedBottomBorderHeight { get; }

        public double CollapsedWindowFooterHeight { get; }

        public bool IsToolbarPanelVisible { get; }

        public bool UseCompactToggleHost { get; }
    }

    internal static class HomeBottomToolbarLayoutPolicy
    {
        public static HomeBottomToolbarLayoutPlan CreatePlan(
            bool isSuperCompact,
            bool isResultsExpanded,
            int visibleFeatureButtonCount,
            bool hasVisibleLocalArticleModule,
            double normalToolbarHeight,
            double compactCollapsedToolbarHeight,
            double normalCollapsedBottomBorderHeight)
        {
            if (isSuperCompact)
            {
                return new HomeBottomToolbarLayoutPlan(
                    HomeBottomToolbarLayoutMode.SuperCompact,
                    toolbarReservedHeight: 0,
                    collapsedBottomBorderHeight: 0,
                    collapsedWindowFooterHeight: compactCollapsedToolbarHeight,
                    isToolbarPanelVisible: false,
                    useCompactToggleHost: true);
            }

            var layoutMode = GetLayoutMode(visibleFeatureButtonCount, hasVisibleLocalArticleModule);
            if (layoutMode == HomeBottomToolbarLayoutMode.Compact)
            {
                return new HomeBottomToolbarLayoutPlan(
                    layoutMode,
                    toolbarReservedHeight: isResultsExpanded ? 0 : compactCollapsedToolbarHeight,
                    collapsedBottomBorderHeight: 0,
                    collapsedWindowFooterHeight: compactCollapsedToolbarHeight,
                    isToolbarPanelVisible: true,
                    useCompactToggleHost: true);
            }

            return new HomeBottomToolbarLayoutPlan(
                HomeBottomToolbarLayoutMode.Normal,
                toolbarReservedHeight: normalToolbarHeight,
                collapsedBottomBorderHeight: isResultsExpanded ? 0 : normalCollapsedBottomBorderHeight,
                collapsedWindowFooterHeight: normalCollapsedBottomBorderHeight,
                isToolbarPanelVisible: true,
                useCompactToggleHost: false);
        }

        public static HomeBottomToolbarLayoutMode GetLayoutMode(
            int visibleFeatureButtonCount,
            bool hasVisibleLocalArticleModule)
        {
            return visibleFeatureButtonCount > 0 || hasVisibleLocalArticleModule
                ? HomeBottomToolbarLayoutMode.Normal
                : HomeBottomToolbarLayoutMode.Compact;
        }

        public static double GetReservedHeight(
            HomeBottomToolbarLayoutMode layoutMode,
            bool isResultsExpanded,
            double normalToolbarHeight,
            double compactCollapsedToolbarHeight)
        {
            if (layoutMode == HomeBottomToolbarLayoutMode.SuperCompact)
                return 0;

            if (layoutMode == HomeBottomToolbarLayoutMode.Compact)
                return isResultsExpanded ? 0 : compactCollapsedToolbarHeight;

            return normalToolbarHeight;
        }
    }
}
