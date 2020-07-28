using System;

namespace TeaBot.Utilities
{
    static class GeneralUtilities
    {
        /// <summary>
        ///   Pluralizes a given word if needed.
        /// </summary>
        /// <param name="quantity">The quantity to use to determine if a plural is needed.</param>
        /// <param name="word">The word to pluralize.</param>
        /// <returns>String with the word and the quantity.</returns>
        public static string Pluralize(int quantity, string word) => $"{quantity} {word}{(Math.Abs(quantity) != 1 ? "s" : "")}";
    }
}
