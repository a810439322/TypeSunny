using System.Windows;
using System.Windows.Controls;

namespace TypeSunny.UI
{
    internal static class TitleBarButtonIcons
    {
        private const string MinimizeStyleKey = "SunnyTitleBarMinimizeButtonStyle";
        private const string CloseStyleKey = "SunnyTitleBarCloseIconButtonStyle";
        private const string MaximizeStyleKey = "SunnyTitleBarMaximizeButtonStyle";
        private const string RestoreStyleKey = "SunnyTitleBarRestoreButtonStyle";

        public static void ApplyMinimizeButtonStyle(Button button)
        {
            ApplyStyle(button, MinimizeStyleKey);
        }

        public static void ApplyCloseButtonStyle(Button button)
        {
            ApplyStyle(button, CloseStyleKey);
        }

        public static void SetMaximizeButtonState(Button button, bool isMaximized)
        {
            var resourceKey = isMaximized ? RestoreStyleKey : MaximizeStyleKey;
            ApplyStyle(button, resourceKey);
        }

        private static void ApplyStyle(Button button, string resourceKey)
        {
            if (button == null)
                return;

            var style = button.TryFindResource(resourceKey) as Style;
            if (style != null)
                button.Style = style;
        }
    }
}
