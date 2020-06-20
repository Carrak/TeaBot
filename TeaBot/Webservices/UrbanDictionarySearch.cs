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
    class UrbanDictionarySearch
    {
        private readonly string _word;

        public UrbanDictionarySearch(string word)
        {
            _word = word;
        }

        public IEnumerable<UrbanDictionaryDefinition> DeserealiseDefinitions(string json)
        {
            JArray jobj = (JArray)JObject.Parse(json)["list"];

            if (!jobj.HasValues)
                return Enumerable.Empty<UrbanDictionaryDefinition>();

            IEnumerable<UrbanDictionaryDefinition> definitions = JsonConvert.DeserializeObject<IEnumerable<UrbanDictionaryDefinition>>(jobj.ToString());
            definitions = definitions.Where(x => x.Definition.Length < 2048).Where(x => x.Example.Length < 2048).Take(10);

            foreach (var definition in definitions)
            {
                definition.Definition = ParseAndPlaceReferenceHyperlinks(definition.Definition);
                definition.Example = ParseAndPlaceReferenceHyperlinks(definition.Example);
            }

            return definitions;

            static string ParseAndPlaceReferenceHyperlinks(string toFormat)
            {
                var mc = Regex.Matches(toFormat, @"\[.*?\]");
                foreach (var match in mc)
                {
                    string reference = match.ToString();
                    string refWord = reference[1..^1];
                    toFormat = toFormat.Replace(reference, $"{reference}(https://www.urbandictionary.com/define.php?term={WebUtilities.FormatStringForURL(refWord)})");
                }
                return toFormat;
            }
        }

        public async Task<string> GetDefinitionsJSONAsync()
        {
            string word = WebUtilities.FormatStringForURL(_word);
            var response = await TeaEssentials.HttpClient.GetAsync($"http://api.urbandictionary.com/v0/define?term={word}");

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
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
