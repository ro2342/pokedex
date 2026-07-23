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

        // gameId -> (pokemonId -> numero regional). So cobre os jogos com
        // Pokedex regional propria (bd/arceus/za) - fonte: PokeAPI, ver
        // www/js/data.js REGIONAL_DEX pro comentario completo.
        public static Dictionary<string, Dictionary<int, int>> RegionalDex { get; private set; } = new Dictionary<string, Dictionary<int, int>>();

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

            RegionalDex = new Dictionary<string, Dictionary<int, int>>();
            if (root.ContainsKey("regionalDex"))
            {
                JsonObject regionalRoot = root["regionalDex"].GetObject();
                foreach (string gameId in regionalRoot.Keys)
                {
                    JsonObject perPokemon = regionalRoot[gameId].GetObject();
                    Dictionary<int, int> map = new Dictionary<int, int>();
                    foreach (string idStr in perPokemon.Keys)
                    {
                        map[int.Parse(idStr)] = (int)perPokemon[idStr].GetNumber();
                    }
                    RegionalDex[gameId] = map;
                }
            }
        }

        public static GameConfig GameById(string id) => Games.FirstOrDefault(g => g.Id == id);

        public static bool InDex(int pokemonId, string gameId)
        {
            GameConfig g = GameById(gameId);
            if (g?.Dex == null) return true;
            return g.Dex.Contains(pokemonId);
        }

        // Z-A tem duas dexes: a principal (Lumiose City) e a do Mega
        // Dimension (DLC, numeracao propria). Se o pokemon nao esta na
        // principal mas esta na do DLC, devolve com prefixo "MD" pra nao
        // confundir os dois numeros. Devolve null se nao esta em nenhuma
        // (o chamador cai pro National Dex nesse caso).
        public static string RegionalNumberLabel(string gameId, int pokemonId)
        {
            if (RegionalDex.TryGetValue(gameId, out Dictionary<int, int> map) && map.TryGetValue(pokemonId, out int num))
            {
                return "#" + num.ToString("D3");
            }
            if (gameId == "za" && RegionalDex.TryGetValue("zaMega", out Dictionary<int, int> megaMap) && megaMap.TryGetValue(pokemonId, out int megaNum))
            {
                return "MD#" + megaNum.ToString("D3");
            }
            return null;
        }
    }
}
