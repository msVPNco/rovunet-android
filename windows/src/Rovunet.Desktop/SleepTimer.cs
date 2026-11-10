using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    AgentClient? timerDisconnectClient;
    Func<Task> recoverConnection = () => AgentClient.Recover();
    SleepTimerSession sleepSession = new();
    readonly DispatcherTimer sleepTicker = new() { Interval = TimeSpan.FromSeconds(1) };
    TextBlock sleepCountdown = null!;
    Button cancelSleep = null!;
    void UpdateSleepTimer()
    {
        sleepSession.UpdateConnection(state.State == "connected", data.SleepTimerMinutes);
        if (sleepSession.Active) sleepTicker.Start(); else sleepTicker.Stop();
        RefreshSleepTimer();
    }
    void RefreshSleepTimer()
    {
        if (sleepCountdown == null) return;
        bool active = sleepSession.Active;
        sleepCountdown.Visibility = cancelSleep.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        sleepCountdown.Text = string.Format(CultureInfo.CurrentCulture, L.Text("SLEEP_COUNTDOWN"), TimeSpan.FromSeconds(sleepSession.RemainingSeconds).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
    }
    void CancelSleepTimer() { sleepSession.Cancel(); sleepTicker.Stop(); RefreshSleepTimer(); }
    void TickSleepTimer()
    {
        if (sleepSession.PollExpired()) { sleepTicker.Stop(); _ = ExpireSleepTimer(sleepSession.Generation); }
        RefreshSleepTimer();
    }
    async Task ExpireSleepTimer(long generation)
    {
        if (sleepSession.Generation != generation) return;
        if (fixtureMode && !networkFixtureMode) { SetState(new()); return; }
        if (closing) return;
        if (busy)
        {
            timerDisconnectClient = client;
            RequestDisconnect();
            try { if (operationDone != null) await operationDone.Task.WaitAsync(TimeSpan.FromSeconds(35)); }
            catch { lastError = "RECOVERY_REQUIRED"; SetState(state); return; }
        }
        if (!closing && sleepSession.Generation == generation && state.State != "disconnected") await Run(Disconnect);
    }
}
