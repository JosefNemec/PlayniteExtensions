using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayniteExtensions.Common
{
    public static class DateHelper
    {
        public static ReleaseDate? ParseReleaseDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return null;
            }

            var currentCulture = CultureInfo.CurrentCulture;
            var invariantCulture = CultureInfo.InvariantCulture;
            var dateTimeStyles = DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces;

            DateTime parsedDate;

            if (DateTime.TryParseExact(dateString, "yyyy", currentCulture, dateTimeStyles, out parsedDate)
                || DateTime.TryParseExact(dateString, "yyyy", invariantCulture, dateTimeStyles, out parsedDate))
            {
                return new ReleaseDate(parsedDate.Year);
            }

            var yearAndMonthFormats = new[] { "MMM yyyy", "MMMM yyyy" };

            if (DateTime.TryParseExact(dateString, yearAndMonthFormats, currentCulture, dateTimeStyles, out parsedDate)
                || DateTime.TryParseExact(dateString, yearAndMonthFormats, invariantCulture, dateTimeStyles, out parsedDate))
            {
                return new ReleaseDate(parsedDate.Year, parsedDate.Month);
            }

            if (DateTime.TryParse(dateString, currentCulture, dateTimeStyles, out parsedDate)
                || DateTime.TryParse(dateString, invariantCulture, dateTimeStyles, out parsedDate))
            {
                return new ReleaseDate(parsedDate);
            }

            return null;
        }
    }
}
