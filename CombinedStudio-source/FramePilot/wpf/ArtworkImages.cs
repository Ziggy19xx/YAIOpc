using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FramePilot {
 static class ArtworkImages {
  sealed class Entry {public BitmapImage Image;public DateTime RetryAfter;}
  static readonly ConcurrentDictionary<string,Entry> images=new ConcurrentDictionary<string,Entry>();
  static readonly ConcurrentDictionary<string,Lazy<Task<BitmapImage>>> pending=new ConcurrentDictionary<string,Lazy<Task<BitmapImage>>>();
  static string Key(FoundGame game){return game.Source+":"+game.AppId+":"+game.Name;}
  public static BitmapImage Peek(FoundGame game){Entry entry;return images.TryGetValue(Key(game),out entry)?entry.Image:null;}
  public static async Task<BitmapImage> GetAsync(FoundGame game,CancellationToken token){
   string key=Key(game);Entry entry;if(images.TryGetValue(key,out entry)&&(entry.Image!=null||entry.RetryAfter>DateTime.UtcNow))return entry.Image;
   var lazy=pending.GetOrAdd(key,k=>new Lazy<Task<BitmapImage>>(()=>Load(game,key,token)));
   try{return await lazy.Value.ConfigureAwait(false);}finally{((ICollection<KeyValuePair<string,Lazy<Task<BitmapImage>>>>)pending).Remove(new KeyValuePair<string,Lazy<Task<BitmapImage>>>(key,lazy));}
  }
  static async Task<BitmapImage> Load(FoundGame game,string key,CancellationToken token){
   BitmapImage bitmap=null;
   try{string path=await SteamArtwork.GetLibraryImageAsync(game,token).ConfigureAwait(false);token.ThrowIfCancellationRequested();if(path!=null)bitmap=await Task.Run(delegate{var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=640;image.UriSource=new Uri(path,UriKind.Absolute);image.EndInit();image.Freeze();return image;},token).ConfigureAwait(false);}catch(OperationCanceledException){throw;}catch{}
   // Bound decoded memory; unavailable images retry later instead of issuing
   // repeated requests on each status update or filter change.
   if(images.Count>=256)images.Clear();images[key]=new Entry{Image=bitmap,RetryAfter=DateTime.UtcNow.AddMinutes(2)};return bitmap;
  }
 }
}
