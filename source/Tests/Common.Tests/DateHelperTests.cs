using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK.Models;
using System.Globalization;
using PlayniteExtensions.Common;

namespace Common.Tests
{
    [TestFixture]
    class DateHelperTests
    {
        public static object[] DateParseCases =
        {
            new object[] { "March 2023", new ReleaseDate(2023, 3), "en-US" },
            new object[] { "March 2023", new ReleaseDate(2023, 3), "fr-FR" },
            new object[] { "Mars 2023", new ReleaseDate(2023, 3), "fr-FR" },
            new object[] { "2023", new ReleaseDate(2023), "fr-FR" },
            new object[] { "March 1, 2023", new ReleaseDate(2023, 3, 1), "en-US" },
            new object[] { "March 1, 2023", new ReleaseDate(2023, 3, 1), "nl-NL" },
            new object[] { "1 maart 2023", new ReleaseDate(2023, 3, 1), "nl-NL" },
            new object[] { "Gibberish", null, "en-US" },
            new object[] { "Gibberish", null, "nl-NL" },
        };

        [TestCaseSource(nameof(DateParseCases))]
        public void ParseReleaseDateReturnsExpected(string input, ReleaseDate? expectedOutput, string cultureId)
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureId);
            var result = DateHelper.ParseReleaseDate(input);
            Assert.AreEqual(expectedOutput, result);
        }
    }
}
