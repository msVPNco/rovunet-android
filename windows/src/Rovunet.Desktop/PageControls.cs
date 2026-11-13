using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    Border OutlinedField(string key, Control control, string? hintKey=null)
    {
        control.Margin=new(0);control.MinHeight=Math.Max(control.MinHeight,48);control.Padding=new(12,10,12,8);
        control.Background=Brushes.Transparent;control.BorderThickness=new(0);
        AutomationProperties.SetName(control,L.Text(key));control.Tag ??= key;
        var field=new Grid();var outline=new Border{BorderBrush=GlassEdge(Tone("Outline")),BorderThickness=new(1),CornerRadius=new(6),Margin=new(0,8,0,0),Child=control};field.Children.Add(outline);
        var label=Text(L.Text(key),13,Muted);label.FontWeight=FontWeights.Medium;label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;label.MaxWidth=double.IsFinite(control.Width)?Math.Max(60,control.Width-8):340;label.ToolTip=L.Text(key);label.Background=Tone("Bg");label.Padding=new(4,0,4,0);label.Margin=new(10,0,10,0);label.HorizontalAlignment=HorizontalAlignment.Left;label.VerticalAlignment=VerticalAlignment.Top;field.Children.Add(label);
        var body=new StackPanel{Margin=new(0,0,0,18)};body.Children.Add(field);if(hintKey!=null)body.Children.Add(Text(L.Text(hintKey),13,Muted));
        return new Border{Child=body};
    }
    Border SettingsRow(CheckBox toggle,string? hintKey=null)
    {
        toggle.FontSize=17;toggle.Margin=new(0);toggle.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        var row=new StackPanel{Margin=new(0,2,0,8)};row.Children.Add(toggle);
        if(hintKey!=null){var hint=Text(L.Text(hintKey),13,Muted);hint.Margin=new(2,0,62,6);row.Children.Add(hint);}
        return new Border{Child=row,BorderBrush=Tone("Outline"),BorderThickness=new(0,0,0,1),Margin=new(0,0,0,6)};
    }
    Button SettingsAction(string key,Action action)
    {
        var b=Button(key,action);b.Background=Brushes.Transparent;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.MinHeight=52;b.FontSize=16;b.Margin=new(0,2,0,2);return b;
    }
    FrameworkElement ChoiceChips(ComboBox model)
    {
        var chips=new WrapPanel{Margin=new(0,4,0,12)};var buttons=new List<(Button Button,object Value)>();
        foreach(var item in model.Items)
        {
            var type=item.GetType();object value=type.GetProperty("Value")?.GetValue(item)??item;
            string label=type.GetProperty("Label")?.GetValue(item)?.ToString()??item.ToString()??"";
            var button=Button("SELECT",()=>model.SelectedValue=value);button.Content=label;AutomationProperties.SetName(button,label);button.FontSize=15;button.Padding=new(10,5,10,5);button.Margin=new(0,0,6,6);chips.Children.Add(button);buttons.Add((button,value));
        }
        void Refresh(){foreach(var (button,value) in buttons){bool selected=Equals(model.SelectedValue,value);button.Background=selected?Tone("Selected"):Brushes.Transparent;button.BorderBrush=selected?Accent:Tone("Outline");AutomationProperties.SetHelpText(button,selected?L.Text("SELECTED"):"");button.IsEnabled=model.IsEnabled;}}
        model.SelectionChanged+=(_,_)=>Refresh();model.IsEnabledChanged+=(_,_)=>Refresh();Refresh();return chips;
    }
}

