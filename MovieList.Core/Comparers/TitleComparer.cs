using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MovieList.Comparers
{
    public sealed class TitleComparer : IComparer<string>
    {
        private static readonly Regex NumberRegex = new Regex("([0-9]+)", RegexOptions.Compiled);

        private readonly EnumerableComparer<object> comparer;

        public TitleComparer(CultureInfo culture)
            => this.comparer = new EnumerableComparer<object>(
                new StringOrIntComparer(culture.CompareInfo.GetStringComparer(CompareOptions.None)));

        public int Compare(string x, string y)
            => this.comparer.Compare(
                NumberRegex.Split(this.Normalize(x)).Select(this.Convert),
                NumberRegex.Split(this.Normalize(y)).Select(this.Convert));

        private string Normalize(string title)
            => title.ToLower()
                .Replace(":", "")
                .Replace(";", "")
                .Replace("!", "")
                .Replace("?", "")
                .Replace(".", "")
                .Replace(",", "")
                .Replace(" - ", " ")
                .Replace(" – ", " ")
                .Replace("-", " ")
                .Replace("'", "")
                .Replace("’", "")
                .Replace("\"", "")
                .Replace("  ", " ");

        private object Convert(string title)
            => Int32.TryParse(title, out int result) ? (object)result : title;
    }
}
