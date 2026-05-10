using System.Text.Json;
using System.Text.Json.Serialization;

namespace BG3PakViewer.Miscellaneous;

public class InterfaceJsonConverter<TInterface, TImpl> : JsonConverter<TInterface>
    where TImpl : TInterface
{
    public override TInterface? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<TImpl>(ref reader, options);
    }

    public override void Write(
        Utf8JsonWriter writer,
        TInterface value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value!.GetType(), options);
    }
}