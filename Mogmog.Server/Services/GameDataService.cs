using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Mogmog.Server.Services
{
    public class GameDataService
    {
        public Dictionary<int, string> Worlds { get; private set; }

        public GameDataService()
        {
            InitializeWorldStore();
        }

        /// <summary>
        /// Loads the world JSON into <see cref="Worlds"/> and appends the contents of <see cref="PseudoWorld"/> to it.
        /// </summary>
        private void InitializeWorldStore()
        {
            var worldJson = File.ReadAllText(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "world.json"));
            Worlds = JsonConvert.DeserializeObject<Dictionary<int, string>>(worldJson);
            // It's not very efficient to do this, but it is practical (and it only happens on startup).
            // We can treat the PseudoWorld enum as an extension to the Dictionary, making it easy to edit the world list with more client types.
            foreach (var pseudoWorld in Enum.GetValues(typeof(PseudoWorld))) {
                Worlds.Add((int)pseudoWorld, pseudoWorld.ToString());
            }
        }
    }
}
