using System;
using TypeSunny.UI;

namespace TypeSunny.Tests
{
    internal static class HomeBottomToolbarLayoutPolicyTests
    {
        private static int Main()
        {
            try
            {
                UsesCompactLayoutWhenEveryFunctionButtonIsHidden();
                KeepsNormalLayoutWhenAnyFeatureButtonIsVisible();
                KeepsNormalLayoutWhenLocalArticleModuleIsVisible();
                UsesZeroReservedHeightForExpandedCompactLayout();
                UsesCompactReservedHeightForCollapsedCompactLayout();
                UsesZeroReservedHeightForSuperCompactLayout();
                UsesNormalReservedHeightForNormalLayout();
                CreatesCollapsedCompactPlanWhenEveryFunctionButtonIsHidden();
                CreatesExpandedCompactPlanWithoutReservedToolbarSpace();
                CreatesNormalPlanWhenFeatureButtonIsVisible();
                CreatesNormalPlanWhenLocalArticleModuleIsVisible();
                CreatesSuperCompactPlan();

                Console.WriteLine("All HomeBottomToolbarLayoutPolicy tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void UsesCompactLayoutWhenEveryFunctionButtonIsHidden()
        {
            AssertEqual(
                "all function buttons hidden",
                HomeBottomToolbarLayoutMode.Compact,
                HomeBottomToolbarLayoutPolicy.GetLayoutMode(0, false));
        }

        private static void KeepsNormalLayoutWhenAnyFeatureButtonIsVisible()
        {
            AssertEqual(
                "feature button visible",
                HomeBottomToolbarLayoutMode.Normal,
                HomeBottomToolbarLayoutPolicy.GetLayoutMode(1, false));
        }

        private static void KeepsNormalLayoutWhenLocalArticleModuleIsVisible()
        {
            AssertEqual(
                "local article module visible",
                HomeBottomToolbarLayoutMode.Normal,
                HomeBottomToolbarLayoutPolicy.GetLayoutMode(0, true));
        }

        private static void UsesZeroReservedHeightForExpandedCompactLayout()
        {
            AssertEqual(
                "expanded compact reserved height",
                0,
                HomeBottomToolbarLayoutPolicy.GetReservedHeight(
                    HomeBottomToolbarLayoutMode.Compact,
                    true,
                    30,
                    15));
        }

        private static void UsesCompactReservedHeightForCollapsedCompactLayout()
        {
            AssertEqual(
                "collapsed compact reserved height",
                15,
                HomeBottomToolbarLayoutPolicy.GetReservedHeight(
                    HomeBottomToolbarLayoutMode.Compact,
                    false,
                    30,
                    15));
        }

        private static void UsesNormalReservedHeightForNormalLayout()
        {
            AssertEqual(
                "normal reserved height",
                30,
                HomeBottomToolbarLayoutPolicy.GetReservedHeight(
                    HomeBottomToolbarLayoutMode.Normal,
                    false,
                    30,
                    15));
        }

        private static void UsesZeroReservedHeightForSuperCompactLayout()
        {
            AssertEqual(
                "super compact reserved height",
                0,
                HomeBottomToolbarLayoutPolicy.GetReservedHeight(
                    HomeBottomToolbarLayoutMode.SuperCompact,
                    false,
                    30,
                    15));
        }

        private static void CreatesCollapsedCompactPlanWhenEveryFunctionButtonIsHidden()
        {
            var plan = HomeBottomToolbarLayoutPolicy.CreatePlan(
                isSuperCompact: false,
                isResultsExpanded: false,
                visibleFeatureButtonCount: 0,
                hasVisibleLocalArticleModule: false,
                normalToolbarHeight: 32,
                compactCollapsedToolbarHeight: 15,
                normalCollapsedBottomBorderHeight: 10);

            AssertEqual("collapsed compact plan mode", HomeBottomToolbarLayoutMode.Compact, plan.Mode);
            AssertEqual("collapsed compact toolbar height", 15, plan.ToolbarReservedHeight);
            AssertEqual("collapsed compact bottom border height", 0, plan.CollapsedBottomBorderHeight);
            AssertEqual("collapsed compact window footer height", 15, plan.CollapsedWindowFooterHeight);
            AssertEqual("collapsed compact panel visible", true, plan.IsToolbarPanelVisible);
            AssertEqual("collapsed compact toggle host", true, plan.UseCompactToggleHost);
        }

        private static void CreatesExpandedCompactPlanWithoutReservedToolbarSpace()
        {
            var plan = HomeBottomToolbarLayoutPolicy.CreatePlan(
                isSuperCompact: false,
                isResultsExpanded: true,
                visibleFeatureButtonCount: 0,
                hasVisibleLocalArticleModule: false,
                normalToolbarHeight: 32,
                compactCollapsedToolbarHeight: 15,
                normalCollapsedBottomBorderHeight: 10);

            AssertEqual("expanded compact plan mode", HomeBottomToolbarLayoutMode.Compact, plan.Mode);
            AssertEqual("expanded compact toolbar height", 0, plan.ToolbarReservedHeight);
            AssertEqual("expanded compact bottom border height", 0, plan.CollapsedBottomBorderHeight);
            AssertEqual("expanded compact window footer height", 15, plan.CollapsedWindowFooterHeight);
            AssertEqual("expanded compact panel visible", true, plan.IsToolbarPanelVisible);
            AssertEqual("expanded compact toggle host", true, plan.UseCompactToggleHost);
        }

        private static void CreatesNormalPlanWhenFeatureButtonIsVisible()
        {
            var plan = HomeBottomToolbarLayoutPolicy.CreatePlan(
                isSuperCompact: false,
                isResultsExpanded: false,
                visibleFeatureButtonCount: 1,
                hasVisibleLocalArticleModule: false,
                normalToolbarHeight: 32,
                compactCollapsedToolbarHeight: 15,
                normalCollapsedBottomBorderHeight: 10);

            AssertEqual("feature normal plan mode", HomeBottomToolbarLayoutMode.Normal, plan.Mode);
            AssertEqual("feature normal toolbar height", 32, plan.ToolbarReservedHeight);
            AssertEqual("feature normal bottom border height", 10, plan.CollapsedBottomBorderHeight);
            AssertEqual("feature normal window footer height", 10, plan.CollapsedWindowFooterHeight);
            AssertEqual("feature normal panel visible", true, plan.IsToolbarPanelVisible);
            AssertEqual("feature normal toggle host", false, plan.UseCompactToggleHost);
        }

        private static void CreatesNormalPlanWhenLocalArticleModuleIsVisible()
        {
            var plan = HomeBottomToolbarLayoutPolicy.CreatePlan(
                isSuperCompact: false,
                isResultsExpanded: false,
                visibleFeatureButtonCount: 0,
                hasVisibleLocalArticleModule: true,
                normalToolbarHeight: 32,
                compactCollapsedToolbarHeight: 15,
                normalCollapsedBottomBorderHeight: 10);

            AssertEqual("local article normal plan mode", HomeBottomToolbarLayoutMode.Normal, plan.Mode);
            AssertEqual("local article normal toolbar height", 32, plan.ToolbarReservedHeight);
            AssertEqual("local article normal toggle host", false, plan.UseCompactToggleHost);
        }

        private static void CreatesSuperCompactPlan()
        {
            var plan = HomeBottomToolbarLayoutPolicy.CreatePlan(
                isSuperCompact: true,
                isResultsExpanded: false,
                visibleFeatureButtonCount: 1,
                hasVisibleLocalArticleModule: true,
                normalToolbarHeight: 32,
                compactCollapsedToolbarHeight: 15,
                normalCollapsedBottomBorderHeight: 10);

            AssertEqual("super compact plan mode", HomeBottomToolbarLayoutMode.SuperCompact, plan.Mode);
            AssertEqual("super compact toolbar height", 0, plan.ToolbarReservedHeight);
            AssertEqual("super compact bottom border height", 0, plan.CollapsedBottomBorderHeight);
            AssertEqual("super compact window footer height", 15, plan.CollapsedWindowFooterHeight);
            AssertEqual("super compact panel visible", false, plan.IsToolbarPanelVisible);
            AssertEqual("super compact toggle host", true, plan.UseCompactToggleHost);
        }

        private static void AssertEqual(
            string name,
            HomeBottomToolbarLayoutMode expected,
            HomeBottomToolbarLayoutMode actual)
        {
            if (expected != actual)
                throw new Exception(name + " expected " + expected + ", got " + actual + ".");
        }

        private static void AssertEqual(
            string name,
            double expected,
            double actual)
        {
            if (Math.Abs(expected - actual) > 0.01)
                throw new Exception(name + " expected " + expected + ", got " + actual + ".");
        }

        private static void AssertEqual(
            string name,
            bool expected,
            bool actual)
        {
            if (expected != actual)
                throw new Exception(name + " expected " + expected + ", got " + actual + ".");
        }
    }
}
