using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using X=System.Windows.Markup;
namespace CombinedStudio;
public sealed class CyberFrame:C.Decorator {
 public static readonly W.DependencyProperty ActiveProperty=W.DependencyProperty.Register(nameof(Active),typeof(bool),typeof(CyberFrame),new W.FrameworkPropertyMetadata(false,W.FrameworkPropertyMetadataOptions.AffectsRender));
 public bool Active {get=>(bool)GetValue(ActiveProperty);set=>SetValue(ActiveProperty,value);}
 protected override void OnRender(M.DrawingContext dc){
  base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<2||h<2)return;double cut=Math.Min(9,h/4);
  var shape=new M.StreamGeometry();using(var ctx=shape.Open()){ctx.BeginFigure(new W.Point(cut,1),true,true);ctx.PolyLineTo(new[]{new W.Point(w-cut-1,1),new W.Point(w-1,cut),new W.Point(w-1,h-cut-1),new W.Point(w-cut-1,h-1),new W.Point(cut,h-1),new W.Point(1,h-cut-1),new W.Point(1,cut)},true,false);}shape.Freeze();
  var edge=CyberSidebar.Brush(Active?"#FFD347":IsMouseOver?"#44CDD0":"#24575A");dc.DrawGeometry(CyberSidebar.Brush(Active?"#211D0C":"#061416"),new M.Pen(edge,Active?1.6:1),shape);
  dc.PushClip(shape);for(double y=3;y<h;y+=4)dc.DrawLine(new M.Pen(CyberSidebar.Brush(Active?"#183A310B":"#12005053"),1),new W.Point(2,y),new W.Point(w-2,y));
  if(Active&&w>40){for(double x=5;x<w;x+=5)for(double y=5;y<h;y+=5)dc.DrawRectangle(CyberSidebar.Brush("#16FFCF35"),null,new W.Rect(x,y,1,1));for(int i=0;i<4;i++)dc.DrawRectangle(edge,null,new W.Rect(w-12,h/2-8+i*5,2,2));}
  dc.Pop();dc.DrawLine(new M.Pen(edge,2),new W.Point(1,cut),new W.Point(cut,1));dc.DrawLine(new M.Pen(edge,2),new W.Point(w-cut-1,h-1),new W.Point(w-1,h-cut-1));
 }
 protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e){base.OnMouseEnter(e);InvalidateVisual();}
 protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e){base.OnMouseLeave(e);InvalidateVisual();}
}
internal static class CyberSidebar {
 internal static M.Brush Brush(string hex){var b=(M.SolidColorBrush)new M.BrushConverter().ConvertFromString(hex);b.Freeze();return b;}
 internal static M.Brush Background(){var group=new M.DrawingGroup();group.Children.Add(new M.GeometryDrawing(Brush("#040E10"),null,new M.RectangleGeometry(new W.Rect(0,0,8,8))));group.Children.Add(new M.GeometryDrawing(Brush("#142D7678"),null,new M.RectangleGeometry(new W.Rect(0,0,8,1))));var brush=new M.DrawingBrush(group){TileMode=M.TileMode.Tile,ViewportUnits=M.BrushMappingMode.Absolute,Viewport=new W.Rect(0,0,8,4),Stretch=M.Stretch.Fill};brush.Freeze();return brush;}
 internal static void Install(W.Window window){
  const string styles="""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:CombinedStudio;assembly=YAIOpc">
 <Style x:Key="CyberNav" TargetType="RadioButton">
  <Setter Property="FontFamily" Value="Consolas"/><Setter Property="FontSize" Value="14"/><Setter Property="Foreground" Value="#F0EBDC"/><Setter Property="Margin" Value="0,0,0,9"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton"><local:CyberFrame Active="{TemplateBinding IsChecked}" MinHeight="46"><Border Padding="12,12,18,12"><ContentPresenter VerticalAlignment="Center"/></Border></local:CyberFrame><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter Property="Foreground" Value="#FFD347"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="Foreground" Value="#FFD347"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <ControlTemplate x:Key="CyberGroup" TargetType="Expander">
  <StackPanel><ToggleButton x:Name="Toggle" Focusable="True" IsChecked="{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}" Foreground="{TemplateBinding Foreground}" Content="{TemplateBinding Header}" FontFamily="Consolas" FontSize="14" Cursor="Hand">
   <ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><local:CyberFrame Active="{Binding Tag,RelativeSource={RelativeSource AncestorType=Expander}}"><Grid MinHeight="46" Margin="12,10,14,10"><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/></Grid.ColumnDefinitions><ContentPresenter VerticalAlignment="Center"/><TextBlock x:Name="Arrow" Grid.Column="1" Text="›" FontSize="25" Foreground="#36C2CB" VerticalAlignment="Center" HorizontalAlignment="Right"/></Grid></local:CyberFrame><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Arrow" Property="Text" Value="⌄"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="Foreground" Value="#FFD347"/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
  </ToggleButton><ContentPresenter x:Name="Items" Visibility="Collapsed" ContentSource="Content"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="Items" Property="Visibility" Value="Visible"/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate>
 <Style x:Key="CyberStartup" TargetType="CheckBox"><Setter Property="FontFamily" Value="Consolas"/><Setter Property="FontSize" Value="12"/><Setter Property="Foreground" Value="#F0EBDC"/><Setter Property="Cursor" Value="Hand"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal"><local:CyberFrame Width="22" Height="22" Active="{TemplateBinding IsChecked}" Margin="0,0,10,0"><Path x:Name="Mark" Data="M 0,5 L 4,9 L 11,0" Stroke="#FFD347" StrokeThickness="2" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" Width="11" Height="9" Stretch="Uniform" HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="Collapsed"/></local:CyberFrame><ContentPresenter VerticalAlignment="Center"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Mark" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="Foreground" Value="#FFD347"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>
""";
  window.Resources.MergedDictionaries.Add((W.ResourceDictionary)X.XamlReader.Parse(styles));
 }
 internal static C.StackPanel Caption(string glyph,string text){var row=new C.StackPanel{Orientation=C.Orientation.Horizontal};row.Children.Add(new C.TextBlock{Text=glyph,FontFamily=new M.FontFamily("Segoe MDL2 Assets"),FontSize=19,Width=32,Foreground=Brush("#36C2CB"),VerticalAlignment=W.VerticalAlignment.Center});row.Children.Add(new C.TextBlock{Text=text,FontFamily=new M.FontFamily("Consolas"),FontSize=14,VerticalAlignment=W.VerticalAlignment.Center});return row;}
 internal static W.UIElement Divider()=>new C.Border{Height=1,Background=Brush("#36787B"),Margin=new W.Thickness(0,8,0,13)};
}