param(
    [string]$Configuration = 'Debug'
)

if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -eq $pwsh) {
        throw 'HomeBottomToolbarDynamicLayoutTests requires pwsh so UTF-8 literals are parsed correctly.'
    }

    & $pwsh.Source -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Configuration $Configuration
    exit $LASTEXITCODE
}

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $root 'bin\HomeBottomToolbarDynamicLayoutTests'
$testDriveRoot = Join-Path $buildDir 'TestDrive'
$testRunId = [Guid]::NewGuid().ToString('N')
$harnessDir = Join-Path $testDriveRoot $testRunId
$harnessSource = Join-Path $harnessDir 'HomeBottomToolbarDynamicLayoutHarness.cs'
$testProject = Join-Path $root "TypeSunny.HomeBottomToolbarDynamicLayoutTests.$testRunId.csproj"
$appExe = Join-Path $buildDir '晴跟打.exe'

$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    throw "MSBuild not found at $msbuild"
}

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
New-Item -ItemType Directory -Force -Path $harnessDir | Out-Null

$source = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TypeSunny;
using TypeSunny.UI;

internal static class HomeBottomToolbarDynamicLayoutHarness
{
    private static readonly string[] FeatureKeys =
    {
        "显示首页文来",
        "显示首页练单",
        "显示首页晴双拼",
        "显示首页赛文"
    };

    private static int _failures;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Expected scenario name.");

