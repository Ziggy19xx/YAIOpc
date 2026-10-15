using System.Net.Http;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
namespace CombinedStudio;
internal sealed class CommunityCrosshair {
 public long Id {get;set;} public string Name {get;set;}="";public string Creator {get;set;}="";public string Slug {get;set;}="";
 public string[] Games {get;set;}=Array.Empty<string>();public string[] Tags {get;set;}=Array.Empty<string>();public long FileId {get;set;} public long Bytes {get;set;}public double Rating {get;set;}
 internal CrosshairPreset Preset=>new(){Name=Name,Design=new(){Shape="Community",CommunityId=Id,ImageFile="community-"+Id+".json"}};
 internal string Url=>"https://mod.io/g/crosshair-x/m/"+Uri.EscapeDataString(Slug);
}
internal static class CrosshairCommunity {
 internal static readonly string[] Games={"Valorant","Fortnite","Counter-Strike"};
 static readonly Lazy<CommunityCrosshair[]> catalog=new(()=>{using var stream=typeof(CrosshairCommunity).Assembly.GetManifestResourceStream("crosshair-community.json")??throw new IOException("Community catalog is missing.");var entries=JsonSerializer.Deserialize<CommunityCrosshair[]>(stream)??Array.Empty<CommunityCrosshair>();foreach(var entry in entries){entry.Name=System.Net.WebUtility.HtmlDecode(entry.Name);entry.Creator=System.Net.WebUtility.HtmlDecode(entry.Creator);}return entries;});
 internal static CommunityCrosshair[] Catalog=>catalog.Value;
 static readonly HttpClient client=new(){Timeout=TimeSpan.FromSeconds(60)};
 static readonly SemaphoreSlim downloads=new(4);
 internal static IEnumerable<CrosshairPreset> Pool(string category)=>Catalog.Where(x=>category=="All community games"||category=="All designs"||x.Games.Contains(category)).Select(x=>x.Preset);
 internal static async Task Ensure(CrosshairDesign design,string folder,CancellationToken token=default){
  if(design.Shape!="Community")return;design.Validate();string path=Path.Combine(folder,design.ImageFile);if(File.Exists(path))return;
  var entry=Catalog.FirstOrDefault(x=>x.Id==design.CommunityId)??throw new ArgumentException("This crosshair is absent from the catalog.");
  if(entry.FileId<=0)throw new IOException("The creator has not provided a downloadable package.");
  await downloads.WaitAsync(token);try{
   if(File.Exists(path))return;
   using var response=await client.GetAsync($"https://g-11309.modapi.io/v1/games/11309/mods/{entry.Id}/files/{entry.FileId}/download",HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
   if(response.Content.Headers.ContentLength>20*1024*1024)throw new IOException("Crosshair package exceeds the 20 MB import limit.");
   using var stream=await response.Content.ReadAsStreamAsync(token);using var memory=new MemoryStream();var buffer=new byte[81920];int count;
   while((count=await stream.ReadAsync(buffer,token))>0){if(memory.Length+count>20*1024*1024)throw new IOException("Crosshair package exceeds the 20 MB import limit.");await memory.WriteAsync(buffer.AsMemory(0,count),token);}memory.Position=0;
   using var zip=new ZipArchive(memory,ZipArchiveMode.Read);var files=zip.Entries.Where(x=>x.FullName.StartsWith("crosshairs/",StringComparison.Ordinal)&&x.FullName.EndsWith(".json",StringComparison.OrdinalIgnoreCase)&&!x.FullName.EndsWith("manifest.json",StringComparison.OrdinalIgnoreCase)).ToArray();
   if(files.Length!=1)throw new IOException("This package contains multiple designs; choose a single-design package on the source page.");
   if(files[0].Length>24*1024*1024)throw new IOException("Crosshair data exceeds the import limit.");
   using var extracted=files[0].Open();using var jsonBytes=new MemoryStream();while((count=await extracted.ReadAsync(buffer,token))>0){if(jsonBytes.Length+count>24*1024*1024)throw new IOException("Crosshair data exceeds the import limit.");await jsonBytes.WriteAsync(buffer.AsMemory(0,count),token);}string json=System.Text.Encoding.UTF8.GetString(jsonBytes.ToArray());using var document=JsonDocument.Parse(json);CrosshairCommunityDrawing.Validate(document.RootElement);
   Directory.CreateDirectory(folder);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{await File.WriteAllTextAsync(temp,json,token);File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
  }finally{downloads.Release();}
 }
}
