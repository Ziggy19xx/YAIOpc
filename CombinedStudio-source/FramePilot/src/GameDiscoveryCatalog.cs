using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace FramePilot {
 public sealed class DiscoveryGame {
  public string Name, Summary, CoverUrl, ReleaseDate, Genres, Platforms, DealUrl, StoreName, RatingSource;
  public decimal? SalePrice, NormalPrice;
  public double? Rating, Savings;
 }

 public static class GameDiscoveryCatalog {
  static readonly HttpClient http=new HttpClient{Timeout=TimeSpan.FromSeconds(12)};
  static string entropy="YAIOpc-IGDB-Credentials-v1";
  static GameDiscoveryCatalog(){http.DefaultRequestHeaders.UserAgent.ParseAdd("YAIOpc/4.0");}

  public static async Task<List<DiscoveryGame>> GetDealsAsync(string search,CancellationToken token){
   string url="https://www.cheapshark.com/api/1.0/deals?pageSize=60&sortBy=DealRating&upperPrice=60&onSale=1";
   if(!string.IsNullOrWhiteSpace(search))url+="&title="+Uri.EscapeDataString(search.Trim());
   using(var response=await http.GetAsync(url,token).ConfigureAwait(false)){
    response.EnsureSuccessStatusCode();using(var stream=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))using(var doc=await JsonDocument.ParseAsync(stream,cancellationToken:token).ConfigureAwait(false)){
     var games=new List<DiscoveryGame>();foreach(var item in doc.RootElement.EnumerateArray()){
      string title=Text(item,"title"),dealId=Text(item,"dealID"),sale=Text(item,"salePrice"),normal=Text(item,"normalPrice");decimal salePrice,normalPrice;
      if(string.IsNullOrWhiteSpace(title)||!decimal.TryParse(sale,System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out salePrice))continue;
      decimal? normalValue=decimal.TryParse(normal,System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out normalPrice)?(decimal?)normalPrice:null;
      double savings;double? savingValue=double.TryParse(Text(item,"savings"),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out savings)?(double?)savings:null;
      games.Add(new DiscoveryGame{Name=title,CoverUrl=Text(item,"thumb"),DealUrl=string.IsNullOrWhiteSpace(dealId)?null:"https://www.cheapshark.com/redirect?dealID="+Uri.EscapeDataString(dealId),StoreName=StoreName(Text(item,"storeID")),SalePrice=salePrice,NormalPrice=normalValue,Savings=savingValue,Rating=Number(item,"metacriticScore"),RatingSource="Metacritic"});
     }
     return games;
    }
   }
  }
  public static async Task<List<DiscoveryGame>> GetCatalogAsync(string clientId,string accessToken,string search,CancellationToken token){
   if(string.IsNullOrWhiteSpace(clientId)||string.IsNullOrWhiteSpace(accessToken))return new List<DiscoveryGame>();
   string query="fields name,summary,first_release_date,rating,cover.url,genres.name,platforms.name; where version_parent = null & game_type = 0 & platforms = (6)";
   if(!string.IsNullOrWhiteSpace(search))query+=" & name ~ \"*"+search.Trim().Replace("\\","\\\\").Replace("\"","\\\"")+"*\"";
   query+="; sort rating_count desc; limit 40;";
   using(var request=new HttpRequestMessage(HttpMethod.Post,"https://api.igdb.com/v4/games")){request.Headers.Add("Client-ID",clientId);request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",accessToken);request.Content=new StringContent(query,System.Text.Encoding.UTF8,"text/plain");using(var response=await http.SendAsync(request,token).ConfigureAwait(false)){
    response.EnsureSuccessStatusCode();using(var stream=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))using(var doc=await JsonDocument.ParseAsync(stream,cancellationToken:token).ConfigureAwait(false)){
     var result=new List<DiscoveryGame>();foreach(var item in doc.RootElement.EnumerateArray()){
      var game=new DiscoveryGame{Name=Text(item,"name"),Summary=Text(item,"summary"),Rating=Number(item,"rating"),RatingSource="IGDB"};double release;if(double.TryParse(Text(item,"first_release_date"),out release))game.ReleaseDate=DateTimeOffset.FromUnixTimeSeconds((long)release).ToString("yyyy");
      if(item.TryGetProperty("cover",out var cover)&&cover.TryGetProperty("url",out var image))game.CoverUrl="https:"+image.GetString().Replace("t_thumb","t_cover_big");
      game.Genres=NestedNames(item,"genres");game.Platforms=NestedNames(item,"platforms");if(!string.IsNullOrWhiteSpace(game.Name))result.Add(game);
     }return result;
    }
   }}
  }
  public static void SaveIgdbCredentials(string clientId,string clientSecret){using(var key=Registry.CurrentUser.CreateSubKey(@"Software\YAIOpc")){if(string.IsNullOrWhiteSpace(clientId)){key.DeleteValue("IGDBCredentials",false);return;}if(string.IsNullOrWhiteSpace(clientSecret)){var existing=GetIgdbCredentials();if(existing==null)throw new InvalidOperationException("Enter the Twitch Client Secret to connect IGDB.");clientSecret=existing.Item2;}byte[] plain=System.Text.Encoding.UTF8.GetBytes(clientId.Trim()+"\n"+clientSecret.Trim());key.SetValue("IGDBCredentials",Convert.ToBase64String(ProtectedData.Protect(plain,System.Text.Encoding.UTF8.GetBytes(entropy),DataProtectionScope.CurrentUser)),RegistryValueKind.String);}}
  public static Tuple<string,string> GetIgdbCredentials(){string envId=Environment.GetEnvironmentVariable("IGDB_CLIENT_ID"),envSecret=Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET");if(!string.IsNullOrWhiteSpace(envId)&&!string.IsNullOrWhiteSpace(envSecret))return Tuple.Create(envId.Trim(),envSecret.Trim());try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\YAIOpc")){string saved=key==null?null:Convert.ToString(key.GetValue("IGDBCredentials"));if(!string.IsNullOrWhiteSpace(saved)){string[] values=System.Text.Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(saved),System.Text.Encoding.UTF8.GetBytes(entropy),DataProtectionScope.CurrentUser)).Split(new[]{'\n'},2);if(values.Length==2)return Tuple.Create(values[0],values[1]);}}}catch{}return null;}
  public static async Task<string> GetIgdbTokenAsync(string clientId,string clientSecret,CancellationToken token){using(var response=await http.PostAsync("https://id.twitch.tv/oauth2/token",new FormUrlEncodedContent(new[]{new KeyValuePair<string,string>("client_id",clientId),new KeyValuePair<string,string>("client_secret",clientSecret),new KeyValuePair<string,string>("grant_type","client_credentials")}),token).ConfigureAwait(false)){response.EnsureSuccessStatusCode();using(var stream=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))using(var doc=await JsonDocument.ParseAsync(stream,cancellationToken:token).ConfigureAwait(false)){return Text(doc.RootElement,"access_token");}}}
  static string Text(JsonElement item,string name){JsonElement value;if(item.TryGetProperty(name,out value)){if(value.ValueKind==JsonValueKind.String)return value.GetString();if(value.ValueKind==JsonValueKind.Number)return value.GetRawText();}return null;}
  static double? Number(JsonElement item,string name){double value;return double.TryParse(Text(item,name),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out value)&&value>0?(double?)value:null;}
  static string NestedNames(JsonElement item,string name){JsonElement values;if(!item.TryGetProperty(name,out values)||values.ValueKind!=JsonValueKind.Array)return null;return string.Join(", ",values.EnumerateArray().Select(x=>Text(x,"name")).Where(x=>!string.IsNullOrWhiteSpace(x)).Take(4));}
  static string StoreName(string id){switch(id){case "1":return "Steam";case "7":return "GOG";case "11":return "Humble Store";case "25":return "Epic Games";default:return "PC Store";}}
 }
}
