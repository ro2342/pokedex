// Google sign-in (Google Identity Services, ID-token flow - no client
// secret needed, that's only for the native UWP loopback flow) +
// Firestore REST sync. Same document shape as the UWP app so both
// platforms read/write the same data: users/{uid}/stores/collection,
// single "data" string field holding the whole JSON blob.
const SESSION_KEY = 'pokedexSession';
const SYNC_STORE_NAME = 'collection';

let syncSession = null; // { uid, idToken, refreshToken, expiresAt, email }

function loadSession() {
  try {
    syncSession = JSON.parse(localStorage.getItem(SESSION_KEY) || 'null');
  } catch (e) {
    syncSession = null;
  }
}

function saveSession() {
  localStorage.setItem(SESSION_KEY, JSON.stringify(syncSession));
}

function clearSession() {
  syncSession = null;
  localStorage.removeItem(SESSION_KEY);
}

function isSignedIn() {
  return !!(syncSession && syncSession.uid);
}

function loadGoogleScript() {
  return new Promise((resolve, reject) => {
    if (window.google && window.google.accounts) return resolve();
    const s = document.createElement('script');
    s.src = 'https://accounts.google.com/gsi/client';
    s.onload = resolve;
    s.onerror = reject;
    document.head.appendChild(s);
  });
}

async function signIn() {
  await loadGoogleScript();
  return new Promise((resolve, reject) => {
    google.accounts.id.initialize({
      client_id: GOOGLE_WEB_CLIENT_ID,
      callback: async (response) => {
        try {
          const result = await exchangeWithFirebase(response.credential);
          resolve(result);
        } catch (err) {
          reject(err);
        }
      },
    });
    google.accounts.id.prompt((notification) => {
      if (notification.isNotDisplayed() || notification.isSkippedMoment()) {
        reject(new Error('Login cancelado ou bloqueado pelo navegador.'));
      }
    });
  });
}

async function exchangeWithFirebase(idToken) {
  const url = `https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=${FIREBASE_CONFIG.apiKey}`;
  const postBody = `id_token=${encodeURIComponent(idToken)}&providerId=google.com`;
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      postBody,
      requestUri: location.origin,
      returnIdpCredential: true,
      returnSecureToken: true,
    }),
  });
  const json = await res.json();
  if (!res.ok) throw new Error(json.error?.message || 'Falha no login com Google.');

  syncSession = {
    uid: json.localId,
    idToken: json.idToken,
    refreshToken: json.refreshToken,
    email: json.email,
    displayName: json.displayName,
    expiresAt: Date.now() + (parseInt(json.expiresIn || '3600', 10) * 1000),
  };
  saveSession();
  return syncSession;
}

async function ensureFreshToken() {
  if (!syncSession) throw new Error('Nao logado.');
  if (Date.now() < syncSession.expiresAt - 60000) return syncSession.idToken;

  const res = await fetch(`https://securetoken.googleapis.com/v1/token?key=${FIREBASE_CONFIG.apiKey}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: `grant_type=refresh_token&refresh_token=${encodeURIComponent(syncSession.refreshToken)}`,
  });
  const json = await res.json();
  if (!res.ok) { clearSession(); throw new Error('Sessao expirada, entre novamente.'); }

  syncSession.idToken = json.id_token;
  syncSession.expiresAt = Date.now() + (parseInt(json.expires_in || '3600', 10) * 1000);
  saveSession();
  return syncSession.idToken;
}

function docUrl(uid) {
  return `https://firestore.googleapis.com/v1/projects/${FIREBASE_CONFIG.projectId}/databases/(default)/documents/users/${uid}/stores/${SYNC_STORE_NAME}`;
}

async function getRemoteState(idToken, uid) {
  const res = await fetch(docUrl(uid), { headers: { Authorization: `Bearer ${idToken}` } });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error('Falha ao ler dados da nuvem.');
  const doc = await res.json();
  const dataStr = doc.fields?.data?.stringValue;
  return dataStr ? JSON.parse(dataStr) : null;
}

async function putRemoteState(idToken, uid, state) {
  const body = {
    fields: {
      data: { stringValue: JSON.stringify(state) },
      updatedAt: { timestampValue: new Date().toISOString() },
    },
  };
  const res = await fetch(docUrl(uid), {
    method: 'PATCH',
    headers: { Authorization: `Bearer ${idToken}`, 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error('Falha ao enviar dados para a nuvem.');
}

async function syncNow() {
  if (!isSignedIn()) throw new Error('Nao logado.');
  const idToken = await ensureFreshToken();
  const local = getStateForSync();
  const remote = await getRemoteState(idToken, syncSession.uid);

  let winner = local;
  if (remote && (!local.updatedAt || new Date(remote.updatedAt) > new Date(local.updatedAt))) {
    winner = remote;
  }

  if (winner !== local) replaceStateFromSync(winner);
  await putRemoteState(idToken, syncSession.uid, winner);
  return winner;
}

function signOut() {
  clearSession();
}
