using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    static IEnumerable<FrameworkElement> LayoutElements(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element) yield return element;
            foreach (var nested in LayoutElements(child)) yield return nested;
        }
    }

    void RebuildForResize()
    {
        string query = search.Text;
        bool searchVisible = search.Visibility == Visibility.Visible;
        int caret = search.SelectionStart, length = search.SelectionLength;
        double offset = mainScroll.VerticalOffset;
        var focused = Keyboard.FocusedElement as FrameworkElement;
        bool searchFocused = search.IsKeyboardFocusWithin;
        string name = focused == null ? "" : AutomationProperties.GetName(focused);
        var type = focused?.GetType();
        var matches = LayoutElements((DependencyObject)Content)
            .Where(e => e.GetType() == type && AutomationProperties.GetName(e) == name).ToList();
        int index = focused == null ? -1 : matches.IndexOf(focused);
        Build(); search.Text = query; search.Visibility = searchVisible ? Visibility.Visible : Visibility.Collapsed; UpdateLayout();
        if (searchFocused)
        {
            search.Focus(); search.Select(Math.Min(caret, query.Length), Math.Min(length, query.Length - Math.Min(caret, query.Length)));
        }
        else if (index >= 0 && name.Length > 0)
        {
            var replacement = LayoutElements((DependencyObject)Content)
                .Where(e => e.GetType() == type && AutomationProperties.GetName(e) == name).ElementAtOrDefault(index);
            if (replacement is { IsEnabled: true, IsVisible: true }) replacement.Focus();
        }
        // Focus may call BringIntoView; restore the user's reading position afterwards.
        UpdateLayout(); mainScroll.ScrollToVerticalOffset(offset); UpdateLayout();
    }
}
