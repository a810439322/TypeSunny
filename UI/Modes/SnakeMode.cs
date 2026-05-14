using System;
using System.Windows;
using System.Windows.Controls;
using TypeSunny.Core;
using TypeSunny.Utils;

namespace TypeSunny.UI.Modes
{
    /// <summary>
    /// 贪吃蛇模式 - 流动式显示前后字符
    /// </summary>
    public class SnakeMode
    {
        private readonly MainWindow _main;

        public SnakeMode(MainWindow main)
        {
            _main = main;
        }

        public void Update(int nextToType)
        {
            // 获取配置（赛文API强制使用前20后30）
            int preShowCount;
            int postShowCount;

            if (StateManager.txtSource == TxtSource.raceApi)
            {
                preShowCount = 20;
                postShowCount = 30;
            }
            else
            {
                preShowCount = Config.GetInt("贪吃蛇前显字数");
                postShowCount = Config.GetInt("贪吃蛇后显字数");
            }

            // 如果是第一次进入贪吃蛇模式，需要创建所有TextBlock
            bool isFirstTime = (TextInfo.Blocks.Count != TextInfo.Words.Count);
            if (isFirstTime)
            {
                // 清空显示区域
                _main.TbDispay.Children.Clear();
                _main.ScDisplay.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                TextInfo.Blocks.Clear();

                // 获取字体设置
                var fm = _main.GetCurrentFontFamily();
                double fs = MainWindow.DisplayFontSize;
                double height = fs * (1.0 + Config.GetDouble("行距"));
                double availablePad = Math.Max(0, height - fs * fm.LineSpacing);
                double padTop = (availablePad / 2 + Math.Min((height - fs) / 2, availablePad)) / 2;
                double padBottom = availablePad - padTop;
                double MinWidth = fs * 0.9;

                _main.ScDisplay.FontFamily = fm;
                _main.ScDisplay.Foreground = Colors.DisplayForeground;
                _main.ScDisplay.FontSize = fs;
                _main.TbxInput.FontFamily = fm;

                // 创建所有字符的TextBlock
                for (int i = 0; i < TextInfo.Words.Count; i++)
                {
                    // 在换行位置前插入占满整行的空元素
                    if (TextInfo.LineBreaks.Contains(i))
                    {
                        var lineBreak = new FrameworkElement();
                        lineBreak.Width = _main.TbDispay.ActualWidth > 0 ? _main.TbDispay.ActualWidth : 9999;
                        lineBreak.Height = 0;
                        _main.TbDispay.Children.Add(lineBreak);
                    }

                    TextBlock tb = new TextBlock();
                    tb.Text = TextInfo.Words[i];
                    tb.Height = height;
                    tb.Padding = new Thickness(0, padTop, 0, padBottom);

                    // 引号的特殊处理
                    if (tb.Text == "\u201c" || tb.Text == "\u2018")
                    {
                        tb.MinWidth = MinWidth;
                        tb.TextAlignment = TextAlignment.Right;
                    }
                    else if (tb.Text == "\u201d" || tb.Text == "\u2019")
                    {
                        tb.MinWidth = MinWidth;
                        tb.TextAlignment = TextAlignment.Left;
                    }

                    _main.ApplyDisplayForeground(tb, i);
                    TextInfo.Blocks.Add(tb);
                    _main.TbDispay.Children.Add(_main.CreateDisplayElement(tb, i));
                }
            }

            // 更新所有字符的显示状态（背景色和透明度）
            for (int i = 0; i < TextInfo.Blocks.Count; i++)
            {
                TextBlock tb = TextInfo.Blocks[i];
                double opacity = 1.0;

                if (i < nextToType)
                {
                    // 已打字符 - 向前渐变
                    int distanceFromCurrent = nextToType - i;
                    if (distanceFromCurrent > preShowCount)
                        opacity = 0.0;
                    else if (distanceFromCurrent > preShowCount - 10)
                    {
                        double fadeDistance = distanceFromCurrent - (preShowCount - 10);
                        opacity = 1.0 - (fadeDistance / 10.0);
                    }

                    // 设置背景色
                    if (i < TextInfo.wordStates.Count)
                    {
                        switch (TextInfo.wordStates[i])
                        {
                            case WordStates.WRONG:
                                tb.Background = _main.IsBlindType ? null : Colors.IncorrectBackground;
                                break;
                            case WordStates.RIGHT:
                                tb.Background = _main.IsBlindType ? null : Colors.CorrectBackground;
                                break;
                            default:
                                tb.Background = null;
                                break;
                        }
                    }

                    _main.ApplyDisplayForeground(tb, i);
                }
                else if (i == nextToType)
                {
                    // 当前要打的字符
                    opacity = 1.0;
                    _main.ApplyDisplayForeground(tb, i);
                    tb.Background = null;
                }
                else
                {
                    // 未打字符 - 向后渐变
                    int distanceFromCurrent = i - nextToType;
                    if (distanceFromCurrent > postShowCount)
                        opacity = 0.0;
                    else if (distanceFromCurrent > postShowCount - 10)
                    {
                        double fadeDistance = distanceFromCurrent - (postShowCount - 10);
                        opacity = 1.0 - (fadeDistance / 10.0);
                    }

                    _main.ApplyDisplayForeground(tb, i);
                    tb.Background = null;
                }

                tb.Opacity = opacity;
            }

            // 字帖模式下同步错字提示的透明度
            if (_main._copybookMode != null && _main._copybookMode.IsActive)
                _main._copybookMode.SyncWrongCharHintOpacity();

            // 滚动到当前字符位置
            if (nextToType < TextInfo.Blocks.Count && nextToType >= 0)
            {
                try
                {
                    double currentPosY =
                        TextInfo.Blocks[nextToType].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y
                        + TextInfo.Blocks[nextToType].ActualHeight / 2;

                    double targetOffset = _main.CalculateScrollOffset(currentPosY);
                    _main.SmoothScrollTo(targetOffset, forceScroll: (nextToType == 0));
                    _main.UpdateSpeedFollowHint(nextToType);
                }
                catch
                {
                    // 忽略布局异常
                }
            }
        }
    }
}
