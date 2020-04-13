using System.Collections.Generic;
using System.Linq;

namespace Mogmog
{
    public class FormData
    {
        private readonly IDictionary<string, string> fields;

        public FormData()
            => fields = new Dictionary<string, string>();

        public void Add(string key, string value)
            => fields.Add(key, value);

        public override string ToString()
            => string.Join("&", fields.Select((kvp) => $"{kvp.Key}={kvp.Value}").ToArray());
    }
}
