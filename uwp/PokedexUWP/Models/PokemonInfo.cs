using System.Collections.Generic;

namespace PokedexUWP.Models
{
    public sealed class PokemonInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<string> Types { get; set; }
    }
}
