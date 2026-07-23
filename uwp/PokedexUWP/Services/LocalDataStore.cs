using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace PokedexUWP.Services
{
    // Um unico "store" (collection.json) guarda tudo: { data: { "<pid>":
    // { "<gid>": true } }, updatedAt: "..." } - mesmo formato de documento
    // que o SyncService espelha no Firestore (users/{uid}/stores/collection),
    // so que em arquivo local em vez de IndexedDB (equivalente ao www/js/db.js).
    public static class LocalDataStore
    {
        public static readonly string[] SyncStoreNames = { "collection" };
        private const string FileName = "collection.json";

        public static async Task<JsonObject> GetStoreForSyncAsync(string storeName)
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(FileName);
                string text = await FileIO.ReadTextAsync(file);
                return JsonObject.Parse(text);
            }
            catch (Exception)
            {
                return new JsonObject();
            }
        }

        public static async Task WriteStoreForSyncAsync(string storeName, JsonObject data)
        {
            StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, data.Stringify());
        }

        public static Task<JsonObject> LoadAsync() => GetStoreForSyncAsync("collection");

        public static async Task SaveAsync(JsonObject store)
        {
            store["updatedAt"] = JsonValue.CreateStringValue(DateTimeOffset.UtcNow.ToString("o"));
            await WriteStoreForSyncAsync("collection", store);
        }

        private static JsonObject DataOf(JsonObject store) =>
            store.ContainsKey("data") ? store["data"].GetObject() : new JsonObject();

        public static bool Has(JsonObject store, int pokemonId, string gameId)
        {
            JsonObject data = DataOf(store);
            string key = pokemonId.ToString();
            if (!data.ContainsKey(key)) return false;
            JsonObject entry = data[key].GetObject();
            return entry.ContainsKey(gameId) && entry[gameId].GetBoolean();
        }

        public static void Set(JsonObject store, int pokemonId, string gameId, bool value)
        {
            if (!store.ContainsKey("data")) store["data"] = new JsonObject();
            JsonObject data = store["data"].GetObject();
            string key = pokemonId.ToString();
            JsonObject entry = data.ContainsKey(key) ? data[key].GetObject() : new JsonObject();

            if (value) entry[gameId] = JsonValue.CreateBooleanValue(true);
            else if (entry.ContainsKey(gameId)) entry.Remove(gameId);

            if (entry.Count == 0)
            {
                if (data.ContainsKey(key)) data.Remove(key);
            }
            else
            {
                data[key] = entry;
            }
        }

        public static List<string> MyLocations(JsonObject store, int pokemonId)
        {
            List<string> result = new List<string>();
            JsonObject data = DataOf(store);
            string key = pokemonId.ToString();
            if (!data.ContainsKey(key)) return result;
            JsonObject entry = data[key].GetObject();
            foreach (Models.GameConfig g in ContentStore.Games)
            {
                if (entry.ContainsKey(g.Id) && entry[g.Id].GetBoolean()) result.Add(g.Id);
            }
            return result;
        }
    }
}
