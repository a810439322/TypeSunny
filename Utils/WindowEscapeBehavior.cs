using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TypeSunny.Utils
{
    internal static class WindowEscapeBehavior
    {
        internal static void EnableEscapeToClose(this Window window)
        {
            window.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape)
                    return;

                if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows | ModifierKeys.Shift)) != 0)
                    return;

                if (HandleEscape(window))
                    e.Handled = true;
            };
        }

        internal static bool HandleEscape(Window window)
        {
            var focused = Keyboard.FocusedElement;

            if (focused is ComboBox cb && cb.IsDropDownOpen)
                return false;

            if (focused is TextBox tb && !tb.IsReadOnly)
            {
                if (!string.IsNullOrEmpty(tb.Text))
                {
                    tb.Clear();
                    return true;
                }

                window.Focus();
                return true;
            }

            if (focused is PasswordBox pb)
            {
                if (!string.IsNullOrEmpty(pb.Password))
                {
                    pb.Clear();
                    return true;
                }

                window.Focus();
                return true;
            }

            window.Close();
            return true;
        }
    }
}
