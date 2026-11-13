using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Rovunet.Core;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    DialogWindow Dialog(string key, double width = 500, double height = 740)
    {
        var d=new DialogWindow(this,L.Text(key),Math.Min(width,500),Math.Max(height,620));d.UsePageChrome();return d;
    }
    void Info(string key, string text)
    {
        var d = Dialog(key, 620, 450); d.Content = new TextBlock { Text = text, Margin = new(24) }; d.ShowDialog();
    }
    bool Confirm(string text, string fingerprint = "")
    {
        var d = Dialog("CONFIRM", 600, 400); var stack = new StackPanel { Margin = new(24) }; stack.Children.Add(Text(text));
        if (fingerprint.Length > 0)
        {
            stack.Children.Add(new TextBox { Text = fingerprint, IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(Button("COPY", () => Clipboard.SetText(fingerprint)));
        }
        var yes = Button("APPROVE", () => d.DialogResult = true, true); stack.Children.Add(yes); d.Content = stack; return d.ShowDialog() == true;
    }
    void AboutDialog() => BuildAboutSupport26().ShowDialog();
    void ImportDialog() => AddConfiguration();
    void Edit(Profile p)
    {
        if (busy) return;
        if (state.State == "connected" && activeProfile?.Id == p.Id)
        {
            if (!Confirm(L.Text("DISCONNECT_EDIT"))) return;
            _ = Run(async () => { await Disconnect(); _ = Dispatcher.BeginInvoke(() => Editor(p)); }); return;
        }
        Editor(p);
    }
    void Editor(Profile? existing, bool rawOnly = false)
    {
        if (busy) return;
        if(!rawOnly&&existing!=null&&ChainProfiles.IsChain(existing.Raw)){BuildChainEditor25(existing).ShowDialog();return;}
        if(!rawOnly&&existing!=null&&AlternativeProfiles.Handles(existing.Raw)){BuildAlternativeEditor25(AlternativeProfiles.Kind(existing.Raw),existing).ShowDialog();return;}
        if(!rawOnly&&existing!=null&&ExtraProfileEditing.Read(existing)!=null){BuildExtraProtocolEditor(existing).ShowDialog();return;}
        if (!rawOnly && existing != null && ProfileEditing.IsDns(existing)) { DnsProfileEditor(existing); return; }
        if(!rawOnly&&existing!=null&&CoreProfileEditing.CanEdit(existing)){BuildCoreProfileEditor(existing).ShowDialog();return;}
        if(!rawOnly && existing!=null && EndpointEditing.Read(existing)!=null){BuildEndpointEditor(existing).ShowDialog();return;}
        BuildRawEditor(existing).ShowDialog();
    }
    DialogWindow BuildRawEditor(Profile? existing,string? draft=null,Action<string>? applyRaw=null)
    {
        var d = Dialog(existing == null ? "ADD" : "EDIT_PROFILE", 500, 800); var layout = new DockPanel { Margin = new(18) };d.UsePageChrome();
        var hint = Text(L.Text("IMPORT_HINT"), 13, Muted); DockPanel.SetDock(hint, Dock.Top); layout.Children.Add(hint);
        var input = new TextBox { Text = draft ?? existing?.Raw ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FlowDirection = FlowDirection.LeftToRight, MinHeight = 200, AllowDrop = true };
        System.Windows.Automation.AutomationProperties.SetName(input, L.Text("ADD"));input.Tag="RAW_PROFILE";
        var bottom = new StackPanel(); var error = Text("", 13, Red); bottom.Children.Add(error);
        void ReadFile(string path) { try { if (new FileInfo(path).Length > Importer.MaxBytes) throw new SafeError("IMPORT_TOO_LARGE"); input.Text = File.ReadAllText(path, new UTF8Encoding(false, true)); error.Text = ""; } catch (Exception e) { error.Text = L.Text(e is SafeError s ? s.Code : "INVALID_IMPORT"); } }
        var file = Button("CHOOSE_FILE", () => { var picker = new OpenFileDialog { Filter = "Text / JSON / WireGuard|*.txt;*.json;*.conf|All files|*.*" }; if (picker.ShowDialog(d.NativeOwner) == true) ReadFile(picker.FileName); });
        input.PreviewDragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        input.PreviewDrop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files) ReadFile(files[0]); e.Handled = true; };
        var actions = new WrapPanel(); actions.Children.Add(file);
        var apply = Button(existing==null?"IMPORT":"SAVE", () =>
        {
            try
            {
                var profiles = Importer.Read(input.Text); var previous = data.Profiles.ToList(); int added = 0, duplicates = 0;
                if(applyRaw!=null){if(profiles.Count!=1)throw new SafeError("SINGLE_PROFILE_REQUIRED");applyRaw(profiles[0].Raw);d.DialogResult=true;return;}
                if (existing != null) { if (profiles.Count != 1) throw new SafeError("SINGLE_PROFILE_REQUIRED"); SaveEdited(existing, profiles[0].Raw); d.DialogResult = true; return; }
                else foreach (var p in profiles) { if (data.Profiles.Any(x => x.Raw == p.Raw)) { duplicates++; continue; } data.Profiles.Add(p); added++; }
                try { Save(); } catch { data.Profiles = previous; throw; }
                BuildCards(); Notify(L.Text("IMPORT_ADDED") + " " + added + " · " + L.Text("IMPORT_DUPLICATES") + " " + duplicates); d.DialogResult = true;
            }
            catch (Exception e) { error.Text = L.Text(e is SafeError se ? se.Code : "INVALID_IMPORT"); }
        }, true); if(existing!=null)d.SetPageAction(apply);else actions.Children.Add(apply); bottom.Children.Add(actions); bottom.Children.Add(Text(L.Text("IMPORT_KEYS"), 12, Muted));
        input.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); e.Handled = true; } };
        DockPanel.SetDock(bottom, Dock.Bottom); layout.Children.Add(bottom); layout.Children.Add(input); d.Content = layout; d.Loaded += (_, _) => input.Focus();return d;
    }
}

