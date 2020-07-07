using System.Xml;
using Newtonsoft.Json;

namespace TeaBot.Utilities
{
    /// <summary>
    ///     Utility class for web-based methods.
    /// </summary>
    public static class WebUtilities
    {
        /// <summary>
        ///     Converts XML to JSON
        /// </summary>
        /// <param name="xmlString">XML string to convert</param>
        /// <returns>The JSON string</returns>
        public static string XMLStringToJsonString(string xmlString)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlString);
            doc.RemoveChild(doc.FirstChild);

            return JsonConvert.SerializeXmlNode(doc);
        }

        /// <summary>
        ///     Formats a string for usage in URLs by trimming its ends and replacing spaces with + and double quotes with %20
        /// </summary>
        /// <param name="toFormat">String to format</param>
        /// <returns>Formatted string</returns>
        public static string FormatStringForURL(string toFormat)
        {
            return toFormat.TrimStart().TrimEnd().Replace(" ", "+").Replace("\"", "%22");
        }
    }
}
