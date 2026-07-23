using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using PokedexUWP.Models;
using Windows.Data.Json;
using Windows.Storage;

namespace PokedexUWP.Services
{
    // Carrega Data/content.json (gerado a partir de www/js/data.js por
    // scripts/generate-content-json.js - C# nao executa JavaScript, entao
    // isso mantem a lista de Pokemon/jogos como fonte unica).
    public static class ContentStore
    {
        public static List<PokemonInfo> Pokemon { get; private set; } = new List<PokemonInfo>();
        public static List<GameConfig> Games { get; private set; } = new List<GameConfig>();
        public static List<string> ChipIds { get; private set; } = new List<string>();

        public static async Task LoadAsync()
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Data/content.json"));
            string text = await FileIO.ReadTextAsync(file);
            JsonObject root = JsonObject.Parse(text);

            Pokemon = root["pokemon"].GetArray().Select(v =>
            {
                JsonObject o = v.GetObject();
                return new PokemonInfo
                {
                    Id = (int)o["id"].GetNumber(),
                    Name = o["n"].GetString(),
                    Types = o["t"].GetArray().Select(t => t.GetString()).ToList(),
                };
            }).ToList();

            Games = root["games"].GetArray().Select(v =>
            {
                JsonObject o = v.GetObject();
                List<int> dex = null;
                if (o["dex"].ValueType != JsonValueType.Null)
                {
                    dex = o["dex"].GetArray().Select(d => (int)d.GetNumber()).ToList();
                }
                return new GameConfig
                {
                    Id = o["id"].GetString(),
                    Name = o["name"].GetString(),
                    Sub = o["sub"].GetString(),
                    Color = o["color"].GetString(),
                    Label = o["label"].GetString(),
                    Oneway = o["oneway"].GetBoolean(),
                    Dex = dex,
                };
            }).ToList();

            ChipIds = root["chipIds"].GetArray().Select(v => v.GetString()).ToList();
        }

        public static GameConfig GameById(string id) => Games.FirstOrDefault(g => g.Id == id);

        public static bool InDex(int pokemonId, string gameId)
        {
            GameConfig g = GameById(gameId);
            if (g?.Dex == null) return true;
            return g.Dex.Contains(pokemonId);
        }
    }
}
