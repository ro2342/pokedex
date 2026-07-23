using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Security.Authentication.Web;

namespace PokedexUWP.Services
{
    public sealed class AuthResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Uid { get; set; }
        public string IdToken { get; set; }
        public string RefreshToken { get; set; }
        public string Email { get; set; }
        public int ExpiresInSeconds { get; set; } = 3600;
    }

    // Login com Google trocado pelo login do Firebase via REST (Identity
    // Toolkit) - nao existe SDK oficial do Firebase pra UWP. Cliente OAuth
    // "Desktop app" (nao "Universal Windows Platform" - esse exige
    // associacao com a Microsoft Store) + WebAuthenticationBroker com
    // redirecionamento loopback, que o Windows intercepta sozinho. Mesma
    // decisao documentada em sincronizacao-nuvem-setup.md do theartistsway.
    public static class AuthService
    {
        public const string FirebaseApiKey = "AIzaSyALs_ANiMGLnSvBGt7NaY1xNIJmc2XCVw8";

        private const string GoogleDesktopClientId = "478864823886-fj5sv0q1rcf1ou60dcrtlngqvadkrih0.apps.googleusercontent.com";

        // Substituido pelo valor real (GitHub Actions secret) so no
        // momento do build - ver .github/workflows/02-build-appx.yml.
        // Nunca fica em texto puro no historico deste repositorio publico.
        private const string GoogleDesktopClientSecret = "__GOOGLE_OAUTH_DESKTOP_CLIENT_SECRET__";
        private const string GoogleDesktopRedirectUri = "http://127.0.0.1";

        public static async Task<AuthResult> SignInWithGoogleAsync()
        {
            try
            {
                string authUrl = "https://accounts.google.com/o/oauth2/v2/auth" +
                    "?client_id=" + Uri.EscapeDataString(GoogleDesktopClientId) +
                    "&redirect_uri=" + Uri.EscapeDataString(GoogleDesktopRedirectUri) +
                    "&response_type=code" +
                    "&scope=" + Uri.EscapeDataString("openid email profile");

                WebAuthenticationResult result = await WebAuthenticationBroker.AuthenticateAsync(
                    WebAuthenticationOptions.None, new Uri(authUrl), new Uri(GoogleDesktopRedirectUri));

                if (result.ResponseStatus != WebAuthenticationStatus.Success)
                {
                    return new AuthResult { Success = false, ErrorMessage = $"Navegador: {result.ResponseStatus} ({result.ResponseErrorDetail})" };
                }

                Uri responseUri = new Uri(result.ResponseData);
                Dictionary<string, string> parsed = ParseQueryString(responseUri.Query.TrimStart('?'));

                if (parsed.ContainsKey("error"))
                {
                    return new AuthResult { Success = false, ErrorMessage = "Google: " + parsed["error"] };
                }
                if (!parsed.ContainsKey("code"))
                {
                    return new AuthResult { Success = false, ErrorMessage = "Resposta do Google sem codigo de autorizacao." };
                }

                using (HttpClient client = new HttpClient())
                {
                    FormUrlEncodedContent tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["code"] = parsed["code"],
                        ["client_id"] = GoogleDesktopClientId,
                        ["client_secret"] = GoogleDesktopClientSecret,
                        ["redirect_uri"] = GoogleDesktopRedirectUri,
                        ["grant_type"] = "authorization_code",
                    });

                    HttpResponseMessage tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
                    string tokenResponseText = await tokenResponse.Content.ReadAsStringAsync();
                    if (!tokenResponse.IsSuccessStatusCode)
                    {
                        return new AuthResult { Success = false, ErrorMessage = $"Google token {(int)tokenResponse.StatusCode}: {tokenResponseText}" };
                    }

                    JsonObject tokenJson = JsonObject.Parse(tokenResponseText);
                    string idToken = tokenJson.ContainsKey("id_token") ? tokenJson["id_token"].GetString() : null;
                    string accessToken = tokenJson.ContainsKey("access_token") ? tokenJson["access_token"].GetString() : null;

                    if (!string.IsNullOrEmpty(idToken))
                    {
                        return await ExchangeWithFirebaseAsync(idToken, "id_token");
                    }
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        return await ExchangeWithFirebaseAsync(accessToken, "access_token");
                    }
                    return new AuthResult { Success = false, ErrorMessage = "Google nao devolveu id_token nem access_token." };
                }
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (string pair in query.Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                string[] kv = pair.Split(new[] { '=' }, 2);
                result[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            }
            return result;
        }

        private static async Task<AuthResult> ExchangeWithFirebaseAsync(string token, string tokenParamName)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={FirebaseApiKey}";
                    string postBody = $"{tokenParamName}={Uri.EscapeDataString(token)}&providerId=google.com";

                    JsonObject payload = new JsonObject
                    {
                        ["postBody"] = JsonValue.CreateStringValue(postBody),
                        ["requestUri"] = JsonValue.CreateStringValue("http://localhost"),
                        ["returnIdpCredential"] = JsonValue.CreateBooleanValue(true),
                        ["returnSecureToken"] = JsonValue.CreateBooleanValue(true),
                    };

                    HttpContent content = new StringContent(payload.Stringify(), Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(url, content);
                    string responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return new AuthResult { Success = false, ErrorMessage = $"Firebase {(int)response.StatusCode}: {responseText}" };
                    }

                    JsonObject json = JsonObject.Parse(responseText);
                    return new AuthResult
                    {
                        Success = true,
                        Uid = json.ContainsKey("localId") ? json["localId"].GetString() : null,
                        IdToken = json.ContainsKey("idToken") ? json["idToken"].GetString() : null,
                        RefreshToken = json.ContainsKey("refreshToken") ? json["refreshToken"].GetString() : null,
                        Email = json.ContainsKey("email") ? json["email"].GetString() : null,
                        ExpiresInSeconds = json.ContainsKey("expiresIn") && int.TryParse(json["expiresIn"].GetString(), out int exp) ? exp : 3600,
                    };
                }
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, ErrorMessage = "Troca com Firebase falhou: " + ex.Message };
            }
        }
    }
}
