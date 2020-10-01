using System;
using Discord;
using Newtonsoft.Json;

namespace TeaBot.ReactionCallbackCommands.ReactionRole
{
    /// <summary>
    ///     Extra data for <see cref="FullReactionRoleMessage"/>.
    /// </summary>
    public class ReactionRoleMessageData
    {
        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("description")]
        public string Description { get; }

        [JsonProperty("color")]
        [JsonConverter(typeof(JsonColorConverter))]
        public Color? Color { get; }

        public ReactionRoleMessageData(string name, string description, Color? color)
        {
            Name = name;
            Description = description;
            Color = color;
        }
    }

    /// <summary>
    ///     Extra data for <see cref="FullEmoteRolePair"/>.
    /// </summary>
    public class EmoteRolePairData
    {
        [JsonProperty("description")]
        public string Description { get; }

        public EmoteRolePairData(string description) => Description = description;
    }

    class JsonColorConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();

        public override bool CanConvert(Type objectType) => objectType == typeof(uint) || objectType == typeof(long) || objectType == typeof(object);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => reader.Value is null ? (Color?)null : new Color(Convert.ToUInt32(reader.Value));
    }
}
