using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    sealed class FixtureSleepClock : TimeProvider
    {
        public long Seconds;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }
    void SleepSmoke(Dictionary<string, bool> checks, string report)
    {
        var original = sleepSession; int minutes = data.SleepTimerMinutes;
        try
        {
            var clock = new FixtureSleepClock(); sleepSession = new(clock);
            data.SleepTimerMinutes = 5; SetState(new() { State = "connected" });
            checks["sleep_countdown_visible_after_connection"] = sleepCountdown.Visibility == Visibility.Visible && sleepSession.RemainingSeconds == 300;
            UpdateLayout(); checks["sleep_cancel_touch_target"] = cancelSleep.ActualHeight >= 44 && cancelSleep.ActualWidth >= 44;
            var pixels = new System.Windows.Media.Imaging.RenderTargetBitmap((int)((FrameworkElement)Content).ActualWidth, (int)((FrameworkElement)Content).ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); pixels.Render((FrameworkElement)Content);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(pixels)); using (var file = new FileStream(Path.Combine(Path.GetDirectoryName(report)!, "sleep-active.png"), FileMode.CreateNew)) png.Save(file);
            clock.Seconds = 60; Build();
            checks["sleep_resize_rebuild_does_not_restart"] = sleepSession.RemainingSeconds == 240;
            cancelSleep.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            SetState(state);
            checks["sleep_cancel_keeps_connection_and_preference"] = state.State == "connected" && data.SleepTimerMinutes == 5 && !sleepSession.Active;
            SetState(new()); SetState(new() { State = "connected" });
            checks["sleep_new_connection_rearms"] = sleepSession.Active && sleepSession.RemainingSeconds == 300;
            long previousGeneration = sleepSession.Generation;
            SetState(new()); SetState(new() { State = "connected" });
            ExpireSleepTimer(previousGeneration).GetAwaiter().GetResult();
            checks["stale_sleep_callback_cannot_disconnect_new_session"] = state.State == "connected" && sleepSession.Active;
            var previousState = WindowState; WindowState = WindowState.Minimized;
            clock.Seconds = 360; TickSleepTimer();
            checks["sleep_expiry_works_while_minimized"] = state.State == "disconnected"; WindowState = previousState;
            checks["sleep_expiry_disconnects_fixture"] = state.State == "disconnected" && !sleepSession.Active;
            var settings = BuildSettingsPanel(() => { });
            var host = new Border { Child = settings, Width = 660 }; host.Measure(new Size(660, double.PositiveInfinity)); host.Arrange(new Rect(0,0,660,host.DesiredSize.Height)); host.UpdateLayout();
            var increase = Visuals<Button>(host).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == L.Text("SLEEP_INCREASE"));
            for (int i = 0; i < 24 && increase.IsEnabled; i++) increase.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["sleep_setting_matches_android_range"] = data.SleepTimerMinutes == 120 && !increase.IsEnabled;
            var encoded = System.Text.Json.JsonSerializer.Serialize(data);
            checks["sleep_preference_json_roundtrip"] = System.Text.Json.JsonSerializer.Deserialize<Rovunet.Platform.UserData>(encoded)!.SleepTimerMinutes == 120;
        }
        finally { CancelSleepTimer(); sleepSession = original; data.SleepTimerMinutes = minutes; SetState(new()); }
    }
}
