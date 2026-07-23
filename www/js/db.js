// Local state: { data: { [pokemonId]: { [gameId]: true } }, updatedAt: ISOString }
// Whole blob synced as one Firestore document (see sync.js) - simplest
// merge rule (newest updatedAt wins) since this isn't a list of
// independent records, just one big ownership map.
const STORAGE_KEY = 'pokedexState';

let S = { data: {}, updatedAt: null };

function load() {
  try {
    const raw = JSON.parse(localStorage.getItem(STORAGE_KEY) || 'null');
    if (raw && raw.data) {
      S = raw;
    }
  } catch (e) {
    S = { data: {}, updatedAt: null };
  }
}

function persist() {
  S.updatedAt = new Date().toISOString();
  localStorage.setItem(STORAGE_KEY, JSON.stringify(S));
}

function has(pid, gid) {
  return !!(S.data[pid] && S.data[pid][gid]);
}

function setG(pid, gid, v) {
  if (!S.data[pid]) S.data[pid] = {};
  if (v) {
    S.data[pid][gid] = true;
  } else {
    delete S.data[pid][gid];
    if (Object.keys(S.data[pid]).length === 0) delete S.data[pid];
  }
  persist();
  if (typeof onStateChanged === 'function') onStateChanged();
}

function myLocs(pid) {
  return GAMES.filter(g => has(pid, g.id));
}

function inDex(pid, gid) {
  const g = GAMES.find(x => x.id === gid);
  if (!g || !g.dex) return true;
  return g.dex.has(pid);
}

function clearAll() {
  S = { data: {}, updatedAt: new Date().toISOString() };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(S));
  if (typeof onStateChanged === 'function') onStateChanged();
}

// Used by sync.js to read/replace the whole blob without touching UI state directly.
function getStateForSync() {
  return S;
}

function replaceStateFromSync(newState) {
  S = newState;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(S));
  if (typeof onStateChanged === 'function') onStateChanged();
}
