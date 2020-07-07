using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TeaBot.Utilities
{
    /// <summary>
    ///     Utility class for working with <see cref="DateTime"/> and <see cref="TimeSpan"/>.
    /// </summary>
    public static class TimeUtilities
    {
        /// <summary>
        ///     Creates a string containing full information about a date.
        /// </summary>
        /// <param name="date"><see cref="DateTime"/> object to get info on.</param>
        /// <param name="displayTime">Bool value determining whether the hours, minutes and seconds should be displayed.</param>
        /// <returns></returns>
        public static string DateString(DateTime date, bool displayTime = false)
        {
            if (displayTime)
                return $"{date:dd.MM.yyyy HH:mm:ss} UTC\n{SpanBetweenDatesString(date, DateTime.UtcNow)}";
            else
                return $"{date:dd.MM.yyyy}\n{date.ToString("MMMM d, yyyy (ddd)", new CultureInfo("en-US"))}\n{SpanBetweenDatesString(date, DateTime.UtcNow)}";
        }

        /// <summary>
        ///     Calculates the amount of time between <paramref name="start"></paramref> and <paramref name="end"></paramref> 
        ///     and creates a string that represents that time in either years/months/days or hours/minutes/seconds
        /// </summary>
        /// <returns>String in format "X years X months X days ago" or "X hours X minutes X seconds ago"</returns>
        public static string SpanBetweenDatesString(DateTime start, DateTime end)
        {
            if (start > end)
                return SpanBetweenDatesString(end, start);

            TimeSpan timePassed = end - start;

            if (timePassed.TotalSeconds < 1)
                return "Right now";

            DateTime span = DateTime.MinValue + timePassed;
            
            List<string> thresholds = new List<string>
            {
                Pluralize(span.Year - 1, "year"),
                Pluralize(span.Month - 1, "month"),
                Pluralize(span.Day - 1, "day"),
                Pluralize(span.Hour, "hour"),
                Pluralize(span.Minute, "minute"),
                Pluralize(span.Second, "second")
            };
            
            return $"{string.Join(" ", thresholds.Where(x => !string.IsNullOrEmpty(x)).Take(3))} ago";
        }

        /// <summary>
        ///   Pluralizes a given word if needed.
        /// </summary>
        /// <param name="quantity">The quantity to use to determine if a plural is needed.</param>
        /// <param name="word">The word to pluralize</param>
        /// <returns>String with the word and the quantity, or an empty string if <paramref name="quantity"/> is zero.</returns>
        public static string Pluralize(int quantity, string word)
        {
            return quantity == 0 ? "" : $"{quantity} {word}{(Math.Abs(quantity) != 1 ? "s" : "")}";
        }
    }
}
