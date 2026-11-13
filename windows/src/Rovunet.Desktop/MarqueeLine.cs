using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Rovunet.Desktop;

// The text moves inside a fixed clipped viewport. The adjacent repeat is a visual
// brush, not another interactive copy, so card gestures and automation stay intact.
internal sealed class MarqueeLine : Grid
{
    const double Gap = 32, PixelsPerSecond = 24;
    readonly FrameworkElement source;
    readonly Canvas track = new() { ClipToBounds = false, FlowDirection = FlowDirection.LeftToRight };
    readonly Rectangle repeat = new() { IsHitTestVisible = false };
    readonly VisualBrush repeatBrush;
    readonly TranslateTransform translation = new();
    readonly Func<bool> motion;
    readonly FlowDirection readingDirection;
    readonly List<DependencyPropertyDescriptor> textProperties = [];
    AnimationClock? clock;
    ScrollViewer? viewport;
    Window? hostWindow;
    double? frozenOffset;
    double naturalWidth, lastWidth = -1, lastNatural = -1;
    bool attached, queued, hovering, lastMotion;
    Point? touchStart;
    double touchOffset;
    bool dragging;

    internal bool Overflowing => naturalWidth > ActualWidth + 1;
    internal bool Moving => clock?.CurrentState == ClockState.Active && !clock.IsPaused;
    internal bool HasAnimationClock=>clock!=null;
    internal double Translation => translation.X;
    internal double NaturalWidth => naturalWidth;
    internal bool? MotionOverride { get; set; }

