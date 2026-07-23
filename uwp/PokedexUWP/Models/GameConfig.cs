using System.Collections.Generic;

namespace PokedexUWP.Models
{
    public sealed class GameConfig
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Sub { get; set; }
        public string Color { get; set; }
        public string Label { get; set; }
        public bool Oneway { get; set; }

        // Null means "no official dex restriction" (e.g. Pokemon HOME just
        // stores whatever you put there) - same convention as GAMES in
        // www/js/data.js (dex: null).
        public List<int> Dex { get; set; }
    }
}
