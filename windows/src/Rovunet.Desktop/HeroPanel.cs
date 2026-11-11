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
        heroName21.Text = profile?.Name ?? "";
        heroNameLine21.Visibility = profile == null ? Visibility.Collapsed : Visibility.Visible;
        heroPing.Text = ping.HasValue ? ping.Value.ToString(CultureInfo.InvariantCulture) + " ms" : "";
        heroPing.Visibility = ping.HasValue ? Visibility.Visible : Visibility.Collapsed;
        heroDetail.Text = state.State switch { "connected" => L.Text(state.Verified ? "HTTPS_VERIFIED" : "PATH_READY_UNVERIFIED"), "connecting" => L.Text("CONNECTING_HINT"), "error" => L.Text(state.Error.Length > 0 ? state.Error : "ERROR_HINT"), _ => L.Text("HOME_CONNECT_HINT") };
    }

    long? PingOf(Profile? profile)
    {
        if (profile == null) return null;
        if (measurements.TryGetValue(profile.Id, out var live) && live.ElapsedMs > 0) return live.ElapsedMs;
        if (data.Measurements.TryGetValue(profile.Id, out var saved) && saved.ElapsedMs > 0) return saved.ElapsedMs;
        return null;
    }
}