    internal MarqueeLine(FrameworkElement content, double height, FlowDirection direction, Func<bool> enabled)
    {
        source = content; readingDirection = direction; motion = enabled;
        Height = height; MinWidth = 0; ClipToBounds = true;
        FlowDirection = FlowDirection.LeftToRight; Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        source.HorizontalAlignment = HorizontalAlignment.Left;
        if (source is TextBlock text)
        {
            text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.None;
            text.Margin = new(0); text.VerticalAlignment = VerticalAlignment.Center;
        }
        track.RenderTransform = translation;
        // Map equal-sized absolute rectangles. Relative ink bounds plus Stretch.Fill
        // can distort glyphs in the repeated part of the loop (notably Persian).
        repeatBrush = new VisualBrush(source) { Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, ViewportUnits = BrushMappingMode.Absolute, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
        repeat.Fill = repeatBrush;
        track.Children.Add(source); track.Children.Add(repeat); Children.Add(track);
        Loaded += Attach; Unloaded += Detach;
        IsVisibleChanged += (_, _) => QueueRefresh();
        MouseEnter += (_, _) => { hovering = true; clock?.Controller?.Pause(); };
        MouseLeave += (_, _) => { hovering = false; if (CanMove()) clock?.Controller?.Resume(); };
        PreviewTouchDown += (_, e) => { touchStart = e.GetTouchPoint(this).Position; touchOffset = translation.X; dragging = false; };
        PreviewTouchMove += (_, e) =>
        {
            if (touchStart is not { } start || !Overflowing) return;
            var delta = e.GetTouchPoint(this).Position - start;
            if (!dragging && (Math.Abs(delta.X) < 12 || Math.Abs(delta.X) <= Math.Abs(delta.Y))) return;
            if (!dragging) { Stop(false); dragging = true; CaptureTouch(e.TouchDevice); }
            double lower = readingDirection == FlowDirection.RightToLeft ? 0 : ActualWidth - naturalWidth;
            double upper = readingDirection == FlowDirection.RightToLeft ? naturalWidth - ActualWidth : 0;
            translation.X = Math.Clamp(touchOffset + delta.X, lower, upper); e.Handled = true;
        };
        PreviewTouchUp += (_, e) =>
        {
            touchStart = null;
            if (!dragging) return;
            ReleaseTouchCapture(e.TouchDevice); dragging = false; e.Handled = true;
            lastWidth = -1; QueueRefresh();
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Normalize(source);
        source.Measure(new Size(double.PositiveInfinity, Math.Max(0, Height)));
        naturalWidth = Math.Max(0, source.DesiredSize.Width);
        return new(double.IsFinite(availableSize.Width) ? Math.Min(naturalWidth, availableSize.Width) : naturalWidth, Height);
    }

    static void Normalize(FrameworkElement element)
    {
        if (element is TextBlock text)
        {
            string singleLine = text.Text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            if (singleLine != text.Text) text.Text = singleLine;
            text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.None;
        }
        else if (element is Panel panel) foreach (FrameworkElement child in panel.Children) Normalize(child);
    }

    internal static void RefreshAll(DependencyObject root)
    {
        if (root is MarqueeLine line) line.RefreshMotion();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) RefreshAll(VisualTreeHelper.GetChild(root, i));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = readingDirection == FlowDirection.RightToLeft ? finalSize.Width - naturalWidth : 0;
        // The right edge is the reading origin for RTL; only the track moves.
        track.Arrange(new Rect(x, 0, naturalWidth, finalSize.Height));
        source.Arrange(new Rect(0, 0, naturalWidth, finalSize.Height));
        repeat.Width = naturalWidth; repeat.Height = finalSize.Height;
        var copyBounds = new Rect(0, 0, naturalWidth, finalSize.Height);
        repeatBrush.Viewbox = copyBounds; repeatBrush.Viewport = copyBounds;
        Canvas.SetLeft(repeat, (readingDirection == FlowDirection.RightToLeft ? -1 : 1) * (naturalWidth + Gap));
        repeat.Arrange(new Rect(Canvas.GetLeft(repeat), 0, naturalWidth, finalSize.Height));
        QueueRefresh(); return finalSize;
    }

    void Attach(object sender, RoutedEventArgs e)
    {
        if (attached) return; attached = true;
        if (source is TextBlock)
        {
            foreach (var property in new[] { TextBlock.TextProperty, TextBlock.FontFamilyProperty, TextBlock.FontSizeProperty, TextBlock.FontWeightProperty, TextBlock.FontStyleProperty, TextBlock.FontStretchProperty, TextBlock.LineHeightProperty, FrameworkElement.FlowDirectionProperty, FrameworkElement.LanguageProperty })
            {
                var descriptor = DependencyPropertyDescriptor.FromProperty(property, typeof(TextBlock));
                if (descriptor == null) continue;
                descriptor.AddValueChanged(source, TextChanged); textProperties.Add(descriptor);
            }
        }
        for (DependencyObject? parent = VisualTreeHelper.GetParent(this); parent != null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is ScrollViewer scroll) { viewport = scroll; viewport.ScrollChanged += Scrolled; break; }
        SystemParameters.StaticPropertyChanged += PreferenceChanged;
        hostWindow=Window.GetWindow(this);
        if(hostWindow!=null){hostWindow.Activated+=WindowActivityChanged;hostWindow.Deactivated+=WindowActivityChanged;hostWindow.StateChanged+=WindowActivityChanged;}
        // Text can also change while this view is detached (another page).
        InvalidateMeasure(); InvalidateArrange(); lastWidth = -1; QueueRefresh();
    }
    void Detach(object sender, RoutedEventArgs e)
    {
        foreach (var descriptor in textProperties) descriptor.RemoveValueChanged(source, TextChanged);
        textProperties.Clear();
        attached = false; if (viewport != null) viewport.ScrollChanged -= Scrolled;
        viewport = null; SystemParameters.StaticPropertyChanged -= PreferenceChanged;
        if(hostWindow!=null){hostWindow.Activated-=WindowActivityChanged;hostWindow.Deactivated-=WindowActivityChanged;hostWindow.StateChanged-=WindowActivityChanged;hostWindow=null;}
        frozenOffset=null;Stop();
    }
    void TextChanged(object? sender, EventArgs e)
    {
        // Canvas does not propagate a child's new desired size to this viewport.
        // Re-measure the current text immediately instead of waiting for a full
        // page rebuild (for example a language change). Restart at its origin.
        frozenOffset = null; lastNatural = -1; Stop();
        source.InvalidateMeasure(); InvalidateMeasure(); InvalidateArrange();
        QueueRefresh();
    }
    void Scrolled(object sender, ScrollChangedEventArgs e) => QueueRefresh();
    void PreferenceChanged(object? sender, PropertyChangedEventArgs e) => QueueRefresh();
    internal void RefreshMotion() { lastWidth = -1; QueueRefresh(); }
    void QueueRefresh()
    {
        if (queued || !IsLoaded) return; queued = true;
        Dispatcher.BeginInvoke(() => { queued = false; Refresh(); }, DispatcherPriority.Render);
    }
    bool OnScreen()
    {
        if (!IsVisible || ActualWidth < 1 || ActualHeight < 1) return false;
        if (viewport == null) return true;
        try { return TransformToAncestor(viewport).TransformBounds(new Rect(RenderSize)).IntersectsWith(new Rect(viewport.RenderSize)); }
        catch (InvalidOperationException) { return false; }
    }
    bool WindowActive=>hostWindow?.IsActive==true&&hostWindow.WindowState!=WindowState.Minimized;
    void WindowActivityChanged(object? sender,EventArgs e)
    {
        if(!WindowActive){if(clock!=null){frozenOffset=translation.X;Stop(false);}return;}
        lastWidth=-1;QueueRefresh();
    }
    bool CanMove() => WindowActive&&(MotionOverride ?? motion()) && !SystemParameters.HighContrast && Overflowing && OnScreen() && !dragging;
    void Refresh()
    {
        if(!WindowActive){
            if(clock!=null){frozenOffset=translation.X;Stop(false);}
            if(lastWidth!=ActualWidth||lastNatural!=naturalWidth){Stop();frozenOffset=null;repeat.Visibility=Visibility.Collapsed;}
            lastWidth=ActualWidth;lastNatural=naturalWidth;lastMotion=false;return;
        }
        bool run = CanMove();
        repeat.Visibility = run ? Visibility.Visible : Visibility.Collapsed;
        if (lastWidth == ActualWidth && lastNatural == naturalWidth && lastMotion == run) return;
        lastWidth = ActualWidth; lastNatural = naturalWidth; lastMotion = run; Stop();
        if (!run) return;
        double distance = naturalWidth + Gap, travel = distance / PixelsPerSecond;
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(travel + 2.6), RepeatBehavior = RepeatBehavior.Forever };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.3))));
        double end = readingDirection == FlowDirection.RightToLeft ? distance : -distance;
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(end, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(travel + 1.3))));
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(end, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(travel + 2.6))));
        Timeline.SetDesiredFrameRate(animation, 30);
        clock = (AnimationClock)animation.CreateClock(true); translation.ApplyAnimationClock(TranslateTransform.XProperty, clock);
        if(frozenOffset is {} offset){clock.Controller?.SeekAlignedToLastTick(TimeSpan.FromSeconds(1.3+Math.Clamp(Math.Abs(offset),0,distance)/PixelsPerSecond),TimeSeekOrigin.BeginTime);frozenOffset=null;}
        if (hovering) clock.Controller?.Pause();
    }
    void Stop(bool reset = true)
    {
        double current = translation.X; clock?.Controller?.Remove(); clock = null;
        translation.ApplyAnimationClock(TranslateTransform.XProperty, null); translation.X = reset ? 0 : current;
    }
}
