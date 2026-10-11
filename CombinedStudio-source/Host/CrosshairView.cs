using W=System.Windows;
using M=System.Windows.Media;
using I=System.Windows.Media.Imaging;
namespace CombinedStudio;
internal sealed class CrosshairView:W.FrameworkElement {
 internal CrosshairDesign Design=new();internal bool PhysicalPixels;internal string ImageFolder="";
 string imageKey="";I.BitmapSource bitmap;
 internal static I.BitmapSource LoadImage(string path){using var stream=File.OpenRead(path);var decoder=I.BitmapDecoder.Create(stream,I.BitmapCreateOptions.PreservePixelFormat,I.BitmapCacheOption.OnLoad);if(decoder is not I.PngBitmapDecoder)throw new ArgumentException("Choose a PNG image.");var image=decoder.Frames[0];image.Freeze();return image;}
 internal string ImagePath(string file){if(System.IO.Path.GetFileName(file)!=file)throw new ArgumentException("Invalid image reference.");return System.IO.Path.Combine(ImageFolder,file);}
 protected override void OnRender(M.DrawingContext dc){base.OnRender(dc);var design=Design;var dpi=M.VisualTreeHelper.GetDpi(this);double scaleX=PhysicalPixels?1/dpi.DpiScaleX:1,scaleY=PhysicalPixels?1/dpi.DpiScaleY:1;
  dc.PushTransform(new M.TranslateTransform(ActualWidth/2,ActualHeight/2));dc.PushTransform(new M.ScaleTransform(scaleX,scaleY));dc.PushTransform(new M.RotateTransform(design.Rotation));dc.PushOpacity(design.Opacity/100d);
  var color=new M.SolidColorBrush((M.Color)M.ColorConverter.ConvertFromString(design.Color));var outline=new M.SolidColorBrush((M.Color)M.ColorConverter.ConvertFromString(design.OutlineColor));
  void Line(double x1,double y1,double x2,double y2){if(design.Outline>0)dc.DrawLine(new M.Pen(outline,design.Thickness+design.Outline*2),new(x1,y1),new(x2,y2));dc.DrawLine(new M.Pen(color,design.Thickness),new(x1,y1),new(x2,y2));}
  void Dot(){double radius=design.DotSize/2d;if(design.Outline>0)dc.DrawEllipse(outline,null,new(0,0),radius+design.Outline,radius+design.Outline);dc.DrawEllipse(color,null,new(0,0),radius,radius);}
  double gap=design.Gap,length=design.Length;
  if(design.Shape=="Cross"||design.Shape=="T shape"||design.Shape=="Ring + cross"){Line(-gap-length,0,-gap,0);Line(gap,0,gap+length,0);Line(0,gap,0,gap+length);if(design.Shape!="T shape")Line(0,-gap-length,0,-gap);}
  if(design.Shape=="Ring"||design.Shape=="Ring + cross"){if(design.Outline>0)dc.DrawEllipse(null,new M.Pen(outline,design.Thickness+design.Outline*2),new(0,0),design.Radius,design.Radius);dc.DrawEllipse(null,new M.Pen(color,design.Thickness),new(0,0),design.Radius,design.Radius);}
  if(design.Shape=="Chevron"){Line(-length,length/2,0,-length/2);Line(0,-length/2,length,length/2);}
  void Polygon(int sides,double radius,double angle=0,bool star=false){var points=new List<W.Point>();for(int i=0;i<sides;i++){double a=(angle+i*360d/sides)*Math.PI/180,r=star&&i%2==1?radius*.45:radius;points.Add(new W.Point(Math.Cos(a)*r,Math.Sin(a)*r));}for(int i=0;i<points.Count;i++){var a=points[i];var b=points[(i+1)%points.Count];Line(a.X,a.Y,b.X,b.Y);}}
  if(design.Shape=="Diamond")Polygon(4,design.Radius);
  if(design.Shape=="Triangle")Polygon(3,design.Radius,-90);
  if(design.Shape=="Square")Polygon(4,design.Radius,45);
  if(design.Shape=="Star")Polygon(10,design.Radius,-90,true);
  if(design.Shape=="Brackets"){double r=design.Radius,l=Math.Min(length,r);foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1}){Line(x*r,y*r,x*(r-l),y*r);Line(x*r,y*r,x*r,y*(r-l));}}
  if(design.Shape=="Double ring"){foreach(double radius in new[]{(double)design.Radius,Math.Max(2,design.Radius-5d)}){if(design.Outline>0)dc.DrawEllipse(null,new M.Pen(outline,design.Thickness+design.Outline*2),new(0,0),radius,radius);dc.DrawEllipse(null,new M.Pen(color,design.Thickness),new(0,0),radius,radius);}}
  if(design.Shape=="Burst"){for(int i=0;i<16;i++){double a=i*Math.PI/8;Line(Math.Cos(a)*design.Radius,Math.Sin(a)*design.Radius,Math.Cos(a)*(design.Radius+length),Math.Sin(a)*(design.Radius+length));}}
  if(design.Shape=="Image"){
   try{string path=ImagePath(design.ImageFile);if(imageKey!=path){bitmap=LoadImage(path);imageKey=path;}double width=design.ImageSize,height=width*bitmap.PixelHeight/bitmap.PixelWidth;if(height>design.ImageSize){height=design.ImageSize;width=height*bitmap.PixelWidth/bitmap.PixelHeight;}dc.DrawImage(bitmap,new W.Rect(-width/2,-height/2,width,height));}catch{bitmap=null;imageKey="";}
  }else if(design.Dot||design.Shape=="Dot")Dot();
  dc.Pop();dc.Pop();dc.Pop();dc.Pop();
 }
 internal void ExportPng(string file){var view=new CrosshairView{Design=Design.Copy(),ImageFolder=ImageFolder,Width=256,Height=256};view.Measure(new W.Size(256,256));view.Arrange(new W.Rect(0,0,256,256));view.UpdateLayout();var bitmap=new I.RenderTargetBitmap(256,256,96,96,M.PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new I.PngBitmapEncoder();encoder.Frames.Add(I.BitmapFrame.Create(bitmap));using var stream=File.Create(file);encoder.Save(stream);}
}
