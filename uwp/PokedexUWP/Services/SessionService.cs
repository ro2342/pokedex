using System;
using Windows.Security.Credentials;

namespace PokedexUWP.Services
{
    public sealed class FirebaseSession
    {
        public string Uid { get; set; }
        public string IdToken { get; set; }
        public string RefreshToken { get; set; }
        public string Email { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public bool NeedsRefresh => DateTimeOffset.UtcNow > ExpiresAt.AddMinutes(-1);
    }

    // Guarda a sessao no PasswordVault (cofre de credenciais criptografado
    // pelo proprio Windows) em vez de arquivo texto puro - mesmo mecanismo
    // do theartistsway.
    public static class SessionService
    {
        private const string ResourceName = "PokedexUWP.GoogleSession";

        public static FirebaseSession GetSession()
        {
            PasswordVault vault = new PasswordVault();
            try
            {
                var creds = vault.FindAllByResource(ResourceName);
                if (creds.Count == 0) return null;
                PasswordCredential cred = creds[0];
                cred.RetrievePassword();
                string[] parts = cred.Password.Split('|');
                if (parts.Length < 4) return null;
                return new FirebaseSession
                {
                    Uid = cred.UserName,
                    IdToken = parts[0],
                    RefreshToken = parts[1],
                    Email = parts[2],
                    ExpiresAt = DateTimeOffset.Parse(parts[3]),
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SaveSession(string uid, string idToken, string refreshToken, string email, int expiresInSeconds)
        {
            ClearSession();
            PasswordVault vault = new PasswordVault();
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
            string password = $"{idToken}|{refreshToken}|{email}|{expiresAt:o}";
            vault.Add(new PasswordCredential(ResourceName, uid, password));
        }

        public static void UpdateTokens(string idToken, int expiresInSeconds)
        {
            FirebaseSession session = GetSession();
            if (session == null) return;
            SaveSession(session.Uid, idToken, session.RefreshToken, session.Email, expiresInSeconds);
        }

        public static void ClearSession()
        {
            PasswordVault vault = new PasswordVault();
            try
            {
                foreach (PasswordCredential cred in vault.FindAllByResource(ResourceName))
                {
                    vault.Remove(cred);
                }
            }
            catch (Exception)
            {
                // nada guardado ainda
            }
        }
    }
}
