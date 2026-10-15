using System.Reflection;
using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using F=System.Windows.Forms;
using B=System.Windows.Data;

namespace VelvetVoice;
internal sealed class UiControls
{
    readonly List<Action> updates=new();
    bool syncing;
    internal static M.Brush Tone(string hex)=>CombinedStudio.ThemePalette.Brush(hex);
    internal static readonly M.Brush Ink=Tone("#F3ECE4"),Muted=Tone("#A39789"),Accent=Tone("#E8964A"),Line=Tone("#2B231D"),Panel=Tone("#1A1613"),Raised=Tone("#221C18");
    internal static C.TextBlock Text(string text,double size=13,M.Brush? color=null)=>new(){Text=text,FontSize=size,Foreground=color??Ink,TextWrapping=W.TextWrapping.Wrap,Margin=new W.Thickness(0,0,0,8)};
    internal static C.Border Card(W.UIElement content)=>new(){Child=content,Background=Panel,BorderBrush=Line,BorderThickness=new W.Thickness(1),CornerRadius=new W.CornerRadius(14),Padding=new W.Thickness(20),Margin=new W.Thickness(0,0,0,14)};
    internal static C.StackPanel Section(string title,string? description=null){var stack=new C.StackPanel();var heading=Text(title,17);heading.FontWeight=W.FontWeights.SemiBold;stack.Children.Add(heading);if(description!=null)stack.Children.Add(Text(description,12,Muted));return stack;}
    internal void Watch(Action action)=>updates.Add(action);
    internal void Sync(){if(syncing)return;syncing=true;try{foreach(var update in updates)update();}finally{syncing=false;}}
    internal void Write(Action action){if(syncing)return;try{action();Sync();}catch(Exception error){W.MessageBox.Show(StudioWindow.Owner,error.GetBaseException().Message,"Velvet Audio",W.MessageBoxButton.OK,W.MessageBoxImage.Information);}}
    void Observe(F.Control source,W.FrameworkElement target){Watch(()=>{target.IsEnabled=source.Enabled;});W.Automation.AutomationProperties.SetName(target,string.IsNullOrEmpty(source.AccessibleName)?source.Text:source.AccessibleName);}
    internal W.FrameworkElement Convert(F.Control source)
    {
        W.FrameworkElement view;
        switch(source)
        {
            case F.Button button:
                var action=new C.Button{Content=button.Text,FontSize=12,MinHeight=38};action.Click+=(_,_)=>Write(()=>MainForm.UiClick(button));Watch(()=>action.Content=button.Text);view=action;break;
            case F.ComboBox combo:
                var choice=new C.ComboBox{MinWidth=170,MaxWidth=650,MinHeight=40,Margin=new W.Thickness(0,0,12,10),HorizontalAlignment=W.HorizontalAlignment.Stretch};
                choice.SelectionChanged+=(_,_)=>Write(()=>combo.SelectedIndex=choice.SelectedIndex);string? comboKey=null;
                Watch(()=>{string next=string.Join("|",combo.Items.Cast<object>());if(next!=comboKey){comboKey=next;choice.ItemsSource=combo.Items.Cast<object>().ToArray();}choice.SelectedIndex=combo.SelectedIndex;});view=choice;break;
            case F.TrackBar slider:
                var scale=new C.Slider{Minimum=slider.Minimum,Maximum=slider.Maximum,TickFrequency=slider.SmallChange,SmallChange=slider.SmallChange,LargeChange=slider.LargeChange,IsSnapToTickEnabled=true,MinWidth=80,MinHeight=26,Margin=new W.Thickness(0,2,14,4)};
                scale.ValueChanged+=(_,_)=>Write(()=>slider.Value=Math.Clamp((int)Math.Round(scale.Value),slider.Minimum,slider.Maximum));Watch(()=>scale.Value=slider.Value);view=scale;break;
            case F.NumericUpDown number:
                var entry=new C.TextBox{Width=88,MinHeight=38,Margin=new W.Thickness(0,0,12,10),VerticalContentAlignment=W.VerticalAlignment.Center};
                void Commit(){if(decimal.TryParse(entry.Text,out var value))Write(()=>number.Value=Math.Clamp(value,number.Minimum,number.Maximum));else entry.Text=number.Value.ToString();}
                entry.LostKeyboardFocus+=(_,_)=>Commit();entry.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){Commit();e.Handled=true;}};Watch(()=>{if(!entry.IsKeyboardFocusWithin)entry.Text=number.Value.ToString("F"+number.DecimalPlaces);});view=entry;break;
            case F.CheckBox check:
                var caption=Text(check.Text);var toggle=new C.CheckBox{Content=caption,Margin=new W.Thickness(0,8,12,10)};toggle.Checked+=(_,_)=>Write(()=>check.Checked=true);toggle.Unchecked+=(_,_)=>Write(()=>check.Checked=false);Watch(()=>{toggle.IsChecked=check.Checked;caption.Text=check.Text;caption.MaxWidth=Math.Max(140,toggle.ActualWidth-40);});view=toggle;break;
            case F.Label label:
                var text=Text(label.Text,label.Font.Bold?17:13,label.Font.Bold?Ink:Muted);if(label.Font.Bold)text.FontWeight=W.FontWeights.SemiBold;Watch(()=>text.Text=label.Text);view=text;break;
            case F.TextBox box:
                var input=new C.TextBox{IsReadOnly=box.ReadOnly,Text=box.Text,MinHeight=38,MaxLength=box.MaxLength,Margin=new W.Thickness(0,0,12,10)};input.TextChanged+=(_,_)=>Write(()=>box.Text=input.Text);Watch(()=>{if(input.Text!=box.Text)input.Text=box.Text;});view=input;break;
            case F.TableLayoutPanel table:
                var rows=new C.StackPanel();int rowCount=Math.Max(table.RowCount,table.Controls.Cast<F.Control>().Select(x=>Position(table,x).Row+1).DefaultIfEmpty(0).Max());for(int row=0;row<rowCount;row++)rows.Children.Add(TableRow(table,row));view=rows;break;
            case F.FlowLayoutPanel flow:
                C.Panel layout=flow.FlowDirection==F.FlowDirection.TopDown?new C.StackPanel():new C.WrapPanel();foreach(F.Control child in flow.Controls)layout.Children.Add(Convert(child));view=layout;break;
            default:
                if(source.HasChildren){var children=new C.StackPanel();foreach(F.Control child in source.Controls)children.Children.Add(Convert(child));view=children;}
                else throw new NotSupportedException("Missing WPF control: "+source.GetType().Name);
                break;
        }
        Observe(source,view);return view;
    }
    internal C.Grid TableRow(F.TableLayoutPanel table,int row)
    {
        var grid=new C.Grid{Margin=new W.Thickness(0,0,0,8)};
        for(int column=0;column<table.ColumnCount;column++){
            var style=column<table.ColumnStyles.Count?table.ColumnStyles[column]:null;
            grid.ColumnDefinitions.Add(new C.ColumnDefinition{Width=style?.SizeType==F.SizeType.Absolute?new W.GridLength(Math.Min(180,style.Width)):new W.GridLength(style?.SizeType==F.SizeType.Percent?Math.Max(1,style.Width):1,W.GridUnitType.Star)});
        }
        foreach(var source in table.Controls.Cast<F.Control>().Where(x=>Position(table,x).Row==row).OrderBy(x=>Position(table,x).Column)){
            var view=Convert(source);C.Grid.SetColumn(view,Position(table,source).Column);C.Grid.SetColumnSpan(view,table.GetColumnSpan(source));grid.Children.Add(view);
        }
        return grid;
    }
    static (int Row,int Column) Position(F.TableLayoutPanel table,F.Control source){var cell=table.GetPositionFromControl(source);if(cell.Row>=0&&cell.Column>=0)return(cell.Row,cell.Column);int row=table.GetRow(source),column=table.GetColumn(source),index=table.Controls.IndexOf(source),columns=Math.Max(1,table.ColumnCount);return(row>=0?row:index/columns,column>=0?column:index%columns);}
    internal W.FrameworkElement SliderRow(string name,F.TrackBar source)
    {
        var row=new C.Grid{Margin=new W.Thickness(0,2,0,4)};row.ColumnDefinitions.Add(new C.ColumnDefinition{Width=new W.GridLength(160)});row.ColumnDefinitions.Add(new C.ColumnDefinition());row.ColumnDefinitions.Add(new C.ColumnDefinition{Width=new W.GridLength(72)});
        var label=Text(name,12,Muted);label.VerticalAlignment=W.VerticalAlignment.Center;row.Children.Add(label);var slider=Convert(source);C.Grid.SetColumn(slider,1);row.Children.Add(slider);var value=Text("",13,Ink);value.TextAlignment=W.TextAlignment.Right;value.VerticalAlignment=W.VerticalAlignment.Center;Watch(()=>value.Text=name=="Comp ratio"?$"{source.Value/10f:0.0}:1":$"{source.Value/10f:0.0} dB");C.Grid.SetColumn(value,2);row.Children.Add(value);return row;
    }
    public sealed record Pad(F.ListViewItem Source,string Name,string Detail);
    internal C.ListBox SoundPads(F.ListView source,Action play)
    {
        _=source.Handle;
        var panelFactory=new W.FrameworkElementFactory(typeof(C.WrapPanel));
        var pads=new C.ListBox{ItemsPanel=new C.ItemsPanelTemplate(panelFactory),Background=Panel,Foreground=Ink,BorderThickness=new W.Thickness(0),MinHeight=170,MaxHeight=440,HorizontalContentAlignment=W.HorizontalAlignment.Stretch,Margin=new W.Thickness(0,6,0,14)};
        var content=new W.FrameworkElementFactory(typeof(C.StackPanel));var name=new W.FrameworkElementFactory(typeof(C.TextBlock));name.SetBinding(C.TextBlock.TextProperty,new B.Binding("Name"));name.SetValue(C.TextBlock.FontWeightProperty,W.FontWeights.SemiBold);name.SetValue(C.TextBlock.FontSizeProperty,15.0);name.SetValue(C.TextBlock.TextWrappingProperty,W.TextWrapping.Wrap);name.SetValue(C.TextBlock.MarginProperty,new W.Thickness(0,0,0,8));content.AppendChild(name);var detail=new W.FrameworkElementFactory(typeof(C.TextBlock));detail.SetBinding(C.TextBlock.TextProperty,new B.Binding("Detail"));detail.SetValue(C.TextBlock.FontSizeProperty,11.0);detail.SetValue(C.TextBlock.ForegroundProperty,Muted);detail.SetValue(C.TextBlock.TextWrappingProperty,W.TextWrapping.Wrap);content.AppendChild(detail);pads.ItemTemplate=new W.DataTemplate{VisualTree=content};
        var style=new W.Style(typeof(C.ListBoxItem),(W.Style)StudioWindow.Owner!.FindResource(typeof(C.ListBoxItem)));style.Setters.Add(new W.Setter(W.FrameworkElement.WidthProperty,220.0));style.Setters.Add(new W.Setter(W.FrameworkElement.MinHeightProperty,100.0));style.Setters.Add(new W.Setter(W.FrameworkElement.MarginProperty,new W.Thickness(0,0,10,10)));pads.ItemContainerStyle=style;
        Pad[] entries=Array.Empty<Pad>();string? key=null;
        pads.SelectionChanged+=(_,_)=>Write(()=>{foreach(F.ListViewItem item in source.Items)item.Selected=false;if(pads.SelectedItem is Pad selected&&source.Items.Contains(selected.Source)){selected.Source.Selected=true;selected.Source.Focused=true;}});
        pads.MouseDoubleClick+=(_,_)=>Write(play);
        Watch(()=>{string next=string.Join("|",source.Items.Cast<F.ListViewItem>().Select(x=>string.Join("/",x.SubItems.Cast<F.ListViewItem.ListViewSubItem>().Select(s=>s.Text))));if(key!=next||entries.Length!=source.Items.Count||entries.Where((x,i)=>x.Source!=source.Items[i]).Any()){key=next;entries=source.Items.Cast<F.ListViewItem>().Select(x=>new Pad(x,x.Text,string.Join(" · ",x.SubItems.Cast<F.ListViewItem.ListViewSubItem>().Skip(1).Select(s=>s.Text)))).ToArray();pads.ItemsSource=entries;}pads.SelectedItem=entries.FirstOrDefault(x=>x.Source.Selected);});return pads;
    }
}
