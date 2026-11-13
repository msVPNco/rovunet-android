using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    TextBlock heroTitle = null!, heroDetail = null!, heroPing = null!;
    bool ConnectionInternetVerified()=>state.State=="connected"&&(latencySampled.HasValue
        ?liveLatency.HasValue&&(DateTimeOffset.UtcNow-latencySampled.Value).TotalSeconds<60
        :state.Verified);
    string ConnectionDetailKey()=>ConnectionInternetVerified()?"CONNECTION_INTERNET_VERIFIED"
        :lastTraffic is {Uploaded:>0} or {Downloaded:>0}?"CONNECTION_DATA_CONFIRMED":"CONNECTION_ESTABLISHED";

    FrameworkElement BuildHero() => BuildHero21();

    void HeroConnect()
    {
        if (state.State is "connected" or "connecting") { RequestDisconnect(); return; }
        if (Selected == null) {var candidate=RandomConnectableProfile20();if(candidate==null){OpenAddHub();return;}SelectProfile20(candidate);if(Selected==null)return;}
        _ = Run(Connect);
    }
    bool CanConnectFromTray20()=>data.Profiles.Count==0||(Selected is {} selected?SupportPolicy.Allowed(selected,data.IncludeExperimental):data.Profiles.Any(p=>p.CanConnect&&SupportPolicy.Allowed(p,data.IncludeExperimental)));
    void ToggleTrayConnection()
    {
        if(state.State=="disconnecting")return;
        if(Selected==null)ShowExisting();
        HeroConnect();
    }

    void RefreshHero()
    {
        if (heroTitle == null) return;
        RefreshPowerButton();
        var profile = activeProfile ?? Selected;
        long? ping = state.State=="connected"?liveLatency:null;
        bool connected = state.State == "connected";
        heroTitle.Text = L.Text(state.State.ToUpperInvariant());
        heroTitle.Foreground=ConnectionInk28(state.State);
        heroName21.Text = profile?.Name ?? "";
        heroNameLine21.Visibility = profile == null ? Visibility.Collapsed : Visibility.Visible;
        heroPing.Text = ping.HasValue ? ping.Value.ToString(CultureInfo.InvariantCulture) + " ms" : "";
        heroPing.Visibility = ping.HasValue ? Visibility.Visible : Visibility.Collapsed;
        heroPing.Foreground=latencySampled.HasValue&&(DateTimeOffset.UtcNow-latencySampled.Value).TotalSeconds<60?Green:Muted;
        heroDetail.Text = state.State switch { "connected" => L.Text(ConnectionDetailKey()), "connecting" => L.Text("CONNECTING_HINT"), "error" => L.Text(state.Error.Length > 0 ? state.Error : "ERROR_HINT"), _ => L.Text("CONNECTION_IDLE") };
        heroDetail.Foreground=state.State=="error"?MessageInk28(state.Error.Length>0?state.Error:"ERROR_HINT"):connected&&ConnectionInternetVerified()?Green:Muted;
        if(connectionText!=null){connectionText.Text=L.Text(state.State.ToUpperInvariant());connectionText.Foreground=ConnectionInk28(state.State);}
        // Telemetry refresh must not resurrect rows hidden by the sticky layout.
        RefreshHeroCollapse19();
    }

    long? PingOf(Profile? profile)
    {
        if (profile == null) return null;
        if (measurements.TryGetValue(profile.Id, out var live) && live.ElapsedMs > 0) return live.ElapsedMs;
        if (data.Measurements.TryGetValue(profile.Id, out var saved) && saved.ElapsedMs > 0) return saved.ElapsedMs;
        return null;
    }
}
