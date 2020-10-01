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
                return $"{date:dd.MM.yyyy HH:mm:ss} UTC\n{SpanBetweenDates(date, DateTime.UtcNow)}";
            else
                return $"{date:dd.MM.yyyy}\n{date.ToString("MMMM d, yyyy (ddd)", new CultureInfo("en-US"))}\n{SpanBetweenDates(date, DateTime.UtcNow)}";
        }

        /// <summary>
        ///     Calculates the amount of time between <paramref name="start"></paramref> and <paramref name="end"></paramref> 
        ///     and creates a string that represents that time in the last three non-zero time thresholds (years, months, etc)
        /// </summary>
        /// <returns>String in format "X years X months X days ago" etc.</returns>
        public static string SpanBetweenDates(DateTime start, DateTime end)
        {
            if (start > end)
                return SpanBetweenDates(end, start);

            TimeSpan timePassed = end - start;

            if (timePassed.TotalSeconds < 1)
                return "Right now";

            DateTime span = DateTime.MinValue + timePassed;

            List<string> thresholds = new List<string>();

            if (span.Year - 1 != 0)
                thresholds.Add(GeneralUtilities.Pluralize(span.Year - 1, "year"));
            if (span.Month - 1 != 0)
                thresholds.Add(GeneralUtilities.Pluralize(span.Month - 1, "month"));
            if (span.Day - 1 != 0)
                thresholds.Add(GeneralUtilities.Pluralize(span.Day - 1, "day"));
            if (span.Hour != 0)
                thresholds.Add(GeneralUtilities.Pluralize(span.Hour, "hour"));
            if (span.Minute != 0)
                thresholds.Add(GeneralUtilities.Pluralize(span.Minute, "minute"));
            if (span.Second != 0)
                thresholds.Add(GeneralUtilities.Pluralize(span.Second, "second"));

            return $"{string.Join(" ", thresholds.Take(3))} ago";
        }
    }
}
