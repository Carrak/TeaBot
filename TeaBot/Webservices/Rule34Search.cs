using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeaBot.Webservices
{
    /// <summary>
    ///     Static class used for searching on rule34.xxx
    /// </summary>
    public static class Rule34Search
    {
        /// <summary>
        ///     Converts the provided XML to JSON and deserializes it to a Rule34Post instance
        /// </summary>
        /// <param name="json">The JSON to deserealise into an instance of Rule34Post.</param>
        /// <returns>An instance of Rule34Post taken from the JSON or null if there aren't any.</returns>
        public static Rule34Post DeserializePost(string json)
        {
            JObject jobj = JObject.Parse(json);

            if (Convert.ToInt32(jobj["posts"]["@count"]) == 0)
                return null;

            var posts = jobj["posts"]["post"];
            if (posts is JArray)
            {
                JObject post = (JObject)posts[new Random().Next(0, posts.Count())];
                Rule34Post r34post = JsonConvert.DeserializeObject<Rule34Post>(post.ToString());
                return r34post;
            }
            else if (posts is JObject)
            {
                Rule34Post r34post = JsonConvert.DeserializeObject<Rule34Post>(posts.ToString());
                return r34post;
            }
            else
                return null;

        }

        /// <summary>
        ///     Finds a random post on rule34.xxx
        /// </summary>
        /// <param name="tags">The tags to search for</param>
        /// <param name="postsCount">Count of posts that a tag combination yields</param>
        /// <returns>
        ///     XML string with the contents of the response, or null in the following cases:
        ///     1. <paramref name="postsCount"/> is less than 1
        ///     2. The status code is not a success status code
        ///     3. API limited due to abuse
        /// </returns>
        public static async Task<string> GetRandomPostAsync(string tags, int postsCount)
        {
            if (postsCount == -1)
                return null;

            tags = WebUtilities.FormatStringForURL(tags);
            int random = new Random().Next(0, Math.Min(2001, postsCount));
            var response = await TeaEssentials.HttpClient.GetAsync($"https://rule34.xxx/index.php?page=dapi&s=post&q=index&pid={random}&tags={tags}&limit=1");

            if (!response.IsSuccessStatusCode)
                return null;

            string content = await response.Content.ReadAsStringAsync();

            string json = WebUtilities.XMLStringToJsonString(content);

            if (json.Contains("Search error: API limited due to abuse"))
                return null;

            return json;

        }

        /// <summary>
        ///     Sends a request to rule34.xxx to retrieve the count of posts that match the provided <paramref name="tags"/>
        /// </summary>
        /// <param name="tags">The tags to search for</param>
        /// <returns>The count of posts or -1 if the response is not a success</returns>
        public async static Task<int> GetResultCountAsync(string tags)
        {
            tags = WebUtilities.FormatStringForURL(tags);

            var response = await TeaEssentials.HttpClient.GetAsync($"https://rule34.xxx/index.php?page=dapi&s=post&q=index&limit=0&tags={tags}");

            if (!response.IsSuccessStatusCode)
            {
                return -1;
            }

            string content = await response.Content.ReadAsStringAsync();
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(content);

            return Convert.ToInt32(xmlDoc["posts"].Attributes["count"].Value);
        }
    }

    /// <summary>
    ///     Class that represents a post on rule34.xxx
    /// </summary>
    public sealed class Rule34Post
    {
        [JsonProperty("@file_url")]
        public string FileUrl;

        [JsonProperty("@tags")]
        public string Tags;

        [JsonProperty("@created_at")]
        public string Creation;
    }
}
