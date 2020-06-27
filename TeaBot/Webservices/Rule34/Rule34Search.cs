using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeaBot.Main;
using TeaBot.Utilities;

namespace TeaBot.Webservices
{
    /// <summary>
    ///     Class for working with rule34.xxx API.
    /// </summary>
    class Rule34Search
    {
        // URL for rule34.xxx API access
        private const string Rule34ApiURL = "https://rule34.xxx/index.php?page=dapi&s=post&q=index";

        // Request tags
        private readonly IEnumerable<string> _tags;

        // Blacklisted tags
        private readonly IEnumerable<string> _defaultBlacklist;
        private readonly IEnumerable<string> _userBlacklist;
        private readonly IEnumerable<string> _guildBlacklist;

        public Rule34Search(IEnumerable<string> tags, IEnumerable<string> defaultBlacklist, IEnumerable<string> userBlacklist, IEnumerable<string> guildBlacklist)
        {
            // Ensure tags aren't blacklisted
            if (defaultBlacklist.Any(tag => tags.Contains(tag)))
                throw new R34SearchException("Your tag combination contains tags blacklisted by the default blacklist.");
            if (userBlacklist.Any(tag => tags.Contains(tag)))
                throw new R34SearchException("Your tag combination contains tags blacklisted by you.");
            if (guildBlacklist.Any(tag => tags.Contains(tag)))
                throw new R34SearchException("Your tag combination contains tags blacklisted by the guild.");

            _tags = tags;
            _defaultBlacklist = defaultBlacklist;
            _userBlacklist = userBlacklist;
            _guildBlacklist = guildBlacklist;
        }

        /// <summary>
        ///     Deserializes the provided JSON string to a Rule34Post instance
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
        ///     Get a random post using a random PID.
        /// </summary>
        /// <param name="postCount">Post count with the tag combination (request tags + blacklisted tags) for this instance.</param>
        /// <returns>Raw JSON string</returns>
        public async Task<string> GetRandomPostAsync(int postCount)
        {
            string tags = GetTagsForURL();

            // Generate a random PID (and constrain it between 0 and 2000 inclusive because rule34.xxx rejects PIDs over 2000)
            int randomPID = new Random().Next(0, Math.Min(2001, postCount));
            var response = await TeaEssentials.HttpClient.GetAsync($"{Rule34ApiURL}&pid={randomPID}&tags={tags}&limit=1");

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            // Raise an exception if the search is limited
            if (content.Contains("Search error: API limited due to abuse"))
                throw new R34SearchException("Search limited. Try again?");

            return WebUtilities.XMLStringToJsonString(content);
        }

        /// <summary>
        ///     Get result count for a tag combination
        /// </summary>
        /// <returns>Count of posts that contain the given tags.</returns>
        public async Task<int> GetResultCountAsync()
        {
            string tags = GetTagsForURL();

            var response = await TeaEssentials.HttpClient.GetAsync($"{Rule34ApiURL}&limit=0&tags={tags}");

            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(content);

            return Convert.ToInt32(xmlDoc["posts"].Attributes["count"].Value);
        }

        /// <summary>
        ///     Creates a string out of request tags and blacklisted tags and formats for using
        /// </summary>
        /// <returns></returns>
        private string GetTagsForURL()
        {
            var overallBlacklist = _defaultBlacklist.Concat(_userBlacklist).Concat(_guildBlacklist);
            return WebUtilities.FormatStringForURL(string.Join(' ', _tags.Concat(overallBlacklist.Select(x => $"-{x}"))));
        }
    }

    /// <summary>
    ///     A separate exception to raise when the search cannot happen or when it fails.
    /// </summary>
    class R34SearchException : Exception
    {
        public R34SearchException(string message)
            : base(message)
        {
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
