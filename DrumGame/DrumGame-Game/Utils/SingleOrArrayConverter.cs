using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DrumGame.Game.Utils;

public class SingleOrArrayConverter<T> : JsonConverter<T[]>
{
    public override T[] ReadJson(JsonReader reader, Type objectType, T[] existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (hasExistingValue)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                if (existingValue.Length > 0)
                {
                    serializer.Populate(reader, existingValue[0]);
                    return existingValue;
                }
                else
                    return [serializer.Deserialize<T>(reader)];
            }
            else
            {
                reader.Read(); // open array
                var append = new List<T>();
                var i = 0;
                while (reader.TokenType == JsonToken.StartObject)
                {
                    if (i < existingValue.Length)
                        serializer.Populate(reader, existingValue[i]);
                    else
                        append.Add(serializer.Deserialize<T>(reader));
                    reader.Read(); // close object
                    i += 1;
                }
                if (append.Count > 0)
                    return [.. existingValue, .. append];
                return existingValue;
            }
        }
        if (reader.TokenType == JsonToken.StartObject)
            return [serializer.Deserialize<T>(reader)];
        else
            return serializer.Deserialize<T[]>(reader);
    }
    public override void WriteJson(JsonWriter writer, T[] value, JsonSerializer serializer)
    {
        if (value.Length == 1)
            serializer.Serialize(writer, value[0]);
        else
            serializer.Serialize(writer, value);
    }
}
