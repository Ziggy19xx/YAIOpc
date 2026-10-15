using System.Text.Json;
using W=System.Windows;
using M=System.Windows.Media;
using B=System.Windows.Data;
namespace CombinedStudio;
internal sealed record AppTheme(string Id,string Name,string Description,string Bg,string Sidebar,string Panel,string Raised,string Line,string Ink,string Muted,string Accent) {
 internal M.Color Color(string role)=> (M.Color)M.ColorConverter.ConvertFromString(role switch{
  "Bg"=>Bg,"Sidebar"=>Sidebar,"Panel"=>Panel,"Raised"=>Raised,"Line"=>Line,"Ink"=>Ink,"Muted"=>Muted,"Dim"=>Muted,"Accent"=>Accent,"OnAccent"=>"#101018",_=>Panel});
}
internal static class ThemePalette {
 internal static readonly AppTheme[] Themes={
  new("cyber","Cyber Gold","Dark teal surfaces, cyan details and golden highlights.","#061113","#040E10","#0B1D20","#11292C","#285257","#F0EBDC","#8CB0B0","#FFD347"),
  new("copper","Copper","The original warm studio style.","#0D0B0A","#13100E","#1A1613","#221C18","#2B231D","#F3ECE4","#A39789","#E8964A"),
  new("midnight","Midnight","Deep navy with cool blue accents.","#080E1A","#101827","#151F30","#1B2A40","#30425B","#ECF2FF","#A2B3CE","#7AA9FF"),
  new("ocean","Ocean","Dark teal with bright aqua highlights.","#071416","#0C1C20","#10272B","#17343B","#29505A","#E8FCFD","#A0BEC3","#55DDD3"),
  new("amethyst","Amethyst","Plum surfaces and soft violet accents.","#100B1A","#191226","#231A32","#302343","#493761","#F6ECFF","#BCA8CF","#BD92FF"),
  new("forest","Forest","Evergreen panels with fresh mint details.","#09130E","#102018","#172B20","#203B2D","#365340","#EBF7EE","#A6C1B0","#7AE0A5"),
  new("rose","Rose","Burgundy and rose-pink highlights.","#180D13","#25141D","#311C28","#422637","#603B50","#FFF0F6","#D1AABD","#FF92BA"),
  new("sunset","Sunset","Smoky purple with a peach accent.","#171018","#231924","#302232","#402E43","#5A425D","#FFF1EB","#CCB0C1","#FFAF83"),
  new("contrast","High Contrast","Black surfaces, strong borders, crisp text.","#000000","#090909","#111111","#1D1D1D","#888888","#FFFFFF","#D5D5D5","#FFE16A")
 };
 internal static AppTheme Current {get;private set;}=Themes[0];
 internal static readonly Dictionary<string,string> Roles=new(StringComparer.OrdinalIgnoreCase){
  ["#0D0B0A"]="Bg",["#120E0C"]="Bg",["#13100E"]="Sidebar",["#1A1613"]="Panel",["#221C18"]="Raised",["#1F1A16"]="Hover",["#2A221C"]="Hover",["#2A2420"]="Hover",
  ["#2B231D"]="Line",["#3A2F27"]="Line",["#4A3D33"]="Line",["#5A4A3C"]="Line",["#4D3827"]="Line",
  ["#F3ECE4"]="Ink",["#D9CFC4"]="Ink",["#A39789"]="Muted",["#6F6358"]="Dim",
  ["#E8964A"]="Accent",["#F2A55A"]="AccentHot",["#C77A3A"]="Accent",["#8A5A2E"]="Accent",
  ["#46301F"]="Soft",["#3A2A1D"]="Soft",["#38271B"]="Soft",["#2E2214"]="Soft",["#3A2314"]="Soft",["#4A2E17"]="Soft",["#4A3219"]="Soft",["#5A3E1E"]="Soft",["#2E2012"]="Soft",
  ["#1A1109"]="OnAccent"
 };
 static readonly Dictionary<string,M.SolidColorBrush> brushes=new(StringComparer.OrdinalIgnoreCase);
 static readonly Dictionary<string,ThemeColorValue> values=new(StringComparer.OrdinalIgnoreCase);
 internal static bool Themed(string hex)=>Roles.ContainsKey(hex)||(hex.Length==9&&Roles.ContainsKey("#"+hex.Substring(3)));
 internal static string Key(string hex,string type)=>"ThemeHex"+hex.Substring(1).ToUpperInvariant()+type;
 static M.Color Mix(M.Color a,M.Color b,double ratio)=>M.Color.FromRgb((byte)(a.R*(1-ratio)+b.R*ratio),(byte)(a.G*(1-ratio)+b.G*ratio),(byte)(a.B*(1-ratio)+b.B*ratio));
 static M.Color Resolve(string hex){var original=(M.Color)M.ColorConverter.ConvertFromString(hex);if(Current.Id=="copper")return original;string rgb=hex.Length==9?"#"+hex.Substring(3):hex;if(!Roles.TryGetValue(rgb,out string role))return original;
  var color=role switch{"Soft"=>Mix(Current.Color("Panel"),Current.Color("Accent"),.24),"Hover"=>Current.Color("Raised"),"AccentHot"=>Mix(Current.Color("Accent"),M.Colors.White,.16),_=>Current.Color(role)};color.A=original.A;return color;}
 internal static M.Brush Brush(string hex){if(!Themed(hex)){var brush=(M.SolidColorBrush)new M.BrushConverter().ConvertFromString(hex);brush.Freeze();return brush;}if(!brushes.TryGetValue(hex,out var shared)){var value=new ThemeColorValue{Value=Resolve(hex)};shared=new();B.BindingOperations.SetBinding(shared,M.SolidColorBrush.ColorProperty,new B.Binding(nameof(ThemeColorValue.Value)){Source=value,Mode=B.BindingMode.OneWay});values[hex]=value;brushes[hex]=shared;}return shared;}
 internal static void Apply(AppTheme theme){Current=theme;foreach(var pair in values)pair.Value.Set(Resolve(pair.Key));var resources=W.Application.Current?.Resources;if(resources==null)return;
  foreach(string hex in Roles.Keys.Concat(new[]{"#F00D0B0A","#E00D0B0A","#B80D0B0A","#400D0B0A","#000D0B0A"})){resources[Key(hex,"Color")]=Resolve(hex);resources[Key(hex,"Brush")]=Brush(hex);}
 }
}
internal sealed class ThemeColorValue:System.ComponentModel.INotifyPropertyChanged {
 public M.Color Value {get;set;}
 public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
 internal void Set(M.Color value){Value=value;PropertyChanged?.Invoke(this,new(nameof(Value)));}
}
internal sealed class AppThemeService {
 readonly string path;internal string Error {get;private set;}="";internal event Action Changed;
 internal AppThemeService(string folder){path=Path.Combine(folder,"theme.json");string id="copper";try{if(File.Exists(path))id=JsonSerializer.Deserialize<string>(File.ReadAllText(path));}catch(Exception error){Error="Could not read theme preferences: "+error.Message;}ThemePalette.Apply(ThemePalette.Themes.FirstOrDefault(x=>x.Id==id)??ThemePalette.Themes[0]);}
 internal void UseReferenceStyleOnce(){if(File.Exists(path+".cyber-v1"))return;Select("cyber");File.WriteAllText(path+".cyber-v1","Applied reference styling.");}
 internal void Select(string id){var theme=ThemePalette.Themes.FirstOrDefault(x=>x.Id==id)??throw new ArgumentException("Unknown theme.");Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(id));File.Move(temp,path,true);ThemePalette.Apply(theme);Changed?.Invoke();}
 internal string SavedId=>JsonSerializer.Deserialize<string>(File.ReadAllText(path));
 internal bool VerifyRestart(){string id=SavedId;_ = new AppThemeService(Path.GetDirectoryName(path));return ThemePalette.Current.Id==id;}
}
