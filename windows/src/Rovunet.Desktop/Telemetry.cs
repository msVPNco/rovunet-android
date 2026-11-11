using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Rovunet.Core;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    TrafficSampler trafficSampler = new(); TrafficSnapshot? lastTraffic;
    bool trafficBaselineNeeded=true;
    TransferRates rates = new(null, null); readonly Queue<double> graphSamples = [];
    bool polling; long lastLatencyTick; DateTimeOffset? latencySampled; long? liveLatency; string liveLatencyError = "";
    static string Size(double value) => value >= 1073741824 ? (value / 1073741824).ToString("0.##", CultureInfo.InvariantCulture) + " GiB" : value >= 1048576 ? (value / 1048576).ToString("0.##", CultureInfo.InvariantCulture) + " MiB" : value >= 1024 ? (value / 1024).ToString("0.#", CultureInfo.InvariantCulture) + " KiB" : value.ToString("0", CultureInfo.InvariantCulture) + " B";
    void ResetTelemetry() { trafficSampler = new();trafficBaselineNeeded=true; lastTraffic = null; rates = new(null, null); liveLatency = null; liveLatencyError = ""; latencySampled = null; lastLatencyTick = 0; graphSamples.Clear(); }
    void BeginTrafficSampling()
    {
        if(!trafficBaselineNeeded)return;
        // Xray counters belong to this newly started core. Include payload sent before the first timer tick.
        trafficSampler.Sample(new(0,0),Stopwatch.GetTimestamp());trafficBaselineNeeded=false;
    }
    void RefreshTrafficText()
    {
        if (trafficUp == null) return;
        trafficUp.Text = "↑ " + (rates.UploadPerSecond is double up ? Size(up) + "/s" : "—"); trafficDown.Text = "↓ " + (rates.DownloadPerSecond is double down ? Size(down) + "/s" : "—");
        trafficTotal.Text = lastTraffic == null ? "—" : Size(lastTraffic.Uploaded + lastTraffic.Downloaded); trafficTotal.ToolTip = L.Text("TRAFFIC_SCOPE");
        trafficRecorded.Text = Size((double)data.RecordedUpload + data.RecordedDownload + (lastTraffic?.Uploaded ?? 0) + (lastTraffic?.Downloaded ?? 0)); trafficRecorded.ToolTip = L.Text("RECORDED_SCOPE");
        if (latencySampled.HasValue)
        {
            lastTestValue.Text = liveLatency.HasValue ? liveLatency.Value.ToString(CultureInfo.InvariantCulture) + " ms" : "—"; lastTestValue.ToolTip = liveLatencyError.Length > 0 ? L.Text(liveLatencyError) : L.Text("HTTPS_LATENCY_SCOPE");
            latencyAge.Text = L.Text("SAMPLED_AGO") + " " + Math.Max(0, (int)(DateTimeOffset.UtcNow - latencySampled.Value).TotalSeconds) + " s";
        }
        if (graphSamples.Count >= 2) { double max = Math.Max(1, graphSamples.Max()); var values = graphSamples.ToArray(); trafficLine.Points = new PointCollection(values.Select((v, i) => new Point(i * 98d / Math.Max(1, values.Length - 1), 21 - v / max * 19))); }
        else trafficLine.Points.Clear();
    }
    async Task PollTelemetry()
    {
        if (polling || busy || closing || client == null || state.State != "connected") return;
        polling = true; var current = client;
        try
        {
            var reply = await current.Send(new() { Action = "telemetry" }); if (current != client || state.State != "connected" || closing) return;
            if (reply.TrafficAvailable)
            {
                lastTraffic = new(reply.UploadedBytes, reply.DownloadedBytes); rates = trafficSampler.Sample(lastTraffic, Stopwatch.GetTimestamp());
                if (rates.DownloadPerSecond.HasValue && rates.UploadPerSecond.HasValue) { graphSamples.Enqueue(rates.DownloadPerSecond.Value + rates.UploadPerSecond.Value); while (graphSamples.Count > 40) graphSamples.Dequeue(); }
            }
            else { rates = new(null, null); }
            RefreshTrafficText();
            if (data.LiveLatency && (lastLatencyTick == 0 || Stopwatch.GetElapsedTime(lastLatencyTick).TotalSeconds >= 30) && !busy)
            {
                // Separate, bounded request. This is HTTPS latency, not ICMP ping.
                // It never gates manual connect or changes the active route.
                lastLatencyTick = Stopwatch.GetTimestamp(); var latency = await current.Send(new() { Action = "latency", ProbeUrl = data.LatencyUrl });
                if (current != client || state.State != "connected" || closing) return;
                latencySampled = DateTimeOffset.UtcNow; liveLatency = latency.Verified ? latency.ElapsedMs : null; liveLatencyError = latency.Error; RefreshTrafficText();
            }
        }
        catch { if (current == client) { rates = new(null, null); RefreshTrafficText(); } }
        finally { polling = false; }
    }
    void RecordTraffic(AgentReply reply)
    {
        var final = reply.TrafficAvailable ? new TrafficSnapshot(reply.UploadedBytes, reply.DownloadedBytes) : lastTraffic;
        try
        {
            if (final != null)
            {
                long beforeUp = data.RecordedUpload, beforeDown = data.RecordedDownload;
                data.RecordedUpload += Math.Max(0, final.Uploaded); data.RecordedDownload += Math.Max(0, final.Downloaded);
                try { Save(); } catch { data.RecordedUpload = beforeUp; data.RecordedDownload = beforeDown; throw; }
            }
        }
        finally { ResetTelemetry(); RefreshTrafficText(); }
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    void UpdateTitleTheme()
    {
        try { var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle; if (handle != IntPtr.Zero) { int value = darkTheme && !SystemParameters.HighContrast ? 1 : 0; _ = DwmSetWindowAttribute(handle, 20, ref value, 4); } } catch { }
    }
}
