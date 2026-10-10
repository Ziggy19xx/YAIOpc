using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace FramePilot {
 // Minimal reader for the two supplied triangle GLBs. No web renderer or Qt runtime.
 internal sealed class GlbModel {
  Dictionary<string,object> doc;byte[] binary;object[] views,accessors,materials,textures,images,meshes,nodes;
  static Dictionary<string,object> D(object o){return (Dictionary<string,object>)o;}
  static object[] A(Dictionary<string,object> d,string key){return d.ContainsKey(key)?((IEnumerable)d[key]).Cast<object>().ToArray():new object[0];}
  static int I(Dictionary<string,object> d,string key,int fallback=0){return d.ContainsKey(key)?Convert.ToInt32(d[key]):fallback;}
  public static Model3DGroup Load(string path){return new GlbModel().Read(path);}
  Model3DGroup Read(string path){
   using(var r=new BinaryReader(File.OpenRead(path))){if(r.ReadUInt32()!=0x46546c67||r.ReadInt32()!=2)throw new InvalidDataException("Unsupported model file");r.ReadInt32();int count=r.ReadInt32();if(r.ReadInt32()!=0x4e4f534a||count<1||count>8*1024*1024)throw new InvalidDataException("Invalid model metadata");doc=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Deserialize<Dictionary<string,object>>(System.Text.Encoding.UTF8.GetString(r.ReadBytes(count)));count=r.ReadInt32();if(r.ReadInt32()!=0x004e4942||count>32*1024*1024)throw new InvalidDataException("Invalid model data");binary=r.ReadBytes(count);}
   views=A(doc,"bufferViews");accessors=A(doc,"accessors");materials=A(doc,"materials");textures=A(doc,"textures");images=A(doc,"images");meshes=A(doc,"meshes");nodes=A(doc,"nodes");
   var result=new Model3DGroup();var scenes=A(doc,"scenes");foreach(object id in A(D(scenes[I(doc,"scene")]),"nodes"))result.Children.Add(Node(Convert.ToInt32(id),0));
   var bounds=result.Bounds;double largest=Math.Max(bounds.SizeX,Math.Max(bounds.SizeY,bounds.SizeZ));if(largest<=0)throw new InvalidDataException("Empty model");var transform=new Transform3DGroup();transform.Children.Add(new TranslateTransform3D(-bounds.X-bounds.SizeX/2,-bounds.Y-bounds.SizeY/2,-bounds.Z-bounds.SizeZ/2));transform.Children.Add(new ScaleTransform3D(2/largest,2/largest,2/largest));result.Transform=transform;result.Freeze();return result;
  }
  Model3DGroup Node(int index,int depth){
   if(depth>100)throw new InvalidDataException("Model hierarchy too deep");var n=D(nodes[index]);var group=new Model3DGroup();
   if(n.ContainsKey("mesh"))foreach(object raw in A(D(meshes[I(n,"mesh")]),"primitives")){
    var p=D(raw);if(I(p,"mode",4)!=4)continue;var attributes=D(p["attributes"]);var positions=ReadValues(I(attributes,"POSITION"),3);var geometry=new MeshGeometry3D();for(int i=0;i<positions.Length;i+=3)geometry.Positions.Add(new Point3D(positions[i],positions[i+1],positions[i+2]));
    if(attributes.ContainsKey("NORMAL")){var normal=ReadValues(I(attributes,"NORMAL"),3);for(int i=0;i<normal.Length;i+=3)geometry.Normals.Add(new Vector3D(normal[i],normal[i+1],normal[i+2]));}
    if(attributes.ContainsKey("TEXCOORD_0")){var uv=ReadValues(I(attributes,"TEXCOORD_0"),2);for(int i=0;i<uv.Length;i+=2)geometry.TextureCoordinates.Add(new Point(uv[i],uv[i+1]));}
    if(p.ContainsKey("indices"))foreach(double id in ReadValues(I(p,"indices"),1))geometry.TriangleIndices.Add((int)id);else for(int i=0;i<geometry.Positions.Count;i++)geometry.TriangleIndices.Add(i);
    var material=Material(I(p,"material",-1));group.Children.Add(new GeometryModel3D(geometry,material){BackMaterial=material});
   }
   foreach(object child in A(n,"children"))group.Children.Add(Node(Convert.ToInt32(child),depth+1));
   if(n.ContainsKey("matrix")){double[] m=A(n,"matrix").Select(Convert.ToDouble).ToArray();group.Transform=new MatrixTransform3D(new Matrix3D(m[0],m[1],m[2],m[3],m[4],m[5],m[6],m[7],m[8],m[9],m[10],m[11],m[12],m[13],m[14],m[15]));}
   else {var t=new Transform3DGroup();if(n.ContainsKey("scale")){var a=A(n,"scale").Select(Convert.ToDouble).ToArray();t.Children.Add(new ScaleTransform3D(a[0],a[1],a[2]));}if(n.ContainsKey("rotation")){var a=A(n,"rotation").Select(Convert.ToDouble).ToArray();t.Children.Add(new RotateTransform3D(new QuaternionRotation3D(new Quaternion(a[0],a[1],a[2],a[3]))));}if(n.ContainsKey("translation")){var a=A(n,"translation").Select(Convert.ToDouble).ToArray();t.Children.Add(new TranslateTransform3D(a[0],a[1],a[2]));}group.Transform=t;}
   return group;
  }
  double[] ReadValues(int index,int components){var a=D(accessors[index]);var view=D(views[I(a,"bufferView")]);int type=I(a,"componentType"),size=type==5126||type==5125?4:type==5123?2:1,count=I(a,"count"),start=I(view,"byteOffset")+I(a,"byteOffset"),stride=I(view,"byteStride",size*components);if(count<0||count>2000000)throw new InvalidDataException("Model accessor too large");var result=new double[count*components];for(int row=0;row<count;row++)for(int col=0;col<components;col++){int pos=start+row*stride+col*size;result[row*components+col]=type==5126?BitConverter.ToSingle(binary,pos):type==5125?BitConverter.ToUInt32(binary,pos):type==5123?BitConverter.ToUInt16(binary,pos):binary[pos];}return result;}
  Material Material(int index){Brush brush=new SolidColorBrush(Color.FromRgb(120,130,145));if(index>=0){var m=D(materials[index]);if(m.ContainsKey("pbrMetallicRoughness")){var p=D(m["pbrMetallicRoughness"]);if(p.ContainsKey("baseColorTexture")){var texture=D(textures[I(D(p["baseColorTexture"]),"index")]);var img=D(images[I(texture,"source")]);var view=D(views[I(img,"bufferView")]);using(var stream=new MemoryStream(binary,I(view,"byteOffset"),I(view,"byteLength"))){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();brush=new ImageBrush(bitmap);}}
    else if(p.ContainsKey("baseColorFactor")){var a=A(p,"baseColorFactor").Select(Convert.ToDouble).ToArray();brush=new SolidColorBrush(Color.FromScRgb((float)a[3],(float)a[0],(float)a[1],(float)a[2]));}}}brush.Freeze();var material=new DiffuseMaterial(brush);material.Freeze();return material;}
 }
 public sealed class ModelPose {public double Yaw {get;set;}public double Pitch {get;set;}public double Zoom {get;set;}public double Roll {get;set;}public ModelPose(){Yaw=-25;Pitch=15;Zoom=1;}}
 public sealed class HardwarePreview:StackPanel {
  readonly Viewport3D viewport;readonly TextBlock note;readonly ModelVisual3D model=new ModelVisual3D();string key;ModelPose pose=new ModelPose();Point last;int generation;
  public HardwarePreview(){
   note=new TextBlock{Text="Visual reference model, not automatic identification of your installed hardware.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,6)};Children.Add(note);
   viewport=new Viewport3D{Height=225,ClipToBounds=true,Camera=new PerspectiveCamera(new Point3D(0,0,4),new Vector3D(0,0,-4),new Vector3D(0,1,0),40)};
   var lighting=new Model3DGroup();lighting.Children.Add(new AmbientLight(Color.FromRgb(110,110,110)));lighting.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-1,-1,-2)));viewport.Children.Add(new ModelVisual3D{Content=lighting});viewport.Children.Add(model);viewport.SizeChanged+=delegate{Apply();};
   var surface=new Border{Background=new SolidColorBrush(Color.FromRgb(26,22,19)),Child=viewport};Children.Add(surface);
   surface.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){last=e.GetPosition(surface);surface.CaptureMouse();};surface.MouseLeftButtonUp+=delegate{surface.ReleaseMouseCapture();};surface.MouseMove+=delegate(object s,MouseEventArgs e){if(!surface.IsMouseCaptured)return;var p=e.GetPosition(surface);pose.Yaw+=(p.X-last.X)*0.5;pose.Pitch+=(p.Y-last.Y)*0.5;last=p;Apply();};surface.MouseWheel+=delegate(object s,MouseWheelEventArgs e){pose.Zoom=Math.Max(0.35,Math.Min(3,pose.Zoom*(e.Delta>0?1.1:0.9)));Apply();e.Handled=true;};
   var buttons=new WrapPanel();var reset=new Button{Content="Reset view"};reset.Click+=delegate{pose=DefaultPose();Apply();};buttons.Children.Add(reset);var save=new Button{Content="Save view"};save.Click+=delegate{try{if(key!=null){Directory.CreateDirectory(Path.GetDirectoryName(PosePath()));Journal.Atomic(PosePath(),System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(pose)));}}catch(Exception e){note.Text=e.Message;}};buttons.Children.Add(save);buttons.Children.Add(new TextBlock{Text="Drag to rotate · scroll to zoom",VerticalAlignment=VerticalAlignment.Center});Children.Add(buttons);
  }
  public bool Ready {get{return model.Content!=null;}} public string PreviewStatus {get{return note.Text;}}
  string PosePath(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FramePilot","model-"+key+".json");}
  public async void ShowModel(string name){if(name==key)return;key=name;int request=++generation;model.Content=null;pose=DefaultPose();try{if(File.Exists(PosePath()))pose=new JavaScriptSerializer().Deserialize<ModelPose>(File.ReadAllText(PosePath()));if(pose==null||!Finite(pose.Yaw)||!Finite(pose.Pitch)||!Finite(pose.Zoom)||!Finite(pose.Roll))pose=DefaultPose();pose.Zoom=Math.Max(0.35,Math.Min(3,pose.Zoom));}catch{pose=DefaultPose();}note.Text="Loading visual reference…";
   try{string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"models",name+".glb");var content=await Task.Run(()=>GlbModel.Load(file));if(request!=generation)return;model.Content=content;Apply();note.Text=(name=="gpu"?"RTX 3080 by _surovic_":"MSI B450 Tomahawk MAX by shig0")+" · CC BY 4.0 · Visual reference, not your detected hardware. WPF uses simplified material lighting.";}catch(Exception e){if(request==generation)note.Text="Model preview unavailable: "+e.Message;}
  }
  static bool Finite(double n){return !double.IsNaN(n)&&!double.IsInfinity(n);}
  ModelPose DefaultPose(){return new ModelPose{Roll=key=="gpu"?90:0};}
  void Apply(){
   var rotation=new Transform3DGroup();rotation.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1,0,0),pose.Pitch)));rotation.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0,1,0),pose.Yaw)));rotation.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0,0,1),pose.Roll)));
   var transform=new Transform3DGroup();transform.Children.Add(rotation);transform.Children.Add(new ScaleTransform3D(pose.Zoom,pose.Zoom,pose.Zoom));model.Transform=transform;
   if(model.Content!=null&&viewport.ActualWidth>0&&viewport.ActualHeight>0){var bounds=rotation.TransformBounds(model.Content.Bounds);double tangent=Math.Tan(20*Math.PI/180);double distance=1.2*Math.Max(bounds.SizeX/(2*tangent),bounds.SizeY*viewport.ActualWidth/viewport.ActualHeight/(2*tangent))+bounds.SizeZ/2;distance=Math.Max(distance,1);var camera=(PerspectiveCamera)viewport.Camera;camera.Position=new Point3D(0,0,distance);camera.LookDirection=new Vector3D(0,0,-distance);}
  }
 }
}
