using System;
using System.Collections.Generic;
using System.Linq;

namespace TeaBot.Utilities
{
    /// <summary>
    ///     Static utility class for <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static class EnumerableUtilities
    {
        /// <summary>
        ///     Shortens an <see cref="IEnumerable{T}"/> such that the sum of the lengths of its elements is the the highest possible below <paramref name="characterLimit"/>.
        /// </summary>
        /// <param name="collection">The <see cref="IEnumerable{T}"/> to shorten.</param>
        /// <param name="characterLimit">The limit of characters.</param>
        /// <param name="collectionSplitter">A combination of characters used for joining the collection.</param>
        /// <returns>The shortened collection.</returns>
        public static IEnumerable<string> Shorten(this IEnumerable<string> collection, int characterLimit, string collectionSplitter)
        {
            // Total length of elements' lengths
            int totalLength = 0;

            // Determine the count
            int countToTake = 0;

            foreach (string element in collection)
            {
                totalLength += element.Length;

                if (totalLength + countToTake * collectionSplitter.Length >= characterLimit)
                    break;

                countToTake++;
            }

            return collection.Take(countToTake);
        }

        /// <summary>
        ///     Shortens an <see cref="IEnumerable{T}"/> such that the sum of the lengths of its elements is below <paramref name="characterLimit"/>.
        /// </summary>
        /// <param name="collection">The <see cref="IEnumerable{T}"/> to shorten.</param>
        /// <param name="stringConversionFunc">The function to use to convert <typeparamref name="T"/> to <see cref="string"/>.</param>
        /// <param name="characterLimit">The limit of characters.</param>
        /// <param name="collectionSplitter">A combination of characters used for joining the collection.</param>
        /// <returns>The shortened collection.</returns>
        public static IEnumerable<string> Shorten<T>(this IEnumerable<T> collection, Func<T, string> stringConversionFunc, int characterLimit, string collectionSplitter)
        {
            return collection.Select(stringConversionFunc).Shorten(characterLimit, collectionSplitter);
        }
    }
}
