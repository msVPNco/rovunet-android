using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Rovunet.Core;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    readonly Dictionary<string, AgentReply> measurements = [];
    CancellationTokenSource? testStop;
    AgentClient? testing;
    MenuItem MenuAction(string key, Action action) { var item = new MenuItem { Header = L.Text(key),Tag=key }; item.Click += (_, args) => { args.Handled=true;try { action(); } catch (Exception e) { lastError = e is SafeError se ? se.Code : "UNEXPECTED_ERROR"; SetState(state); } }; return item; }
    ContextMenu CollectionMenu(List<Profile> members)
    {
        var menu = new ContextMenu { FlowDirection = L.Direction };
        MenuHeading(menu,"MENU_CONNECTION");
        menu.Items.Add(MenuAction("CONNECT",()=>ConnectCollection(members)));
        menu.Items.Add(MenuAction("SHOW_PATHS",()=>ToggleCollection(members)));
        menu.Items.Add(MenuAction("TEST_COLLECTION", () => CollectionTestDialog(members)));
        menu.Items.Add(MenuAction("STABILITY_TEST", () => CollectionTestDialog(members, 3)));
        menu.Items.Add(MenuAction("STOP_TEST",CancelCollectionTest));
        menu.Items.Add(MenuAction("PATH_SELECTION",()=>ChooseCollectionPath(members)));
        var sort=new MenuItem{Header=L.Text("SORT_RESULTS"),Tag="SORT_RESULTS"};sort.Items.Add(MenuAction("SORT_LATENCY",()=>SortGroup(members,false)));sort.Items.Add(MenuAction("SORT_RELIABILITY",()=>SortGroup(members,true)));sort.Items.Add(MenuAction("CLEAR_TESTS",()=>ClearTests(members)));menu.Items.Add(sort);
        menu.Items.Add(MenuAction("TCP_CHECK",()=>TcpCollection(members)));
        MenuHeading(menu,"MENU_MANAGE");
        if(members[0].SourceId.Length>0){
            menu.Items.Add(MenuAction("UPDATE",()=>_ = UpdateCollection20(members[0].SourceId)));
            menu.Items.Add(MenuAction("SUBSCRIPTION_DETAILS",()=>ShowSubscriptionDetails21(members[0].SourceId)));
        }
        var rename = MenuAction("RENAME", () => Rename(members)); rename.IsEnabled = !busy && state.State != "connected"; menu.Items.Add(rename);
        var move=MenuAction("MOVE_COLLECTION",()=>MoveProfiles(members));move.IsEnabled=members.All(p=>p.SourceId.Length==0);menu.Items.Add(move);
        MenuHeading(menu,"SECTION_TRANSFER");
        menu.Items.Add(MenuAction("EXPORT_COLLECTION", () => ExportProfiles(members)));
        menu.Items.Add(MenuAction("SHARE_QR",()=>ShareQr(members)));
        menu.Items.Add(new Separator());
        var delete = MenuAction("DELETE_COLLECTION", () => DeleteCollection(members)); delete.IsEnabled = !busy && state.State != "connected"; menu.Items.Add(delete); return menu;
    }
    void Rename(List<Profile> members)
    {
        if (busy || state.State == "connected") return;
        var d = Dialog("RENAME", 500, 330); var c = new StackPanel { Margin = new(24) }; var input = new TextBox { Text = members[0].CollectionName }; c.Children.Add(input);
        c.Children.Add(Button("SAVE", () => { if (string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 120) return; var prior = members.Select(p => p.CollectionName).ToArray(); for (int i = 0; i < members.Count; i++) members[i].CollectionName = input.Text.Trim(); try { Save(); } catch { for (int i = 0; i < members.Count; i++) members[i].CollectionName = prior[i]; throw; } d.DialogResult = true; BuildCards(); }, true)); d.Content = c; d.ShowDialog();
    }
    void DeleteCollection(List<Profile> members)
    {
        if (busy || state.State == "connected" || !Confirm(L.Text("DELETE_COLLECTION_CONFIRM"))) return;
        var before = data.Profiles; var selected = data.SelectedId; data.Profiles = data.Profiles.Except(members).ToList(); if (members.Any(p => p.Id == selected)) data.SelectedId = "";
        try { Save(); } catch { data.Profiles = before; data.SelectedId = selected; throw; }
        BuildCards();
    }
    void ExportProfiles(IEnumerable<Profile> profiles)
    {
        try
        {
            var list = profiles.ToList(); if (list.Count == 0) return;
            // Exact original text, and never export a sharing-disabled native profile.
            if (list.Any(p => (p.Raw.StartsWith("rovunet://") || p.Raw.StartsWith("slipnet://")) && Importer.Fields(p.Raw)[34] != "1")) throw new SafeError("SHARING_DISABLED");
            if (!Confirm(L.Text("EXPORT_SECRET_CONFIRM"))) return;
            var save = new SaveFileDialog { FileName = "Rovunet-configs.json", Filter = "JSON|*.json" }; if (save.ShowDialog(this) != true) return;
            // Feed v1 preserves native v33 choices, restrictions and credential envelope verbatim.
            File.WriteAllText(save.FileName, JsonSerializer.Serialize(new { version = 1, items = list.Select(p => new { id = p.Id, link = p.Raw }) }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) { lastError = e is SafeError s ? s.Code : "SAVE_FAILED"; SetState(state); }
    }
    void CollectionTestDialog(List<Profile>? profiles = null, int attempts = 1) => StartCollectionTest(profiles,attempts,false);
    void DnsDialog()
    {
        var d = Dialog("DNS_CHECK", 580, 470); var c = new StackPanel { Margin = new(24) }; c.Children.Add(Text(L.Text("DNS_HINT"), 14, Muted));
        var address = new TextBox { Text = "1.1.1.1", FlowDirection = FlowDirection.LeftToRight }; c.Children.Add(address); var tcp = new CheckBox { Content = L.Text("TCP_ONLY"), IsChecked = true }; c.Children.Add(tcp); var result = Text("", 14); c.Children.Add(result);
        CancellationTokenSource? stop = null; bool active = false, closePending = false; var actions = new WrapPanel(); var start = Button("START_TEST", () => { }, true); var cancel = Button("STOP_TEST", () => stop?.Cancel()); cancel.IsEnabled = false; actions.Children.Add(start); actions.Children.Add(cancel); c.Children.Add(actions);
        d.Closing += (_, e) => { if (active) { e.Cancel = true; closePending = true; stop?.Cancel(); } };
        start.Click += async (_, _) =>
        {
            if (active) return; if (!IPAddress.TryParse(address.Text.Trim(), out var ip)) { result.Text = L.Text("DNS_IP_REQUIRED"); return; }
            active = true; address.IsEnabled = tcp.IsEnabled = start.IsEnabled = false; cancel.IsEnabled = true; stop = new(TimeSpan.FromSeconds(5));
            try
            {
                byte[] query = [0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 7, 101, 120, 97, 109, 112, 108, 101, 3, 99, 111, 109, 0, 0, 1, 0, 1]; RandomNumberGenerator.Fill(query.AsSpan(0, 2)); byte[] response;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                if (tcp.IsChecked == true)
                {
                    using var socket = new TcpClient(ip.AddressFamily); await socket.ConnectAsync(ip, 53, stop.Token); using var stream = socket.GetStream(); byte[] frame = [(byte)(query.Length >> 8), (byte)query.Length]; await stream.WriteAsync(frame, stop.Token); await stream.WriteAsync(query, stop.Token); await stream.ReadExactlyAsync(frame, stop.Token); int size = frame[0] * 256 + frame[1]; if (size is < 12 or > 4096) throw new SafeError("DNS_FAILED"); response = new byte[size]; await stream.ReadExactlyAsync(response, stop.Token);
                }
                else { using var socket = new UdpClient(ip.AddressFamily); socket.Connect(ip, 53); await socket.SendAsync(query, stop.Token); response = (await socket.ReceiveAsync(stop.Token)).Buffer; }
                if (response.Length < 12 || response[0] != query[0] || response[1] != query[1] || (response[2] & 0x80) == 0 || (response[3] & 15) != 0) throw new SafeError("DNS_FAILED");
                result.Text = L.Text("DNS_REPLIED") + " · " + clock.ElapsedMilliseconds + " ms";
            }
            catch { result.Text = L.Text(stop.IsCancellationRequested ? "CANCELLED" : "DNS_FAILED"); }
            finally { stop.Dispose(); stop = null; active = false; address.IsEnabled = tcp.IsEnabled = start.IsEnabled = true; cancel.IsEnabled = false; if (closePending) d.Close(); }
        };
        d.Content = c; d.ShowDialog();
    }
}

