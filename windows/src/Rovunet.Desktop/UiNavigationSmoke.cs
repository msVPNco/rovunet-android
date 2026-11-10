using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void NavigationSmoke(Dictionary<string, bool> checks)
    {
        double width = Width, height = Height;
        string query = search.Text, selected = data.SelectedId;
        var profiles = data.Profiles;
        var savedMeasurements = data.Measurements;
        var runtimeMeasurements = measurements.ToDictionary();
        try
        {
            Width = 1000; Height = 740; UpdateLayout();
            ShowSearch(); search.Text = "UI fixture"; search.Focus(); search.Select(2, 3);
            Width = 760; UpdateLayout();
            checks["resize_preserves_search_and_caret"] = search.Text == "UI fixture" && search.IsKeyboardFocused && search.SelectionStart == 2 && search.SelectionLength == 3;
            search.Clear();
            var row = rows[5]; row.Select.Focus(); mainScroll.ScrollToVerticalOffset(180); UpdateLayout();
            double before = mainScroll.VerticalOffset; string profileId = row.Profile.Id;
            Width = 1000; UpdateLayout();
            checks["resize_preserves_profile_focus"] = ReferenceEquals(Keyboard.FocusedElement, rows.Single(r => r.Profile.Id == profileId).Select);
            checks["resize_preserves_scroll"] = Math.Abs(mainScroll.VerticalOffset - before) < 1;
            checks["resize_preserves_selection"] = data.SelectedId == selected;

            SetState(new());
            var group = SelectedGroup();
            var first = group[0]; var second = group[1];
            data.Measurements = new Dictionary<string, PathMeasurement>
            {
                [first.Id] = new() { Verified = true, ElapsedMs = 200, Attempts = 3, Passes = 3, SampledAt = DateTimeOffset.UtcNow },
                [second.Id] = new() { Verified = true, ElapsedMs = 50, Attempts = 3, Passes = 1, SampledAt = DateTimeOffset.UtcNow },
                ["other-fixture-account"] = new() { Attempts = 1 }
            };
            int closeCount = 0;
            Button FindMenu(string key)
            {
                var panel = BuildCollectionManagement(() => closeCount++);
                var host = new Border { Child = panel, Width = 620 };
                host.Measure(new Size(620, double.PositiveInfinity)); host.Arrange(new Rect(0, 0, 620, host.DesiredSize.Height)); host.UpdateLayout();
                return Visuals<Button>(host).Single(b => b.Content?.ToString() == L.Text(key));
            }
            void Click(string key)
            {
                var item = FindMenu(key);
                if (!item.IsEnabled) throw new InvalidOperationException("Disabled fixture action");
                item.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            }
            Click("SORT_LATENCY");
            checks["menu_latency_sort_orders_verified_paths"] = data.Profiles.First(p => group.Contains(p)).Id == second.Id;
            Click("SORT_RELIABILITY");
            checks["menu_reliability_sort_uses_pass_ratio"] = data.Profiles.First(p => group.Contains(p)).Id == first.Id;
            Click("CLEAR_TESTS");
            checks["menu_clear_tests_scoped_to_collection"] = group.All(p => !data.Measurements.ContainsKey(p.Id)) && data.Measurements.ContainsKey("other-fixture-account");
            checks["menu_actions_close_before_running"] = closeCount == 3;
            checks["menu_sort_disabled_without_measurements"] = !FindMenu("SORT_LATENCY").IsEnabled && !FindMenu("CLEAR_TESTS").IsEnabled;
            SetState(new() { State = "connected" });
            checks["menu_connected_blocks_collection_mutation"] = !FindMenu("REGROUP").IsEnabled && !FindMenu("DUPLICATES").IsEnabled;
            _=Dispatcher.BeginInvoke(()=>{var page=DialogWindow.OpenPages.Single();checks["connected_subscription_preview_available"]=Visuals<TextBox>(page.PageVisual).Any(t=>Equals(t.Tag,"SUBSCRIPTION_NAME"));page.Close();},System.Windows.Threading.DispatcherPriority.ApplicationIdle);SubscriptionDialog();
        }
        finally
        {
            data.Profiles = profiles; data.Measurements = savedMeasurements; data.SelectedId = selected;
            measurements.Clear(); foreach (var pair in runtimeMeasurements) measurements[pair.Key] = pair.Value;
            Width = width; Height = height; Build(); search.Text = query; SetState(new()); UpdateLayout();
        }
    }
}
