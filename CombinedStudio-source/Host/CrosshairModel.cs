using System.Text.Json;
using System.Text.RegularExpressions;
namespace CombinedStudio;
internal sealed class CrosshairDesign {
 public string Shape {get;set;}="Cross";
 public string Color {get;set;}="#55FF88";
 public string OutlineColor {get;set;}="#000000";
 public int Length {get;set;}=10;
 public int Thickness {get;set;}=2;
 public int Gap {get;set;}=5;
 public int? CommunityThickness {get;set;}
 public int? CommunityGap {get;set;}
 public int DotSize {get;set;}=2;
 public int Radius {get;set;}=12;
 public int Outline {get;set;}=1;
 public int Opacity {get;set;}=100;
 public int Hue {get;set;}
 public string Finish {get;set;}="Solid";
 public int TextureStrength {get;set;}=65;
 public int Rotation {get;set;}
 public bool Dot {get;set;}=true;
 public string ImageFile {get;set;}="";
 public int ImageSize {get;set;}=48;
 public long CommunityId {get;set;}
 internal static readonly string[] Shapes={"Cross","T shape","Dot","Ring","Ring + cross","Chevron","Diamond","Triangle","Square","Brackets","Double ring","Burst","Star","Image","Community"};
 internal CrosshairCommunityDrawing PreparedDrawing;
 internal System.Windows.Media.Imaging.BitmapSource PreparedImage;
 internal CrosshairDesign Copy(){var copy=JsonSerializer.Deserialize<CrosshairDesign>(JsonSerializer.Serialize(this))!;copy.PreparedDrawing=PreparedDrawing;copy.PreparedImage=PreparedImage;return copy;}
 internal void Validate(){
  if(Hue<0||Hue>359||TextureStrength<0||TextureStrength>100||!CrosshairAppearance.Finishes.Contains(Finish))throw new ArgumentException("Invalid crosshair appearance setting.");
  if(!Shapes.Contains(Shape))throw new ArgumentException("Unknown crosshair shape.");
  if(!Regex.IsMatch(Color??"",@"^#[0-9a-fA-F]{6}$")||!Regex.IsMatch(OutlineColor??"",@"^#[0-9a-fA-F]{6}$"))throw new ArgumentException("Use a color in #RRGGBB format.");
  if(Length<1||Length>60||Thickness<1||Thickness>12||Gap<0||Gap>40||DotSize<1||DotSize>20||Radius<2||Radius>80||Outline<0||Outline>6||Opacity<5||Opacity>100||Rotation<0||Rotation>359||ImageSize<8||ImageSize>200)throw new ArgumentException("The crosshair has a setting outside its supported range.");
  if(CommunityThickness is <1 or >12||CommunityGap is <0 or >40)throw new ArgumentException("Line adjustments are outside their supported range.");
  if(Shape=="Image"&&string.IsNullOrEmpty(ImageFile))throw new ArgumentException("Import a PNG before choosing Image.");
  if(Shape=="Community"&&(CommunityId<=0||ImageFile!="community-"+CommunityId+".json"))throw new ArgumentException("Choose a community design from the game library.");
  if(!string.IsNullOrEmpty(ImageFile)&&(System.IO.Path.GetFileName(ImageFile)!=ImageFile||ImageFile.Length>100))throw new ArgumentException("Invalid image reference.");
 }
}
internal sealed class CrosshairPreset {public string Name {get;set;}="Custom";public CrosshairDesign Design {get;set;}=new();}
internal sealed class CrosshairSettings {
 public CrosshairDesign Design {get;set;}=new();
 public List<CrosshairPreset> Presets {get;set;}=new();
 public string Monitor {get;set;}="";
 public int OffsetX {get;set;}
 public int OffsetY {get;set;}
 public bool HideOnAim {get;set;}
 public int SizePercent {get;set;}=100;
 public int WidthPercent {get;set;}=100;public int HeightPercent {get;set;}=100;
 public string ToggleKey {get;set;}="X";
 public string SpinKey {get;set;}="R";
 public uint SpinModifiers {get;set;}=3;
 public bool SpinShortcutEnabled {get;set;}=true;
 public CrosshairRandomOptions Randomizer {get;set;}=new();
}
internal sealed class CrosshairStore {
 internal readonly string Folder;internal CrosshairSettings Settings {get;private set;}=new();internal string LoadError {get;private set;}="";
 internal string Path=>System.IO.Path.Combine(Folder,"settings.json");
 internal CrosshairStore(string folder){Folder=folder;if(!File.Exists(Path))return;try{var settings=JsonSerializer.Deserialize<CrosshairSettings>(File.ReadAllText(Path))??throw new ArgumentException("Empty settings.");settings.Design.Validate();if(settings.SizePercent<1||settings.SizePercent>500||settings.WidthPercent<10||settings.WidthPercent>300||settings.HeightPercent<10||settings.HeightPercent>300)throw new ArgumentException("Invalid crosshair size.");if(!CrosshairHotkey.ValidKey(settings.SpinKey)||settings.SpinModifiers>15)throw new ArgumentException("Invalid randomizer shortcut.");settings.Randomizer??=new();settings.Randomizer.Validate();settings.Presets??=new();foreach(var preset in settings.Presets)preset.Design.Validate();if(settings.Presets.Count>128||Math.Abs((long)settings.OffsetX)>2000||Math.Abs((long)settings.OffsetY)>2000||!new[]{"X","C","F8","F9"}.Contains(settings.ToggleKey))throw new ArgumentException("Invalid crosshair settings.");Settings=settings;}catch(Exception error){LoadError="Saved crosshair settings could not be loaded: "+error.Message;}}
 internal void Save(){Settings.Design.Validate();Directory.CreateDirectory(Folder);string temp=Path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(Settings,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,Path,true);}
 internal void SavePreset(string name){name=(name??"").Trim();if(name.Length==0||name.Length>60)throw new ArgumentException("Give the preset a name of 1–60 characters.");if(Settings.Presets.Any(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("That preset name already exists. Delete it first or use a new name.");if(Settings.Presets.Count>=128)throw new ArgumentException("The saved preset limit is 128.");Settings.Presets.Add(new(){Name=name,Design=Settings.Design.Copy()});Save();}
 internal string ImportImage(string file){if(new FileInfo(file).Length>10*1024*1024)throw new ArgumentException("Choose a PNG smaller than 10 MB.");var image=CrosshairView.LoadImage(file);if(image.PixelWidth>2048||image.PixelHeight>2048)throw new ArgumentException("Choose an image no larger than 2048 × 2048.");Directory.CreateDirectory(Folder);string target=System.IO.Path.Combine(Folder,"image-"+Guid.NewGuid().ToString("N")+".png");var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var stream=File.Create(target))encoder.Save(stream);return System.IO.Path.GetFileName(target);}
 internal CrosshairDesign ImportDesign(string file){if(new FileInfo(file).Length>65536)throw new ArgumentException("Crosshair preset files must be smaller than 64 KB.");var design=JsonSerializer.Deserialize<CrosshairDesign>(File.ReadAllText(file))??throw new ArgumentException("Empty preset.");if(design.Shape=="Image"||design.Shape=="Community"||!string.IsNullOrEmpty(design.ImageFile))throw new ArgumentException("Import image designs as PNG files.");design.Validate();return design;}
 internal static IEnumerable<CrosshairPreset> BuiltIn(){
  yield return new(){Name="Classic",Design=new()};
  yield return new(){Name="Precision dot",Design=new(){Shape="Dot",DotSize=3,Color="#FF5555"}};
  yield return new(){Name="Clean T",Design=new(){Shape="T shape",Length=8,Gap=4,Dot=false,Color="#55DDFF"}};
  yield return new(){Name="Open ring",Design=new(){Shape="Ring",Radius=10,Thickness=2,Dot=false,Color="#FFFFFF"}};
  yield return new(){Name="Ring + dot",Design=new(){Shape="Ring",Radius=13,DotSize=2,Color="#FFD855"}};
  yield return new(){Name="Diagonal",Design=new(){Rotation=45,Gap=5,Dot=false,Length=9,Color="#DD88FF"}};
  yield return new(){Name="Chevron",Design=new(){Shape="Chevron",Dot=false,Length=12,Color="#FF9944"}};
  yield return new(){Name="Target",Design=new(){Shape="Ring + cross",Radius=16,Length=12,Gap=6,DotSize=3}};
  string[] shapes={"Diamond","Triangle","Square","Brackets","Double ring","Burst","Star","Cross","T shape","Chevron","Ring","Dot"};
  string[] names={"Diamond sight","Triangle sight","Box sight","Corner brackets","Double halo","Sunburst","Star sight","Micro cross","Thin T","Arrowhead","Fine ring","Tiny dot"};
  string[] colors={"#55DDFF","#FFD855","#FFFFFF","#55FF88","#DD88FF","#FF5555"};
  for(int i=0;i<shapes.Length;i++){
   yield return new(){Name=names[i],Design=new(){Shape=shapes[i],Color=colors[i%colors.Length],Radius=10+i%4,Length=6+i%5,Gap=3,Dot=i%2==0,DotSize=2,Thickness=1,Outline=1}};
   yield return new(){Name=names[i]+" bold",Design=new(){Shape=shapes[i],Color=colors[(i+3)%colors.Length],Radius=16+i%3,Length=12,Gap=7,Dot=i%3==0,DotSize=4,Thickness=3,Outline=2,Rotation=i%2==0?0:45}};
  }
 }
}
