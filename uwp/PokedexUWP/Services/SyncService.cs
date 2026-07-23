using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace PokedexUWP.Services
{
    // Sincroniza o store "collection" com o Firestore REST
    // (firestore.googleapis.com) - sem SDK oficial pra UWP. Documento
    // users/{uid}/stores/collection, campo "data" com o JSON inteiro do
    // store como string (evita mapear pros tipos nativos do Firestore).
    // Mescla o blob inteiro (nao por registro) por "updatedAt" - o store
    // e um mapa simples pid->gid->bool, nao uma lista de registros
    // independentes.
    public static class SyncService
    {
        private const string ProjectId = "pokedex-5555f";

        public static async Task<string> SyncNowAsync()
        {
            FirebaseSession session = SessionService.GetSession();
            if (session == null)
            {
                return "Nao logado - nada pra sincronizar.";
            }

            string idToken = session.IdToken;
            if (session.NeedsRefresh)
            {
                idToken = await RefreshIdTokenAsync(session);
                if (idToken == null)
                {
                    return "Sessao expirada - entre novamente.";
                }
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                    JsonObject local = await LocalDataStore.GetStoreForSyncAsync("collection");
                    JsonObject remote = await GetRemoteStoreAsync(client, session.Uid);
                    JsonObject winner = MergeWholeBlob(local, remote);

                    await LocalDataStore.WriteStoreForSyncAsync("collection", winner);
                    await PutRemoteStoreAsync(client, session.Uid, winner);
                }
                return "Sincronizado as " + DateTimeOffset.Now.ToString("HH:mm");
            }
            catch (Exception ex)
            {
                return "Falha ao sincronizar (tenta de novo mais tarde): " + ex.Message;
            }
        }

        private static JsonObject MergeWholeBlob(JsonObject local, JsonObject remote)
        {
            if (remote == null || remote.Count == 0) return local;
            if (local == null || local.Count == 0) return remote;

            DateTimeOffset localTs = ParseTimestamp(local);
            DateTimeOffset remoteTs = ParseTimestamp(remote);
            return remoteTs > localTs ? remote : local;
        }

        private static DateTimeOffset ParseTimestamp(JsonObject obj)
        {
            if (obj.ContainsKey("updatedAt") && obj["updatedAt"].ValueType == JsonValueType.String &&
                DateTimeOffset.TryParse(obj["updatedAt"].GetString(), out DateTimeOffset result))
            {
                return result;
            }
            return DateTimeOffset.MinValue;
        }

        private static string DocUrl(string uid) =>
            $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents/users/{uid}/stores/collection";

        private static async Task<JsonObject> GetRemoteStoreAsync(HttpClient client, string uid)
        {
            HttpResponseMessage response = await client.GetAsync(DocUrl(uid));
            if (response.StatusCode == HttpStatusCode.NotFound) return new JsonObject();
            if (!response.IsSuccessStatusCode) throw new Exception($"Firestore GET: {(int)response.StatusCode}");

            string text = await response.Content.ReadAsStringAsync();
            JsonObject doc = JsonObject.Parse(text);
            if (!doc.ContainsKey("fields")) return new JsonObject();
            JsonObject fields = doc["fields"].GetObject();
            if (!fields.ContainsKey("data")) return new JsonObject();
            string dataJson = fields["data"].GetObject()["stringValue"].GetString();
            return JsonObject.Parse(dataJson);
        }

        private static async Task PutRemoteStoreAsync(HttpClient client, string uid, JsonObject data)
        {
            JsonObject body = new JsonObject
            {
                ["fields"] = new JsonObject
                {
                    ["data"] = new JsonObject { ["stringValue"] = JsonValue.CreateStringValue(data.Stringify()) },
                    ["updatedAt"] = new JsonObject { ["timestampValue"] = JsonValue.CreateStringValue(DateTimeOffset.UtcNow.ToString("o")) },
                },
            };

            HttpContent content = new StringContent(body.Stringify(), Encoding.UTF8, "application/json");
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), DocUrl(uid)) { Content = content };
            HttpResponseMessage response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Firestore PATCH: {(int)response.StatusCode} {errorText}");
            }
        }

        private static async Task<string> RefreshIdTokenAsync(FirebaseSession session)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    FormUrlEncodedContent body = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["refresh_token"] = session.RefreshToken,
                    });
                    HttpResponseMessage response = await client.PostAsync(
                        $"https://securetoken.googleapis.com/v1/token?key={AuthService.FirebaseApiKey}", body);
                    string text = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) return null;

                    JsonObject json = JsonObject.Parse(text);
                    string idToken = json.ContainsKey("id_token") ? json["id_token"].GetString() : null;
                    int expiresIn = json.ContainsKey("expires_in") && int.TryParse(json["expires_in"].GetString(), out int e) ? e : 3600;
                    if (string.IsNullOrEmpty(idToken)) return null;

                    SessionService.UpdateTokens(idToken, expiresIn);
                    return idToken;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
