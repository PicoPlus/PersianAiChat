/**
 * service-worker.js — PWA Service Worker
 * Caches app shell only. Never caches authenticated API responses.
 */

const CACHE_VERSION = 'v1';
const STATIC_CACHE = `persian-ai-chat-static-${CACHE_VERSION}`;

const PRECACHE_URLS = [
  '/',
  '/index.html',
  '/css/app.css',
  '/js/utils.js',
  '/js/auth.js',
  '/js/chat.js',
  '/js/app.js',
  '/manifest.webmanifest'
];

// ── Install: pre-cache shell assets ───────────────────────────
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(STATIC_CACHE)
      .then(cache => cache.addAll(PRECACHE_URLS))
      .then(() => self.skipWaiting())
  );
});

// ── Activate: purge old caches ────────────────────────────────
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(
        keys
          .filter(k => k !== STATIC_CACHE)
          .map(k => caches.delete(k))
      ))
      .then(() => self.clients.claim())
  );
});

// ── Fetch: serve shell from cache; NEVER cache API or chat ────
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // NEVER cache API endpoints
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(fetch(event.request));
    return;
  }

  // Navigation requests — serve index.html from cache (offline shell)
  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request)
        .catch(() => caches.match('/index.html'))
    );
    return;
  }

  // Static assets — cache first
  event.respondWith(
    caches.match(event.request)
      .then(cached => cached || fetch(event.request)
        .then(response => {
          // Only cache same-origin static
          if (response.ok && url.origin === self.location.origin) {
            const clone = response.clone();
            caches.open(STATIC_CACHE).then(c => c.put(event.request, clone));
          }
          return response;
        })
      )
  );
});
