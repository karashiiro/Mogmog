using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Mogmog
{
    public static class DateTimeUtils
    {
        public static readonly Regex Time = new Regex(@"\d+:\d+\s?(?:(?:AM)|(?:PM))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static readonly Regex TimeHours = new Regex(@"\d+(?=:)", RegexOptions.Compiled);
        public static readonly Regex TimeMinutes = new Regex(@"\d+(?=(?:AM|PM))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static readonly Regex TimeMeridiem = new Regex(@"[^\s\d:]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static readonly Regex DayOrDate = new Regex(@"(?:\d+\/\d+\/\d+)|(?:\d+\/\d+)|(?:\s[^\s]+day)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Infers a <see cref="DateTime"/> object from a string.
        /// </summary>
        public static DateTime GetDateTime(string text)
        {
            DateTime date = DateTime.MinValue;
            Match dayMatch = DayOrDate.Match(text);
            if (dayMatch.Success)
            {
                string dayOrDate = dayMatch.Value.Trim();
                if (dayOrDate.IndexOf("/", StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    int[] mmddyyyy = dayOrDate.Split('/').Select(term => int.Parse(term)).ToArray();
                    date = new DateTime(mmddyyyy.Length == 3 ? mmddyyyy[2] : DateTime.Now.Year, mmddyyyy[0], mmddyyyy[1]);
                }
                else
                {
                    var requestedDay = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), dayOrDate);
                    // https://stackoverflow.com/a/6346190 big fan
                    int daysUntil = (requestedDay - DateTime.Now.DayOfWeek + 7) % 7;
                    date = DateTime.Now.AddDays(daysUntil);
                }
            }
            if (date == DateTime.MinValue)
            {
                throw new ArgumentException();
            }
            Match timeMatch = Time.Match(text);
            date.AddSeconds(-date.Second)
                   .AddMilliseconds(-date.Millisecond);
            if (timeMatch.Success)
            {
                string time = timeMatch.Value.Replace(" ", "").Trim().ToUpper();
                int hours = int.Parse(TimeHours.Match(time).Value);
                int minutes = int.Parse(TimeMinutes.Match(time).Value);
                string meridiem = TimeMeridiem.Match(time).Value;
                if (meridiem == "PM")
                {
                    hours += 12;
                    hours += 12;
                }
                date.AddHours(hours - date.Hour)
                    .AddMinutes(minutes - date.Minute);
            }
            else
            {
                date.AddHours(-date.Hour)
                    .AddMinutes(-date.Minute);
            }

            return date;
        }
    }
}
