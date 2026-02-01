using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlayniteExtensions.Common
{
    public class GameNameMatcher
    {
        /// <summary>
        /// <para>
        /// Converts a string to a lowercase alphanumeric key by removing all characters
        /// except letters and digits. Spaces, punctuation, and symbols are discarded.
        /// </para>
        /// <para>
        /// Examples:
        /// "NieR: Automata"    -> "nierautomata"
        /// "Final Fantasy VII" -> "finalfantasyvii"
        /// "DOOM (2016)"       -> "doom2016"
        /// </para>
        /// </summary>
        public static string ToAlphanumericLower(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(str.Length);
            foreach (char c in str)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// <para>
        /// Generates a machine-friendly key for matching game titles.
        /// The result is lowercase and contains only letters and digits.
        /// </para>
        /// <para>
        /// Examples:
        /// "Witcher 3, The"           -> "thewitcher3"<br/>
        /// "NieR: Automata™ [PC]"     -> "nierautomata"<br/>
        /// "Final Fantasy VII Remake" -> "finalfantasyviiremake"
        /// </para>
        /// </summary>
        public static string ToGameKey(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            var newName = str;
            newName = RemoveTrademarks(newName);

            // Remove bracketed metadata, e.g. "The Witcher 3 (GOTY Edition)" -> "The Witcher 3"
            newName = RemoveUnlessThatEmptiesTheString(newName, @"\[.*?\]");
            newName = RemoveUnlessThatEmptiesTheString(newName, @"\(.*?\)");

            // Moves ", The" suffix to the start of the string
            var trimmed = newName.TrimEnd();
            // Case 1: "Witcher 3, The"
            if (trimmed.EndsWith(", The", StringComparison.OrdinalIgnoreCase))
            {
                newName = "The " + trimmed.Substring(0, trimmed.Length - 5); // Remove ", The" (5 characters)
            }
            else
            {
                // Case 2: "Legend of Zelda, The: Breath of the Wild"
                const string marker = ", The:";
                int index = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var title = trimmed.Substring(0, index);
                    var rest = trimmed.Substring(index + marker.Length);
                    newName = $"The {title}:{rest}";
                }
            }

            var sb = new StringBuilder(newName.Length);
            foreach (char c in newName)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        private static string RemoveTrademarks(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            if (str.IndexOfAny(new[] { '™', '©', '®' }) < 0)
            {
                return str;
            }

            var sb = new StringBuilder(str.Length);
            foreach (var c in str)
            {
                switch (c)
                {
                    case '™':
                    case '©':
                    case '®':
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static string RemoveUnlessThatEmptiesTheString(string input, string pattern)
        {
            var output = Regex.Replace(input, pattern, string.Empty);
            if (string.IsNullOrWhiteSpace(output))
            {
                return input;
            }

            return output;
        }
    }
}
