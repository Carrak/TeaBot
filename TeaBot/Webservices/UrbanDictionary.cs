using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeaBot.Main;
using TeaBot.Utilities;

namespace TeaBot.Webservices
{
    public static class UrbanDictionary
    {
        public static IEnumerable<UrbanDictionaryDefinition> DeserealiseDefinitions(string json)
        {
            JArray jobj = (JArray)JObject.Parse(json)["list"];

            if (!jobj.HasValues)
                return null;

            IEnumerable<UrbanDictionaryDefinition> definitions = JsonConvert.DeserializeObject<IEnumerable<UrbanDictionaryDefinition>>(jobj.ToString());
            definitions = definitions.Where(x => x.Definition.Length < 2048).Where(x => x.Example.Length < 2048).TakeWhile((x, index) => index < 10);

            foreach (var definition in definitions)
            {
                definition.Definition = ParseAndPlaceReferenceHyperlinks(definition.Definition);
                definition.Example = ParseAndPlaceReferenceHyperlinks(definition.Example);
            }

            return definitions;

            static string ParseAndPlaceReferenceHyperlinks(string toFormat)
            {
                var mc = Regex.Matches(toFormat, @"\[[\s\S]*?\]");
                foreach (var match in mc)
                {
                    string reference = match.ToString();
                    string refWord = reference[1..^1];
                    toFormat = toFormat.Replace(reference, $"{reference}(https://www.urbandictionary.com/define.php?term={WebUtilities.FormatStringForURL(refWord)})");
                }
                return toFormat;
            }
        }

        public static async Task<string> GetDefinitionJSONAsync(string word)
        {
            word = WebUtilities.FormatStringForURL(word);
            var response = await TeaEssentials.HttpClient.GetAsync($"http://api.urbandictionary.com/v0/define?term={word}");
            if (!response.IsSuccessStatusCode)
                return null;

            string content = await response.Content.ReadAsStringAsync();

            return content;
        }
    }

    public sealed class UrbanDictionaryDefinition
    {
        [JsonProperty("word")]
        public string Word;

        [JsonProperty("definition")]
        public string Definition;

        [JsonProperty("example")]
        public string Example;

        [JsonProperty("author")]
        public string Author;

        [JsonProperty("thumbs_up")]
        public int ThumbsUp;

        [JsonProperty("thumbs_down")]
        public int ThumbsDown;

    }
}
