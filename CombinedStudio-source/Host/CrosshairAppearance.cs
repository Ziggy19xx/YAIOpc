using W=System.Windows;
using M=System.Windows.Media;
using I=System.Windows.Media.Imaging;
namespace CombinedStudio;
internal static class CrosshairAppearance {
 internal static readonly string[] Finishes={"Solid","Horizontal gradient","Vertical gradient","Radial gradient","Stripes","Checker","Dots"};
 // Input is only the crosshair drawing, never a screen capture or game surface.
 internal static M.Drawing Apply(M.Drawing drawing,CrosshairDesign design,double dpiX,double dpiY,W.Rect bounds){
  if(bounds.IsEmpty||bounds.Width<=0||bounds.Height<=0)return drawing;
  int width=(int)Math.Ceiling(bounds.Width*dpiX),height=(int)Math.Ceiling(bounds.Height*dpiY);
  if(width>4096||height>4096)return drawing;
  var visual=new M.DrawingVisual();using(var dc=visual.RenderOpen()){dc.PushTransform(new M.TranslateTransform(-bounds.Left,-bounds.Top));dc.DrawDrawing(drawing);}
  var target=new I.RenderTargetBitmap(width,height,96*dpiX,96*dpiY,M.PixelFormats.Pbgra32);target.Render(visual);
  var pixels=new byte[width*height*4];target.CopyPixels(pixels,width*4,0);
  if(design.Hue!=0||design.Finish!="Solid")Transform(pixels,width,height,design.Hue,design.Finish,design.TextureStrength);
  var bitmap=I.BitmapSource.Create(width,height,96*dpiX,96*dpiY,M.PixelFormats.Pbgra32,null,pixels,width*4);bitmap.Freeze();
  return new M.ImageDrawing(bitmap,new W.Rect(bounds.Left,bounds.Top,width/dpiX,height/dpiY));
 }
 internal static void Transform(byte[] pixels,int width,int height,int hue,string finish,int strength){
  double amount=strength/100d;
  int left=width,top=height,right=0,bottom=0;
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[(y*width+x)*4+3]>0){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
  for(int y=0;y<height;y++)for(int x=0;x<width;x++){
   int i=(y*width+x)*4;double alpha=pixels[i+3];if(alpha==0)continue;
   double r=pixels[i+2]/alpha,g=pixels[i+1]/alpha,b=pixels[i]/alpha;
   double max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),delta=max-min;
   // Keep dark outlines intact; hue alone preserves neutral white/gray pixels.
   if(max<.08)continue;
   double h=delta==0?0:max==r?60*((g-b)/delta%6):max==g?60*((b-r)/delta+2):60*((r-g)/delta+4);
   double s=max==0?0:delta/max;
   double nx=(double)(x-left)/Math.Max(1,right-left),ny=(double)(y-top)/Math.Max(1,bottom-top);
   double ramp=finish switch{"Horizontal gradient"=>nx,"Vertical gradient"=>ny,"Radial gradient"=>Math.Min(1,Math.Sqrt(Math.Pow(nx-.5,2)+Math.Pow(ny-.5,2))*Math.Sqrt(2)),_=>.5};
   bool gradient=finish.EndsWith("gradient",StringComparison.Ordinal);
   h=(h+hue+(gradient?(ramp-.5)*180*amount:0)+720)%360;
   if(gradient&&s<.1)s=amount;
   bool dark=finish switch{"Stripes"=>(x+y)/3%2==1,"Checker"=>(x/3+y/3)%2==1,"Dots"=>x%4==0&&y%4==0,_=>false};
   double v=max*(dark?1-.7*amount:1),c=v*s,t=c*(1-Math.Abs(h/60%2-1)),m=v-c;
   (double R,double G,double B) rgb=h switch{<60=>(c,t,0),<120=>(t,c,0),<180=>(0,c,t),<240=>(0,t,c),<300=>(t,0,c),_=>(c,0,t)};
   pixels[i+2]=(byte)Math.Clamp(Math.Round((rgb.R+m)*alpha),0,alpha);pixels[i+1]=(byte)Math.Clamp(Math.Round((rgb.G+m)*alpha),0,alpha);pixels[i]=(byte)Math.Clamp(Math.Round((rgb.B+m)*alpha),0,alpha);
  }
 }
}