        try
        {
            return Run(args[0]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int Run(string scenario)
    {
        var app = new App();
        app.InitializeComponent();

        ResetConfig();

        if (scenario == "scoped-visible-expanded-to-hidden-collapsed")
            return RunScopedVisibleExpandedToHiddenCollapsed(app);
        if (scenario == "settings-visible-to-hidden-collapsed")
            return RunSettingsVisibleToHiddenCollapsed(app);
        if (scenario == "settings-hidden-to-visible-collapsed")
            return RunSettingsHiddenToVisibleCollapsed(app);
        if (scenario == "settings-last-feature-to-hidden-collapsed")
            return RunSettingsLastFeatureToHiddenCollapsed(app);
        if (scenario == "settings-hidden-to-first-local-collapsed")
            return RunSettingsHiddenToFirstLocalCollapsed(app);
        if (scenario == "settings-repeated-last-feature-toggle-collapsed")
            return RunSettingsRepeatedLastFeatureToggleCollapsed(app);
        if (scenario == "settings-all-home-buttons-visible-to-hidden-collapsed")
            return RunSettingsAllHomeButtonsVisibleToHiddenCollapsed(app);
        if (scenario == "settings-all-home-buttons-hidden-to-first-bottom-collapsed")
            return RunSettingsAllHomeButtonsHiddenToFirstBottomCollapsed(app);
        if (scenario == "super-compact-exit-restores-bottom-toolbar-collapsed")
            return RunSuperCompactExitRestoresBottomToolbarCollapsed(app);
        if (scenario == "scoped-settings-visible-to-hidden-collapsed")
            return RunScopedSettingsVisibleToHiddenCollapsed(app);
        if (scenario == "scoped-settings-hidden-to-visible-collapsed")
            return RunScopedSettingsHiddenToVisibleCollapsed(app);

        bool isResultsExpanded;
        if (scenario.EndsWith("-expanded", StringComparison.Ordinal))
            isResultsExpanded = true;
        else if (scenario.EndsWith("-collapsed", StringComparison.Ordinal))
            isResultsExpanded = false;
        else
            throw new ArgumentException("Scenario must end with -expanded or -collapsed: " + scenario);

        string baseScenario = scenario
            .Replace("-expanded", string.Empty)
            .Replace("-collapsed", string.Empty);

        Config.dicts["成绩面板展开"] = isResultsExpanded ? "是" : "否";

        if (baseScenario == "visible-to-hidden")
            SetBottomButtonsVisible(true);
        else if (baseScenario == "hidden-to-visible")
            SetBottomButtonsVisible(false);
        else
            throw new ArgumentException("Unknown scenario: " + scenario);

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        if (baseScenario == "visible-to-hidden")
        {
            AssertNormal("startup with bottom buttons", window, isResultsExpanded);
            double beforeWindowHeight = window.Height;
            SetBottomButtonsVisible(false);
            window.ApplyHomeToolbarSettings();
            DrainDispatcher();
            window.UpdateLayout();
            AssertCompact("after hiding every bottom button", window, isResultsExpanded);
            Expect(
                "hiding every bottom button shrinks the window footer",
                window.Height < beforeWindowHeight - 10);
        }
        else
        {
            AssertCompact("startup without bottom buttons", window, isResultsExpanded);
            double beforeWindowHeight = window.Height;
            SetBottomButtonsVisible(true);
            window.ApplyHomeToolbarSettings();
            DrainDispatcher();
            window.UpdateLayout();
            AssertNormal("after showing bottom buttons", window, isResultsExpanded);
            Expect(
                "showing bottom buttons expands the window footer",
                window.Height > beforeWindowHeight + 10);
        }

        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunSettingsRepeatedLastFeatureToggleCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "否";
        SetBottomButtonsVisible(false);
        Config.dicts["显示首页文来"] = "是";

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertNormal("settings repeated toggle startup with one feature button", window, false);
        double normalWindowHeight = window.Height;

        var settings = ShowHomeSettingsWindow(window);
        SetHomeSettingsChecked(settings, "文来", false);
        AssertCompact("settings repeated toggle after first hide", window, false);
        double compactWindowHeight = window.Height;
        Expect(
            "settings repeated toggle first hide shrinks footer",
            compactWindowHeight < normalWindowHeight - 10);

        SetHomeSettingsChecked(settings, "文来", true);
        AssertNormal("settings repeated toggle after first show", window, false);
        Expect(
            "settings repeated toggle first show restores normal footer",
            window.Height > compactWindowHeight + 10);

        SetHomeSettingsChecked(settings, "文来", false);
        AssertCompact("settings repeated toggle after second hide", window, false);
        Expect(
            "settings repeated toggle second hide shrinks footer again",
            window.Height < normalWindowHeight - 10);

        settings.Close();
        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunSuperCompactExitRestoresBottomToolbarCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "否";
        SetBottomButtonsVisible(true);

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertNormal("super compact exit startup with bottom buttons", window, false);
        var normal = Capture(window);
        double normalFooterHeight = normal.RowHeight;
        double normalBottomBorderHeight = normal.BottomBorderRowHeight;

        SetSuperCompact(window, true);
        AssertSuperCompact("after entering one-key compact", window);

        SetSuperCompact(window, false);
        AssertNormal("after exiting one-key compact", window, false);
        var restored = Capture(window);
        Expect(
            "one-key compact exit restores the bottom toolbar reserved height",
            Math.Abs(restored.RowHeight - normalFooterHeight) < 2 || restored.RowHeight >= 30.5);
        Expect(
            "one-key compact exit restores the collapsed bottom border height",
            Math.Abs(restored.BottomBorderRowHeight - normalBottomBorderHeight) < 0.5 || Math.Abs(restored.BottomBorderRowHeight - 10) < 0.5);

        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunSettingsAllHomeButtonsVisibleToHiddenCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "否";
        SetHomeButtonsVisible(true);

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertNormal("settings all-home startup with every home button", window, false);
        double beforeWindowHeight = window.Height;

        var settings = ShowHomeSettingsWindow(window);
        foreach (string label in new[] { "文来", "晴练单", "晴双拼", "赛文", "本地文章模块", "设置", "重打", "剪贴板载文", "群载文", "选群" })
            SetHomeSettingsChecked(settings, label, false);

        AssertCompact("settings all-home path after hiding every home button", window, false);
        Expect(
            "settings all-home path hiding every home button shrinks the window without restart",
            window.Height < beforeWindowHeight - 25);

        settings.Close();
        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunSettingsAllHomeButtonsHiddenToFirstBottomCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "否";
        SetHomeButtonsVisible(false);

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertCompact("settings all-home startup without home buttons", window, false);
        double beforeWindowHeight = window.Height;

        var settings = ShowHomeSettingsWindow(window);
        SetHomeSettingsChecked(settings, "文来", true);

        AssertNormal("settings all-home path after showing first bottom button", window, false);
        Expect(
            "settings all-home path showing first bottom button expands the window footer without restart",
            window.Height > beforeWindowHeight + 10);

        settings.Close();
        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunScopedSettingsVisibleToHiddenCollapsed(App app)
    {
        Config.dicts["练单主窗口单独记忆"] = "是";
        TrainerMainWindowConfigScope.EnterTrainerScope();
        try
        {
            Config.dicts["成绩面板展开"] = "否";
            SetBottomButtonsVisible(false);
            Config.dicts["练单场景_成绩面板展开"] = "否";
            SetScopedBottomButtonsVisible(true);

            var window = new MainWindow();
            app.MainWindow = window;
            window.Show();
            DrainDispatcher();
            window.UpdateLayout();

            AssertNormal("scoped settings startup with scoped bottom buttons", window, false);
            double beforeWindowHeight = window.Height;

            var settings = ShowHomeSettingsWindow(window);
            SetHomeSettingsChecked(settings, "文来", false);
            SetHomeSettingsChecked(settings, "晴练单", false);
            SetHomeSettingsChecked(settings, "晴双拼", false);
            SetHomeSettingsChecked(settings, "赛文", false);
            SetHomeSettingsChecked(settings, "本地文章模块", false);

            AssertCompact("scoped settings path after hiding every scoped bottom button", window, false);
            Expect(
                "scoped settings path hiding every bottom button shrinks the window footer without restart",
                window.Height < beforeWindowHeight - 10);

            settings.Close();
            window.Close();
            DrainDispatcher();
        }
        finally
        {
            TrainerMainWindowConfigScope.ExitTrainerScope();
        }

        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunScopedSettingsHiddenToVisibleCollapsed(App app)
    {
        Config.dicts["练单主窗口单独记忆"] = "是";
        TrainerMainWindowConfigScope.EnterTrainerScope();
        try
        {
            Config.dicts["成绩面板展开"] = "否";
            SetBottomButtonsVisible(true);
            Config.dicts["练单场景_成绩面板展开"] = "否";
            SetScopedBottomButtonsVisible(false);

            var window = new MainWindow();
            app.MainWindow = window;
            window.Show();
            DrainDispatcher();
            window.UpdateLayout();

            AssertCompact("scoped settings startup without scoped bottom buttons", window, false);
            double beforeWindowHeight = window.Height;

            var settings = ShowHomeSettingsWindow(window);
            SetHomeSettingsChecked(settings, "文来", true);

            AssertNormal("scoped settings path after showing first scoped bottom button", window, false);
            Expect(
                "scoped settings path showing first bottom button expands the window footer without restart",
                window.Height > beforeWindowHeight + 10);

            settings.Close();
            window.Close();
            DrainDispatcher();
        }
        finally
        {
            TrainerMainWindowConfigScope.ExitTrainerScope();
        }

        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunSettingsLastFeatureToHiddenCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "否";
        SetBottomButtonsVisible(false);
        Config.dicts["显示首页文来"] = "是";

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertNormal("settings startup with only one feature button", window, false);
        double beforeWindowHeight = window.Height;

        var settings = ShowHomeSettingsWindow(window);
        SetHomeSettingsChecked(settings, "文来", false);

        AssertCompact("settings path after hiding last feature button", window, false);
        Expect(
            "settings path hiding the last feature button shrinks the window footer without restart",
            window.Height < beforeWindowHeight - 10);

        settings.Close();
        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunSettingsHiddenToFirstLocalCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "否";
        SetBottomButtonsVisible(false);

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertCompact("settings startup without bottom buttons before local module", window, false);
        double beforeWindowHeight = window.Height;

        var settings = ShowHomeSettingsWindow(window);
        SetHomeSettingsChecked(settings, "本地文章模块", true);

        AssertNormal("settings path after showing first local module button group", window, false);
        Expect(
            "settings path showing the local module expands the window footer without restart",
            window.Height > beforeWindowHeight + 10);

        settings.Close();
        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunSettingsVisibleToHiddenCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "否";
        SetBottomButtonsVisible(true);

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertNormal("settings startup with bottom buttons", window, false);
        double beforeWindowHeight = window.Height;

        var settings = ShowHomeSettingsWindow(window);
        SetHomeSettingsChecked(settings, "文来", false);
        AssertNormal("settings path after hiding wenlai while other buttons remain", window, false);
        SetHomeSettingsChecked(settings, "晴练单", false);
        AssertNormal("settings path after hiding trainer while other buttons remain", window, false);
        SetHomeSettingsChecked(settings, "晴双拼", false);
        AssertNormal("settings path after hiding shuang while race remains", window, false);
        SetHomeSettingsChecked(settings, "赛文", false);
        AssertNormal("settings path after hiding all feature buttons while local remains", window, false);
        SetHomeSettingsChecked(settings, "本地文章模块", false);

        AssertCompact("settings path after hiding every bottom button", window, false);
        Expect(
            "settings path hiding every bottom button shrinks the window footer without restart",
            window.Height < beforeWindowHeight - 10);

        settings.Close();
        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunSettingsHiddenToVisibleCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "否";
        SetBottomButtonsVisible(false);

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertCompact("settings startup without bottom buttons", window, false);
        double beforeWindowHeight = window.Height;

        var settings = ShowHomeSettingsWindow(window);
        SetHomeSettingsChecked(settings, "文来", true);

        AssertNormal("settings path after showing first bottom button", window, false);
        Expect(
            "settings path showing first bottom button expands the window footer without restart",
            window.Height > beforeWindowHeight + 10);

        settings.Close();
        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static int RunScopedVisibleExpandedToHiddenCollapsed(App app)
    {
        Config.dicts["成绩面板展开"] = "是";
        SetBottomButtonsVisible(true);

        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        DrainDispatcher();
        window.UpdateLayout();

        AssertNormal("scoped startup with buttons and expanded results", window, true);

        SetBottomButtonsVisible(false);
        Config.dicts["成绩面板展开"] = "否";
        InvokeApplyScopedMainWindowState(window);
        DrainDispatcher();
        window.UpdateLayout();

        AssertCompact("after scoped state hides buttons and collapses results", window, false);

        window.Close();
        DrainDispatcher();
        app.Shutdown();
        return _failures == 0 ? 0 : 1;
    }

    private static void ResetConfig()
    {
        TrainerMainWindowConfigScope.ExitTrainerScope();
        foreach (string scopedKey in TrainerMainWindowConfigScope.GetAllTrainerScopedKeys().ToList())
            Config.dicts.Remove(scopedKey);

        Config.dicts["窗口高度"] = "750.4";
        Config.dicts["窗口宽度"] = "966.4";
        Config.dicts["窗口坐标X"] = "100";
        Config.dicts["窗口坐标Y"] = "100";
        Config.dicts["成绩面板展开"] = "否";
        Config.dicts["一键极简"] = "否";
        Config.dicts["练单主窗口单独记忆"] = "否";
        Config.dicts["首页功能按钮顺序"] = "文来,晴练单,晴双拼,赛文";
        Config.dicts["显示首页设置"] = "否";
        Config.dicts["显示首页重打"] = "否";
        Config.dicts["显示首页剪贴板载文"] = "否";
        Config.dicts["显示首页群载文"] = "否";
        Config.dicts["显示首页选群"] = "否";
    }

    private static void SetBottomButtonsVisible(bool visible)
    {
        string value = visible ? "是" : "否";
        foreach (string key in FeatureKeys)
            Config.dicts[key] = value;

        Config.dicts["显示首页本地文章"] = value;
    }

    private static void SetHomeButtonsVisible(bool visible)
    {
        string value = visible ? "是" : "否";
        SetBottomButtonsVisible(visible);
        Config.dicts["显示首页设置"] = value;
        Config.dicts["显示首页重打"] = value;
        Config.dicts["显示首页剪贴板载文"] = value;
        Config.dicts["显示首页群载文"] = value;
        Config.dicts["显示首页选群"] = value;
    }

    private static void SetSuperCompact(MainWindow window, bool enabled)
    {
        var item = Find<System.Windows.Controls.MenuItem>(window, "MenuHomeSuperCompact");
        item.IsChecked = enabled;
        var method = typeof(MainWindow).GetMethod(
            "MenuHomeSuperCompact_Click",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new InvalidOperationException("MenuHomeSuperCompact_Click was not found.");

        method.Invoke(window, new object[] { item, new RoutedEventArgs() });
        DrainDispatcher();
        window.UpdateLayout();
    }

    private static void SetScopedBottomButtonsVisible(bool visible)
    {
        string value = visible ? "是" : "否";
        foreach (string key in FeatureKeys)
            Config.dicts[TrainerMainWindowConfigScope.Prefix + key] = value;

        Config.dicts[TrainerMainWindowConfigScope.Prefix + "显示首页本地文章"] = value;
    }

    private static WinConfig ShowHomeSettingsWindow(Window owner)
    {
        var settings = new WinConfig();
        settings.Owner = owner;
        settings.Show();
        DrainDispatcher();
        settings.UpdateLayout();
        InvokeShowCategory(settings, 1);
        DrainDispatcher();
        settings.UpdateLayout();
        return settings;
    }

    private static void InvokeShowCategory(WinConfig settings, int categoryIndex)
    {
        var method = typeof(WinConfig).GetMethod(
            "ShowCategory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new InvalidOperationException("ShowCategory was not found.");

        method.Invoke(settings, new object[] { categoryIndex });
    }

    private static void SetHomeSettingsChecked(WinConfig settings, string label, bool isChecked)
    {
        var checkbox = FindSettingsCheckBox(settings, label);
        if (checkbox.IsChecked == isChecked)
        {
            DrainDispatcher();
            settings.UpdateLayout();
            return;
        }

        checkbox.IsChecked = isChecked;
        DrainDispatcher();
        settings.UpdateLayout();
        foreach (Window window in Application.Current.Windows)
        {
            var element = window as FrameworkElement;
            if (element != null)
                element.UpdateLayout();
        }
        DrainDispatcher();
    }

    private static CheckBox FindSettingsCheckBox(WinConfig settings, string label)
    {
        var contentPanel = settings.FindName("ContentPanel") as Grid;
        if (contentPanel == null)
            throw new InvalidOperationException("ContentPanel was not found.");

        foreach (var child in EnumerateLogical(contentPanel))
        {
            var listBox = child as ListBox;
            if (listBox == null)
                continue;

            foreach (ListBoxItem item in listBox.Items.OfType<ListBoxItem>())
            {
                var checkbox = FindDescendant<CheckBox>(item.Content as DependencyObject);
                if (checkbox != null && ContainsText(item.Content as DependencyObject, label))
                    return checkbox;
            }
        }

        foreach (var child in EnumerateLogical(contentPanel))
        {
            var row = child as StackPanel;
            if (row == null)
                continue;

            var checkbox = FindDescendant<CheckBox>(row);
            if (checkbox != null && ContainsText(row, label))
                return checkbox;
        }

        throw new InvalidOperationException("Could not find settings checkbox: " + label);
    }

    private static IEnumerable<DependencyObject> EnumerateLogical(DependencyObject root)
    {
        if (root == null)
            yield break;

        yield return root;
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            var dependencyChild = child as DependencyObject;
            if (dependencyChild == null)
                continue;

            foreach (var descendant in EnumerateLogical(dependencyChild))
                yield return descendant;
        }
    }

    private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null)
            return null;

        var typed = root as T;
        if (typed != null)
            return typed;

        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            var dependencyChild = child as DependencyObject;
            var result = FindDescendant<T>(dependencyChild);
            if (result != null)
                return result;
        }

        return null;
    }

    private static bool ContainsText(DependencyObject root, string text)
    {
        if (root == null)
            return false;

        var textBlock = root as TextBlock;
        if (textBlock != null && string.Equals(textBlock.Text, text, StringComparison.Ordinal))
            return true;

        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            var dependencyChild = child as DependencyObject;
            if (ContainsText(dependencyChild, text))
                return true;
        }

        return false;
    }

    private static void AssertCompact(string label, MainWindow window, bool expectedResultsExpanded)
    {
        var snapshot = Capture(window);
        Console.WriteLine(label + ": " + snapshot);

        if (expectedResultsExpanded)
        {
            Expect(label + " expanded compact row does not reserve toolbar height", snapshot.RowHeight <= 0.5);
            Expect(label + " expanded compact bottom border is removed", snapshot.BottomBorderRowHeight <= 0.5);
        }
        else
        {
            Expect(label + " collapsed compact row reserves toggle hit height", Math.Abs(snapshot.RowHeight - 15) < 0.5);
            Expect(label + " collapsed compact bottom border is removed", snapshot.BottomBorderRowHeight <= 0.5);
        }
        Expect(label + " panel collapsed", snapshot.PanelVisibility == Visibility.Collapsed);
        Expect(label + " compact host visible", snapshot.CompactHostVisibility == Visibility.Visible);
        Expect(label + " toggle in compact host", snapshot.ToggleParentName == "CompactResultsToggleHost");
        Expect(label + " toggle compact width", Math.Abs(snapshot.ToggleWidth - 15) < 0.5);
        Expect(label + " toggle compact height", Math.Abs(snapshot.ToggleHeight - 15) < 0.5);
        Expect(label + " no feature buttons", snapshot.VisibleFeatureButtonCount == 0);
        Expect(label + " local module hidden", !snapshot.HasVisibleLocalArticleModule);
        Expect(label + " results expanded state", snapshot.IsResultsExpanded == expectedResultsExpanded);
    }

    private static void AssertNormal(string label, MainWindow window, bool expectedResultsExpanded)
    {
        var snapshot = Capture(window);
        Console.WriteLine(label + ": " + snapshot);

        Expect(label + " row height >= 31", snapshot.RowHeight >= 30.5);
        Expect(label + " panel visible", snapshot.PanelVisibility == Visibility.Visible);
        Expect(label + " compact host collapsed", snapshot.CompactHostVisibility == Visibility.Collapsed);
        Expect(label + " toggle in results panel", snapshot.ToggleParentName == "resultsButtonPanel");
        Expect(label + " toggle normal width", snapshot.ToggleWidth >= 35.5);
        Expect(label + " has bottom buttons", snapshot.VisibleFeatureButtonCount > 0 || snapshot.HasVisibleLocalArticleModule);
        Expect(label + " results expanded state", snapshot.IsResultsExpanded == expectedResultsExpanded);
        if (expectedResultsExpanded)
            Expect(label + " expanded normal bottom border is removed", snapshot.BottomBorderRowHeight <= 0.5);
        else
            Expect(label + " collapsed normal bottom border is restored", Math.Abs(snapshot.BottomBorderRowHeight - 10) < 0.5);
    }

    private static void AssertSuperCompact(string label, MainWindow window)
    {
        var snapshot = Capture(window);
        Console.WriteLine(label + ": " + snapshot);

        Expect(label + " bottom toolbar row is removed", snapshot.RowHeight <= 0.5);
        Expect(label + " bottom border row is removed", snapshot.BottomBorderRowHeight <= 0.5);
        Expect(label + " panel collapsed", snapshot.PanelVisibility == Visibility.Collapsed);
        Expect(label + " compact host visible", snapshot.CompactHostVisibility == Visibility.Visible);
        Expect(label + " toggle in compact host", snapshot.ToggleParentName == "CompactResultsToggleHost");
    }

    private static Snapshot Capture(MainWindow window)
    {
        var typingGrid = Find<Grid>(window, "typingAreaAndButtonsGrid");
        var panel = Find<Grid>(window, "resultsButtonPanel");
        var compactHost = Find<Grid>(window, "CompactResultsToggleHost");
        var featurePanel = Find<Panel>(window, "FeatureToolbarPanel");
        var toggle = Find<Button>(window, "BtnToggleResults");
        var resultsGrid = Find<FrameworkElement>(window, "resultsTextBoxGrid");
        var mainGrid = Find<Grid>(window, "grid_a");

        double rowHeight = typingGrid.RowDefinitions[1].Height.IsAbsolute
            ? typingGrid.RowDefinitions[1].Height.Value
            : typingGrid.RowDefinitions[1].ActualHeight;

        int visibleFeatureCount = featurePanel.Children
            .OfType<UIElement>()
            .Count(child => child.Visibility == Visibility.Visible);

        bool localVisible =
            Find<Button>(window, "BtnArticleManager").Visibility == Visibility.Visible ||
            Find<Button>(window, "BtnPrev").Visibility == Visibility.Visible ||
            Find<Button>(window, "BtnNext").Visibility == Visibility.Visible ||
            Find<Button>(window, "BtnSendArticle").Visibility == Visibility.Visible;

        return new Snapshot
        {
            RowHeight = rowHeight,
            RowActualHeight = typingGrid.RowDefinitions[1].ActualHeight,
            BottomBorderRowHeight = mainGrid.RowDefinitions[7].Height.IsAbsolute
                ? mainGrid.RowDefinitions[7].Height.Value
                : mainGrid.RowDefinitions[7].ActualHeight,
            BottomBorderActualHeight = mainGrid.RowDefinitions[7].ActualHeight,
            PanelHeight = panel.Height,
            PanelActualHeight = panel.ActualHeight,
            PanelVisibility = panel.Visibility,
            CompactHostVisibility = compactHost.Visibility,
            CompactHostActualHeight = compactHost.ActualHeight,
            ToggleParentName = ((FrameworkElement)toggle.Parent).Name,
            ToggleWidth = toggle.ActualWidth,
            ToggleHeight = toggle.ActualHeight,
            VisibleFeatureButtonCount = visibleFeatureCount,
            HasVisibleLocalArticleModule = localVisible,
            IsResultsExpanded = resultsGrid.Visibility == Visibility.Visible
        };
    }

    private static void InvokeApplyScopedMainWindowState(MainWindow window)
    {
        var method = typeof(MainWindow).GetMethod(
            "ApplyScopedMainWindowState",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new InvalidOperationException("ApplyScopedMainWindowState was not found.");

        method.Invoke(window, null);
    }

    private static T Find<T>(FrameworkElement root, string name) where T : class
    {
        var value = root.FindName(name) as T;
        if (value == null)
            throw new InvalidOperationException("Could not find " + name + " as " + typeof(T).Name);
        return value;
    }

    private static void DrainDispatcher()
    {
        for (int i = 0; i < 4; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    private static void Expect(string message, bool condition)
    {
        if (condition)
            return;

        _failures++;
        Console.Error.WriteLine("FAILED: " + message);
    }

    private sealed class Snapshot
    {
        public double RowHeight;
        public double RowActualHeight;
        public double BottomBorderRowHeight;
        public double BottomBorderActualHeight;
        public double PanelHeight;
        public double PanelActualHeight;
        public Visibility PanelVisibility;
        public Visibility CompactHostVisibility;
        public double CompactHostActualHeight;
        public string ToggleParentName;
        public double ToggleWidth;
        public double ToggleHeight;
        public int VisibleFeatureButtonCount;
        public bool HasVisibleLocalArticleModule;
        public bool IsResultsExpanded;

        public override string ToString()
        {
            return string.Format(
                "row={0:0.##} actualRow={1:0.##} borderRow={2:0.##} borderActual={3:0.##} panelHeight={4:0.##} panelActual={5:0.##} panel={6} compactHost={7} compactActual={8:0.##} toggleParent={9} toggle={10:0.##}x{11:0.##} features={12} local={13} resultsExpanded={14}",
                RowHeight,
                RowActualHeight,
                BottomBorderRowHeight,
                BottomBorderActualHeight,
                PanelHeight,
                PanelActualHeight,
                PanelVisibility,
                CompactHostVisibility,
                CompactHostActualHeight,
                ToggleParentName,
                ToggleWidth,
                ToggleHeight,
                VisibleFeatureButtonCount,
                HasVisibleLocalArticleModule,
                IsResultsExpanded);
        }
    }
}
'@

Set-Content -Path $harnessSource -Value $source -Encoding UTF8

$originalProject = Join-Path $root 'TypeSunny.csproj'
$projectText = [System.IO.File]::ReadAllText($originalProject)
$projectText = $projectText.Replace('<OutputType>WinExe</OutputType>', '<OutputType>Exe</OutputType>')
$projectText = $projectText.Replace('<StartupObject>TypeSunny.App</StartupObject>', '<StartupObject>HomeBottomToolbarDynamicLayoutHarness</StartupObject>')
$escapedHarness = [System.Security.SecurityElement]::Escape($harnessSource)
$compileItem = "    <Compile Include=""$escapedHarness"" />"
$projectText = $projectText.Replace('    <Compile Include="App.xaml.cs">', "$compileItem`r`n    <Compile Include=""App.xaml.cs"">")

try {
    [System.IO.File]::WriteAllText($testProject, $projectText, [System.Text.Encoding]::UTF8)

    $objDir = Join-Path $buildDir 'obj'
    & $msbuild $testProject `
        /p:Configuration=$Configuration `
        /p:OutputPath="$buildDir\\" `
        /p:BaseIntermediateOutputPath="$objDir\\" `
        /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "TypeSunny dynamic layout test build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path $appExe)) {
        throw "Expected built app not found: $appExe"
    }

    $scenarios = @(
        'visible-to-hidden-collapsed',
        'hidden-to-visible-collapsed',
        'visible-to-hidden-expanded',
        'hidden-to-visible-expanded',
        'scoped-visible-expanded-to-hidden-collapsed',
        'settings-visible-to-hidden-collapsed',
        'settings-hidden-to-visible-collapsed',
        'settings-last-feature-to-hidden-collapsed',
        'settings-hidden-to-first-local-collapsed',
        'settings-repeated-last-feature-toggle-collapsed',
        'settings-all-home-buttons-visible-to-hidden-collapsed',
        'settings-all-home-buttons-hidden-to-first-bottom-collapsed',
        'super-compact-exit-restores-bottom-toolbar-collapsed',
        'scoped-settings-visible-to-hidden-collapsed',
        'scoped-settings-hidden-to-visible-collapsed'
    )
    foreach ($scenario in $scenarios) {
        Push-Location $buildDir
        try {
            & $appExe $scenario
            if ($LASTEXITCODE -ne 0) {
                throw "Home bottom toolbar dynamic layout scenario '$scenario' failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            Pop-Location
        }
    }
}
finally {
    Get-ChildItem -LiteralPath $root -Filter "TypeSunny.HomeBottomToolbarDynamicLayoutTests.$testRunId*.csproj" -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $harnessDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'All HomeBottomToolbarDynamicLayout tests passed.'
