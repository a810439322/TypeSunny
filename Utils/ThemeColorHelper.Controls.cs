using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace TypeSunny.Utils
{
    internal static partial class ThemeColorHelper
    {
        private static ControlTemplate _cachedContextMenuTemplate;

        private static ControlTemplate GetContextMenuTemplate()
        {
            if (_cachedContextMenuTemplate != null)
                return _cachedContextMenuTemplate;

            const string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ContextMenu'>
    <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' SnapsToDevicePixels='True'>
        <ItemsPresenter Margin='0' KeyboardNavigation.DirectionalNavigation='Cycle' SnapsToDevicePixels='{TemplateBinding SnapsToDevicePixels}'/>
    </Border>
</ControlTemplate>";

            _cachedContextMenuTemplate = (ControlTemplate)XamlReader.Parse(xaml);
            _cachedContextMenuTemplate.Seal();
            return _cachedContextMenuTemplate;
        }

        private static ControlTemplate GetMenuItemTemplate(Color hoverBackground)
        {
            string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='MenuItem'>
    <ControlTemplate.Resources>
        <SolidColorBrush x:Key='HoverBackground' Color='" + ToXamlColor(hoverBackground) + @"'/>
    </ControlTemplate.Resources>
    <Border x:Name='Root' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'>
        <Grid VerticalAlignment='Center'>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
            </Grid.ColumnDefinitions>
            <TextBlock x:Name='Check' Grid.Column='0' Text='&#x2713;&#x2002;' VerticalAlignment='Center' Visibility='Collapsed'/>
            <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center'/>
            <Path x:Name='Arrow' Grid.Column='2' Data='M0,0 L4,4 L0,8 Z' Fill='{TemplateBinding Foreground}' Margin='12,0,0,0' VerticalAlignment='Center' Visibility='Collapsed'/>
            <Popup x:Name='PART_Popup' AllowsTransparency='True' Focusable='False' IsOpen='{Binding Path=IsSubmenuOpen, RelativeSource={RelativeSource Mode=TemplatedParent}}' Placement='Right' PopupAnimation='None'>
                <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='0' SnapsToDevicePixels='True'>
                    <ItemsPresenter/>
                </Border>
            </Popup>
        </Grid>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='HasItems' Value='True'>
            <Setter TargetName='Arrow' Property='Visibility' Value='Visible'/>
        </Trigger>
        <Trigger Property='IsChecked' Value='True'>
            <Setter TargetName='Check' Property='Visibility' Value='Visible'/>
        </Trigger>
        <Trigger Property='IsHighlighted' Value='True'>
            <Setter TargetName='Root' Property='Background' Value='{StaticResource HoverBackground}'/>
        </Trigger>
        <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='Root' Property='Background' Value='{StaticResource HoverBackground}'/>
        </Trigger>
        <Trigger Property='IsEnabled' Value='False'>
            <Setter TargetName='Root' Property='Opacity' Value='0.5'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";

            var template = (ControlTemplate)XamlReader.Parse(xaml);
            template.Seal();
            return template;
        }

        public static Style CreateMenuItemStyle(SolidColorBrush menuBg, SolidColorBrush menuFg)
        {
            var style = new Style(typeof(MenuItem));

            var hoverBgBrush = new SolidColorBrush(GetMenuItemHoverBorderColor(menuBg.Color));
            hoverBgBrush.Freeze();
            var borderBrush = CreateSubtleBorderBrush(menuBg);
            borderBrush.Freeze();

            style.Setters.Add(new Setter(MenuItem.TemplateProperty, GetMenuItemTemplate(hoverBgBrush.Color)));
            style.Setters.Add(new Setter(MenuItem.BackgroundProperty, menuBg));
            style.Setters.Add(new Setter(MenuItem.ForegroundProperty, menuFg));
            style.Setters.Add(new Setter(MenuItem.BorderBrushProperty, borderBrush));
            style.Setters.Add(new Setter(MenuItem.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(MenuItem.PaddingProperty, new Thickness(8, 6, 8, 6)));

            return style;
        }

        public static Separator CreateStyledSeparator(SolidColorBrush menuBg)
        {
            return new Separator { Style = BuildSeparatorStyle(menuBg) };
        }

        public static void ApplyContextMenuTheme(ContextMenu menu, SolidColorBrush menuBg, SolidColorBrush menuFg)
        {
            if (menu == null)
                return;

            menu.Background = menuBg;
            menu.Foreground = menuFg;
            menu.BorderBrush = CreateSubtleBorderBrush(menuBg);
            menu.BorderThickness = new Thickness(1);
            menu.Template = GetContextMenuTemplate();

            var itemStyle = CreateMenuItemStyle(menuBg, menuFg);
            var separatorStyle = BuildSeparatorStyle(menuBg);

            // 注入到 ContextMenu.Resources 作隐式样式，覆盖延迟创建/克隆的子菜单项
            menu.Resources[typeof(MenuItem)] = itemStyle;
            // dotnet/wpf#7268: Menu 内的 Separator 不应用 typeof(Separator) 隐式 Style，必须用 MenuItem.SeparatorStyleKey
            menu.Resources[MenuItem.SeparatorStyleKey] = separatorStyle;
            menu.Resources[typeof(Separator)] = separatorStyle;

            foreach (var item in menu.Items)
                ApplyMenuItemThemeRecursive(item, itemStyle, separatorStyle);
        }

        private static void ApplyMenuItemThemeRecursive(
            object item,
            Style itemStyle,
            Style separatorStyle)
        {
            if (item is MenuItem mi)
            {
                mi.Style = itemStyle;

                foreach (var child in mi.Items)
                    ApplyMenuItemThemeRecursive(child, itemStyle, separatorStyle);
            }
            else if (item is Separator sep)
            {
                sep.Style = separatorStyle;
            }
        }

        private static string ToXamlColor(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B);
        }

        private static Style BuildSeparatorStyle(SolidColorBrush menuBg)
        {
            var style = new Style(typeof(Separator));
            var separatorBrush = new SolidColorBrush(GetMenuSeparatorColor(menuBg.Color));
            separatorBrush.Freeze();

            var lineFactory = new FrameworkElementFactory(typeof(Border));
            lineFactory.SetValue(FrameworkElement.HeightProperty, 1.0);
            lineFactory.SetValue(Border.BackgroundProperty, separatorBrush);
            lineFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            lineFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            lineFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0));

            var rootFactory = new FrameworkElementFactory(typeof(Border));
            rootFactory.SetValue(Border.BackgroundProperty, menuBg);
            rootFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            rootFactory.SetValue(Control.PaddingProperty, new Thickness(0));
            rootFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            rootFactory.SetValue(FrameworkElement.HeightProperty, 9.0);
            rootFactory.AppendChild(lineFactory);

            var template = new ControlTemplate(typeof(Separator)) { VisualTree = rootFactory };

            // OverridesDefaultStyle 拒绝 theme 里 MenuItem.SeparatorStyleKey 的默认 Style 干扰
            style.Setters.Add(new Setter(FrameworkElement.OverridesDefaultStyleProperty, true));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 9.0));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, menuBg));
            return style;
        }

        public static void ApplyConfigListTheme(ListBox listBox, SolidColorBrush windowBg, SolidColorBrush windowFg)
        {
            if (listBox == null)
                return;

            var elevatedBg = CreateElevatedBackground(windowBg);
            var hoverBg = CreateHoverBackground(windowBg);
            var selectedBg = CreateSelectedBackground(windowBg);
            var borderBrush = CreateSubtleBorderBrush(windowBg);

            listBox.Background = elevatedBg;
            listBox.Foreground = windowFg;
            listBox.BorderBrush = borderBrush;
            listBox.BorderThickness = new Thickness(1);

            listBox.ItemContainerStyle = BuildConfigListItemStyle(windowFg, hoverBg, selectedBg);
        }

        public static SolidColorBrush GetSecondaryTextBrush(SolidColorBrush background)
        {
            var brush = new SolidColorBrush(GetSecondaryTextColor(background.Color));
            brush.Freeze();
            return brush;
        }

        public static SolidColorBrush GetReadableHighlightBrush(SolidColorBrush background)
        {
            var brush = new SolidColorBrush(GetReadableHighlightColor(background.Color));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush CreateElevatedBackground(SolidColorBrush windowBg)
        {
            var c = windowBg.Color;
            int delta = IsDark(c) ? 20 : -10;
            var brush = new SolidColorBrush(Color.FromArgb(
                c.A,
                ClampToByte(c.R + delta),
                ClampToByte(c.G + delta),
                ClampToByte(c.B + delta)));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush CreateHoverBackground(SolidColorBrush windowBg)
        {
            var c = windowBg.Color;
            int delta = IsDark(c) ? 35 : -20;
            var brush = new SolidColorBrush(Color.FromArgb(
                c.A,
                ClampToByte(c.R + delta),
                ClampToByte(c.G + delta),
                ClampToByte(c.B + delta)));
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush CreateSelectedBackground(SolidColorBrush windowBg)
        {
            var c = windowBg.Color;
            int delta = IsDark(c) ? 55 : -35;
            var brush = new SolidColorBrush(Color.FromArgb(
                c.A,
                ClampToByte(c.R + delta),
                ClampToByte(c.G + delta),
                ClampToByte(c.B + delta)));
            brush.Freeze();
            return brush;
        }

        private static Style BuildConfigListItemStyle(
            SolidColorBrush fg,
            SolidColorBrush hoverBg,
            SolidColorBrush selectedBg)
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, fg));
            style.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(4, 2, 4, 2)));

            var hoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, hoverBg));
            style.Triggers.Add(hoverTrigger);

            var selectedTrigger = new Trigger
            {
                Property = ListBoxItem.IsSelectedProperty,
                Value = true
            };
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, selectedBg));
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, fg));
            style.Triggers.Add(selectedTrigger);

            return style;
        }
    }
}
