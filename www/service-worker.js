// Cache-first for our own assets (app shell + local sprites), so the
// app works offline. NEVER intercept cross-origin requests (Firestore,
// Google auth, the online sprite fallback) - caching those breaks sync
// and traps users on stale data (lesson learned the hard way on
// theartistsway's service worker).
const CACHE_NAME = 'pokedex-v1';
const APP_SHELL = [
  './',
  './index.html',
  './css/style.css',
  './js/data.js',
  './js/db.js',
  './js/config.js',
  './js/sync.js',
  './js/app.js',
  './manifest.json',
];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((cache) => cache.addAll(APP_SHELL)));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(
      keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))
    ))
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);
  if (url.origin !== self.location.origin) return; // let cross-origin pass through untouched

  event.respondWith(
    caches.match(event.request).then((cached) => {
      if (cached) return cached;
      return fetch(event.request).then((res) => {
        if (res.ok && event.request.method === 'GET') {
          const clone = res.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(event.request, clone));
        }
        return res;
      }).catch(() => cached);
    })
  );
});
