using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace System.Web.Script.Serialization {
 public sealed class JavaScriptSerializer {
  public int MaxJsonLength {get;set;}=2097152;
  public int RecursionLimit {get;set;}=100;
  JsonSerializerOptions Options(){var options=new JsonSerializerOptions{IncludeFields=true,NumberHandling=JsonNumberHandling.AllowReadingFromString,PropertyNameCaseInsensitive=true,MaxDepth=RecursionLimit};options.Converters.Add(new ObjectValues());return options;}
  public T Deserialize<T>(string input){if(input.Length>MaxJsonLength)throw new ArgumentException("JSON exceeds permitted length");return JsonSerializer.Deserialize<T>(input,Options());}
  public T ConvertToType<T>(object value){return Deserialize<T>(Serialize(value));}
  public string Serialize(object value){var result=JsonSerializer.Serialize(value,Options());if(result.Length>MaxJsonLength)throw new ArgumentException("JSON exceeds permitted length");return result;}
  sealed class ObjectValues:JsonConverter<object>{
   public override object Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options){using var document=JsonDocument.ParseValue(ref reader);return Value(document.RootElement);}
   static object Value(JsonElement item){switch(item.ValueKind){case JsonValueKind.Object:var map=new Dictionary<string,object>();foreach(var pair in item.EnumerateObject())map[pair.Name]=Value(pair.Value);return map;case JsonValueKind.Array:var list=new ArrayList();foreach(var child in item.EnumerateArray())list.Add(Value(child));return list;case JsonValueKind.String:return item.GetString();case JsonValueKind.Number:if(item.TryGetInt32(out var small))return small;if(item.TryGetInt64(out var large))return large;return item.GetDouble();case JsonValueKind.True:return true;case JsonValueKind.False:return false;default:return null;}}
   public override void Write(Utf8JsonWriter writer,object value,JsonSerializerOptions options){if(value.GetType()==typeof(object)){writer.WriteStartObject();writer.WriteEndObject();return;}JsonSerializer.Serialize(writer,value,value.GetType(),options);}
  }
 }
}


