using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
namespace FramePilot {
 static class ArtworkTests {
  public static async Task<string[]> Run(){
   var results=new List<string>();string root=Path.Combine(Path.GetTempPath(),"FramePilot-art-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);SteamArtwork.TestCacheRoot=root;
   try{
    byte[] bytes;using(var bitmap=new Bitmap(64,64))using(var stream=new MemoryStream()){bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Jpeg);bytes=stream.ToArray();}
    int calls=0;string requested=null;var release=new TaskCompletionSource<bool>();
    SteamArtwork.ResponseFactory=async delegate(string url,CancellationToken token){Interlocked.Increment(ref calls);requested=url;await release.Task;return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};};
    var game=new FoundGame{Name="Artwork fixture "+Guid.NewGuid().ToString("N"),Source="Steam",AppId="999999999991"};
    var first=SteamArtwork.GetLibraryImageAsync(game,CancellationToken.None);var second=SteamArtwork.GetLibraryImageAsync(game,CancellationToken.None);var third=SteamArtwork.GetLibraryImageAsync(game,CancellationToken.None);
    for(int i=0;calls==0&&i<100;i++)await Task.Delay(10);release.SetResult(true);var paths=await Task.WhenAll(first,second,third);if(calls!=1||paths[0]==null||paths[0]!=paths[1]||paths[1]!=paths[2])throw new Exception("Concurrent image requests were not shared: calls="+calls+" paths="+string.Join(",",paths));results.Add("PASS: three concurrent library requests perform one download");
    if(requested==null||!requested.EndsWith("/header.jpg",StringComparison.Ordinal))throw new Exception("Small header was not tried first");results.Add("PASS: small Steam header precedes large hero and optional API lookup");
    await SteamArtwork.GetLibraryImageAsync(game,CancellationToken.None);if(calls!=1)throw new Exception("Disk cache missed");results.Add("PASS: persistent image cache avoids repeat network request");
    var images=await Task.WhenAll(ArtworkImages.GetAsync(game,CancellationToken.None),ArtworkImages.GetAsync(game,CancellationToken.None));if(images[0]==null||!images[0].IsFrozen||!object.ReferenceEquals(images[0],images[1])||calls!=1)throw new Exception("Decoded image cache");if(!object.ReferenceEquals(images[0],ArtworkImages.Peek(game)))throw new Exception("Immediate memory cache");results.Add("PASS: decoded frozen image is shared and immediately reusable");
    calls=0;SteamArtwork.ResponseFactory=(url,token)=>{Interlocked.Increment(ref calls);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));};var missing=new FoundGame{Name="Missing fixture "+Guid.NewGuid().ToString("N"),Source="Steam",AppId="999999999992"};await ArtworkImages.GetAsync(missing,CancellationToken.None);int initialCalls=calls;if(initialCalls==0)throw new Exception("Failure path was not exercised");await ArtworkImages.GetAsync(missing,CancellationToken.None);if(calls!=initialCalls)throw new Exception("Missing art repeatedly requested");results.Add("PASS: unavailable artwork has a retry cooldown");
    return results.ToArray();
   }finally{SteamArtwork.ResponseFactory=null;SteamArtwork.TestCacheRoot=null;try{Directory.Delete(root,true);}catch{}}
  }
 }
}
