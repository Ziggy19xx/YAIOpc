using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using I=System.Windows.Media.Imaging;
namespace CombinedStudio;
internal sealed partial class Shell {
 static IEnumerable<W.FrameworkElement> Elements(W.DependencyObject root){for(int i=0;i<M.VisualTreeHelper.GetChildrenCount(root);i++){var child=M.VisualTreeHelper.GetChild(root,i);if(child is W.FrameworkElement e)yield return e;foreach(var nested in Elements(child))yield return nested;}}
 static bool Visible(W.FrameworkElement element){for(W.DependencyObject current=element;current is W.FrameworkElement e;current=M.VisualTreeHelper.GetParent(current))if(e.Visibility!=W.Visibility.Visible)return false;return true;}
 internal async Task ReviewLayouts(string folder){
  Directory.CreateDirectory(folder);var report=new List<string>();var findings=new List<string>();
  try{
   var routes=new[]{"Overview","PC monitor","Disk explorer","Game library","Optimizations","Windows tuning","Game profiles","Session mode","Background apps","Startup apps","Drivers","Storage & apps","Maintenance","Diagnostics","Compare runs","Recovery","Updates & notes","About & sources","Hardware"}.Select(p=>(Area:"PC",Page:p)).Concat(new[]{"Studio","Headphone EQ","Soundboard + clips","Voice changer","Setup & help"}.Select(p=>(Area:"Audio",Page:p))).Concat(new[]{"FPS Counter","True Stretch","Crosshair Studio","Themes"}.Select(p=>(Area:"Custom",Page:p)));
   foreach(var route in routes){
    if(route.Area=="PC")Performance.SelectPage(route.Page);else if(route.Area=="Audio")Voice.SelectPage(route.Page);else Activate(route.Page);
    await Task.Delay(80);
    foreach(double width in new[]{1060d,1480d}){
     Window.Content=null;composition.Width=double.NaN;composition.Height=double.NaN;
     var capture=new C.Border{Child=composition,Width=width,Height=900,Resources=Window.Resources,Background=Window.Background};System.Windows.Documents.TextElement.SetForeground(capture,Window.Foreground);System.Windows.Documents.TextElement.SetFontFamily(capture,Window.FontFamily);System.Windows.Documents.TextElement.SetFontSize(capture,Window.FontSize);
     capture.Measure(new W.Size(width,900));capture.Arrange(new W.Rect(0,0,width,900));capture.UpdateLayout();
     capture.Measure(new W.Size(width,900));capture.Arrange(new W.Rect(0,0,width,900));capture.UpdateLayout();     string name=route.Area+"-"+route.Page.Replace(" ","-").Replace("&","and").Replace("+","plus")+"-"+width;
     var bitmap=new I.RenderTargetBitmap((int)width,900,96,96,M.PixelFormats.Pbgra32);bitmap.Render(composition);var encoder=new I.PngBitmapEncoder();encoder.Frames.Add(I.BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(folder,name+".png")))encoder.Save(stream);
     int count=0;
     foreach(var text in Elements(composition).OfType<C.TextBlock>().Where(t=>Visible(t)&&!string.IsNullOrWhiteSpace(t.Text)&&t.ActualWidth>0&&t.TextTrimming==W.TextTrimming.None)){
      if(text.FontFamily.Source.Contains("MDL2")||text.Text.Length<3)continue;
      for(W.DependencyObject parent=M.VisualTreeHelper.GetParent(text);parent is W.FrameworkElement box;parent=M.VisualTreeHelper.GetParent(parent)){
       if(box==composition)break;if(box is not C.Border&&box is not C.Grid)continue;
       var bounds=text.TransformToAncestor(box).TransformBounds(new W.Rect(text.RenderSize));
       if(box.ActualWidth>0&&(bounds.Right>box.ActualWidth+2||bounds.Left < -2)){findings.Add(name+" | "+text.Text.Replace("\n"," ")+" | "+box.GetType().Name+" "+Math.Round(bounds.Right)+" / "+Math.Round(box.ActualWidth));count++;break;}
      }
     }
     report.Add(name+" | rendered "+composition.ActualWidth+"x"+composition.ActualHeight+" | horizontal overflow candidates: "+count);     var workspace=route.Area=="Audio"?voiceRoot:route.Area=="PC"?performanceRoot:route.Page=="FPS Counter"?(W.FrameworkElement)fps:route.Page=="True Stretch"?(W.FrameworkElement)stretch:route.Page=="Crosshair Studio"?crosshair:themes;
     var scroll=workspace as C.ScrollViewer??Elements(workspace).OfType<C.ScrollViewer>().FirstOrDefault(x=>Visible(x)&&x.ViewportHeight>100&&x.ScrollableHeight>100);
     if(scroll!=null&&scroll.ScrollableHeight>100){foreach(double fraction in new[]{.5,1d}){scroll.ScrollToVerticalOffset(scroll.ScrollableHeight*fraction);composition.UpdateLayout();var detail=new I.RenderTargetBitmap((int)width,900,96,96,M.PixelFormats.Pbgra32);detail.Render(composition);var detailEncoder=new I.PngBitmapEncoder();detailEncoder.Frames.Add(I.BitmapFrame.Create(detail));using(var output=File.Create(Path.Combine(folder,name+(fraction==1?"-bottom":"-middle")+".png")))detailEncoder.Save(output);}scroll.ScrollToTop();composition.UpdateLayout();}
     capture.Child=null;Window.Content=composition;
    }
   }
   File.WriteAllLines(Path.Combine(folder,"layout-review.txt"),report);File.WriteAllLines(Path.Combine(folder,"overflow-candidates.txt"),findings);
  }catch(Exception ex){Environment.ExitCode=1;File.WriteAllText(Path.Combine(folder,"layout-error.txt"),ex.ToString());}
  finally{W.Application.Current.Shutdown(Environment.ExitCode);}
 }
}