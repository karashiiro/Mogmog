using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mogmog
{
    // https://stackoverflow.com/a/8854209
    public static class UriExtensions
    {
        private static readonly Regex queryStringRegex = new Regex(@"[\?&](?<name>[^&=]+)=(?<value>[^&=]+)", RegexOptions.Compiled);

        public static IEnumerable<KeyValuePair<string, string>> ParseQueryString(this Uri uri)
        {
            if (uri == null)
                throw new ArgumentException(nameof(uri));

            var matches = queryStringRegex.Matches(uri.OriginalString);
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                yield return new KeyValuePair<string, string>(match.Groups["name"].Value, match.Groups["value"].Value);
            }
        }
    }
}
