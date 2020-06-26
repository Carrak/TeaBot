using System;
using System.Globalization;

namespace TeaBot.Utilities
{
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

            int years = span.Year - 1;
            int months = span.Month - 1;
            int days = span.Day - 1;

            if (years == 0 && months == 0 && days == 0)
                return $"{PeriodToString(span.Hour, "hour")}{PeriodToString(span.Minute, "minute")}{PeriodToString(span.Second, "second")}ago";
            else
                return $"{PeriodToString(years, "year")}{PeriodToString(months, "month")}{PeriodToString(days, "day")}ago";
        }

        /// <summary>
        ///   Creates a string that transforms a named period of time into a string, e.g. "3 days", "1 month", etc.
        /// </summary>
        /// <param name="timePassed">The amount of the given period time.</param>
        /// <param name="timePeriod">The name of the period, e.g. month, day, etc.</param>
        /// <param name="insertSpacebar">Bool value determining whether a spacebar should be inserted at the end of the end string.</param>
        /// <returns>Created string with the number and the period, or an empty string if <paramref name="timePassed"/> is zero.</returns>
        public static string PeriodToString(int timePassed, string timePeriod, bool insertSpacebar = true)
        {
            if (timePassed == 0) return "";

            string result = $"{timePassed} {timePeriod}" +
                $"{(Math.Abs(timePassed) != 1 ? "s" : "")}" +
                $"{(insertSpacebar ? " " : "")}";

            return result;
        }
    }
}
