using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace Mogmog.Tests.Stubs
{
    public class TestConfiguration : IConfiguration
    {
        private readonly IDictionary<string, string> inputs;

        public string this[string key] { get => this.inputs[key]; set => throw new System.NotImplementedException(); }

        public TestConfiguration(IDictionary<string, string> inputs)
        {
            this.inputs = inputs;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            throw new System.NotImplementedException();
        }

        public IChangeToken GetReloadToken()
        {
            throw new System.NotImplementedException();
        }

        public IConfigurationSection GetSection(string key)
        {
            throw new System.NotImplementedException();
        }
    }
}
