using W=System.Windows;
using C=System.Windows.Controls;
namespace CombinedStudio;
internal static class ResponsiveLayout {
 internal static void Columns(C.Grid grid,double breakpoint){
  var widths=grid.ColumnDefinitions.Select(x=>x.Width).ToArray();var children=grid.Children.Cast<W.FrameworkElement>().ToArray();var columns=children.Select(C.Grid.GetColumn).ToArray();bool? narrow=null;
  void Update(){if(grid.ActualWidth<=0)return;bool next=grid.ActualWidth<breakpoint;if(narrow==next)return;narrow=next;grid.RowDefinitions.Clear();
   if(next){for(int i=0;i<children.Length;i++)grid.RowDefinitions.Add(new C.RowDefinition{Height=W.GridLength.Auto});}
   for(int c=0;c<widths.Length;c++)grid.ColumnDefinitions[c].Width=next?(c==0?new W.GridLength(1,W.GridUnitType.Star):new W.GridLength(0)):widths[c];
   for(int i=0;i<children.Length;i++){C.Grid.SetColumn(children[i],next?0:columns[i]);C.Grid.SetRow(children[i],next?i:0);}
  }
  grid.SizeChanged+=(_,_)=>Update();grid.Loaded+=(_,_)=>Update();
 }
 internal static void Tiles(C.Primitives.UniformGrid grid,double minWidth,int maximum){void Update(){if(grid.ActualWidth>0){int columns=Math.Clamp((int)(grid.ActualWidth/minWidth),1,maximum);if(maximum==4&&columns==3)columns=2;if(grid.Columns!=columns)grid.Columns=columns;}}grid.SizeChanged+=(_,_)=>Update();grid.Loaded+=(_,_)=>Update();}
}