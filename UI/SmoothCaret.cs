using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TypeSunny.UI
{
    public class SmoothCaret
    {
        private const double CaretWidth = 2.0;

        private readonly bool _blinkWhenVisible;
        private readonly TranslateTransform _positionTransform;
        private readonly SmoothMotionTiming _timing = new SmoothMotionTiming();
        private bool _isPositionAnimating;
        private int _positionAnimationVersion;

        public Border Element { get; private set; }

        public SmoothCaret(double height, Brush foreground, bool startBlinking = true)
            : this(null, 0, 0, height, foreground, startBlinking)
        {
        }

        public SmoothCaret(Canvas parent, double x, double y, double height, Brush foreground, bool startBlinking = true)
        {
            _blinkWhenVisible = startBlinking;
            Element = new Border
            {
                Width = CaretWidth,
                Height = height,
                Background = foreground,
                Visibility = Visibility.Visible,
                IsHitTestVisible = false,
                SnapsToDevicePixels = false,
                UseLayoutRounding = false
            };
            _positionTransform = new TranslateTransform();
            Element.RenderTransform = _positionTransform;

            Canvas.SetLeft(Element, x);
            Canvas.SetTop(Element, y);

            if (parent != null)
                parent.Children.Add(Element);

            if (startBlinking)
                StartBlinking();
        }

        public static int GetDurationMilliseconds()
        {
            return SmoothMotionTiming.GetDurationMilliseconds();
        }

        public void RecordInput()
        {
            _timing.RecordInput();
        }

        public void AnimatePosition(double x, double y, double? height = null)
        {
            int durationMs = RefreshSpeedFromConfig();
            if (durationMs <= 0)
            {
                SetPosition(x, y, height);
                return;
            }

            double currentLeft = GetVisualLeft();
            double currentTop = GetVisualTop();
            double currentHeight = GetCurrent(FrameworkElement.HeightProperty, Element.Height, height ?? Element.ActualHeight);

            ClearPositionAnimations();
            int animationVersion = ++_positionAnimationVersion;
            _isPositionAnimating = true;

            Canvas.SetLeft(Element, x);
            Canvas.SetTop(Element, y);
            _positionTransform.X = currentLeft - x;
            _positionTransform.Y = currentTop - y;
            Element.Height = currentHeight;

            var duration = TimeSpan.FromMilliseconds(durationMs);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            var xAnimation = CreateAnimation(_positionTransform.X, 0, duration, ease);
            xAnimation.Completed += delegate
            {
                if (animationVersion == _positionAnimationVersion)
                    _isPositionAnimating = false;
            };

            _positionTransform.BeginAnimation(TranslateTransform.XProperty, xAnimation);
            _positionTransform.BeginAnimation(TranslateTransform.YProperty, CreateAnimation(_positionTransform.Y, 0, duration, ease));

            if (height.HasValue)
                Element.BeginAnimation(FrameworkElement.HeightProperty, CreateAnimation(currentHeight, height.Value, duration, ease));
        }

        public void SetPosition(double x, double y, double? height = null)
        {
            ClearPositionAnimations();
            Canvas.SetLeft(Element, x);
            Canvas.SetTop(Element, y);
            _positionTransform.X = 0;
            _positionTransform.Y = 0;

            if (height.HasValue)
                Element.Height = height.Value;
        }

        public void TrackPosition(double x, double y, double? height = null)
        {
            if (!_isPositionAnimating)
            {
                SetPosition(x, y, height);
                return;
            }

            double currentLeft = GetVisualLeft();
            double currentTop = GetVisualTop();

            Canvas.SetLeft(Element, x);
            Canvas.SetTop(Element, y);
            _positionTransform.X = currentLeft - x;
            _positionTransform.Y = currentTop - y;

            if (height.HasValue)
                Element.Height = height.Value;
        }

        public void StartBlinking()
        {
            UpdateBlinkingAnimation();
        }

        public void StopBlinking()
        {
            Element.BeginAnimation(UIElement.OpacityProperty, null);
            Element.Opacity = 1;
        }

        public void UpdateBlinkingAnimation()
        {
            Element.BeginAnimation(UIElement.OpacityProperty, null);

            if (!_blinkWhenVisible || Element.Visibility != Visibility.Visible)
            {
                Element.Opacity = 1;
                return;
            }

            var blink = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                FillBehavior = FillBehavior.HoldEnd
            };

            Element.BeginAnimation(UIElement.OpacityProperty, blink);
        }

        public void ApplyForeground(Brush foreground)
        {
            Element.Background = foreground;
        }

        public void Show()
        {
            Element.Visibility = Visibility.Visible;
            UpdateBlinkingAnimation();
        }

        public void Hide()
        {
            Element.Visibility = Visibility.Collapsed;
            StopBlinking();
        }

        public int RefreshSpeedFromConfig()
        {
            return _timing.GetCurrentDurationMilliseconds();
        }

        public int GetBackgroundDurationMilliseconds()
        {
            return _timing.GetBackgroundDurationMilliseconds();
        }

        private static DoubleAnimation CreateAnimation(double from, double to, TimeSpan duration, IEasingFunction ease)
        {
            return new DoubleAnimation(from, to, new Duration(duration))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            };
        }

        private void ClearPositionAnimations()
        {
            _positionAnimationVersion++;
            _isPositionAnimating = false;
            _positionTransform.BeginAnimation(TranslateTransform.XProperty, null);
            _positionTransform.BeginAnimation(TranslateTransform.YProperty, null);
            Element.BeginAnimation(FrameworkElement.HeightProperty, null);
        }

        private double GetVisualLeft()
        {
            return ReadCanvasValue(Canvas.GetLeft(Element), 0) + _positionTransform.X;
        }

        private double GetVisualTop()
        {
            return ReadCanvasValue(Canvas.GetTop(Element), 0) + _positionTransform.Y;
        }

        private double GetCurrent(DependencyProperty property, double value, double fallback)
        {
            object animatedValue = Element.GetValue(property);
            if (animatedValue is double)
                value = (double)animatedValue;

            return ReadCanvasValue(value, fallback);
        }

        private static double ReadCanvasValue(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return fallback;

            return value;
        }

    }
}
