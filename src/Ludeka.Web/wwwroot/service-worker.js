// Service Worker de Ludeka — PWA & Modo Offline
const CACHE_NAME = 'ludeka-cache-v1';

const PRECACHE_ASSETS = [
    '/',
    '/app.css',
    '/offline.html',
    '/manifest.webmanifest',
    '/favicon.ico',
    '/favicon.svg',
    '/favicon-32x32.png',
    '/apple-touch-icon.png',
    '/images/game-placeholder.svg',
    '/images/expansion-placeholder.svg',
    '/images/logo.png',
    '/icons/icon-192.svg',
    '/icons/icon-512.svg',
    '/icons/icon-maskable.svg',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/js/ludeka-offline.js'
];

// 1. Instalación y Pre-cacheo de recursos esenciales
self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                return cache.addAll(PRECACHE_ASSETS);
            })
            .then(() => self.skipWaiting())
    );
});

// 2. Activación y purga de versiones antiguas de caché
self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(cacheNames => {
                return Promise.all(
                    cacheNames
                        .filter(name => name !== CACHE_NAME)
                        .map(name => caches.delete(name))
                );
            })
            .then(() => self.clients.claim())
    );
});

// 3. Intercepción inteligente de peticiones
self.addEventListener('fetch', event => {
    const request = event.request;

    // Ignorar peticiones no-GET o esquemas no HTTP/HTTPS
    if (request.method !== 'GET' || !request.url.startsWith('http')) {
        return;
    }

    const url = new URL(request.url);

    // No interceptar conexiones de SignalR o WebSockets de Blazor (_blazor)
    if (url.pathname.startsWith('/_blazor')) {
        return;
    }

    // A. Peticiones de Navegación HTML (Páginas): Network-First con Fallback Offline
    if (request.mode === 'navigate') {
        event.respondWith(
            fetch(request)
                .then(networkResponse => {
                    if (networkResponse && networkResponse.status === 200) {
                        const clone = networkResponse.clone();
                        caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
                    }
                    return networkResponse;
                })
                .catch(async () => {
                    // Si falla la red, intentar responder desde la caché para esta ruta
                    const cachedResponse = await caches.match(request);
                    if (cachedResponse) {
                        return cachedResponse;
                    }
                    // Fallback a la pantalla de cortesía offline
                    const fallback = await caches.match('/offline.html');
                    return fallback || new Response('Sin conexión a internet', {
                        status: 503,
                        headers: { 'Content-Type': 'text/plain; charset=utf-8' }
                    });
                })
        );
        return;
    }

    // B. Imágenes: Cache-First con fallback a placeholder si no hay red
    if (request.destination === 'image' || url.pathname.match(/\.(png|jpg|jpeg|svg|webp|gif|ico)$/i)) {
        event.respondWith(
            caches.match(request).then(cachedResponse => {
                if (cachedResponse) {
                    return cachedResponse;
                }
                return fetch(request)
                    .then(networkResponse => {
                        if (networkResponse && networkResponse.status === 200) {
                            const clone = networkResponse.clone();
                            caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
                        }
                        return networkResponse;
                    })
                    .catch(() => {
                        // Fallback a placeholder de juego si la imagen no está disponible
                        return caches.match('/images/game-placeholder.svg');
                    });
            })
        );
        return;
    }

    // C. Recursos estáticos (CSS, JS, Fuentes): Cache-First
    event.respondWith(
        caches.match(request).then(cachedResponse => {
            if (cachedResponse) {
                return cachedResponse;
            }
            return fetch(request).then(networkResponse => {
                if (networkResponse && networkResponse.status === 200) {
                    const clone = networkResponse.clone();
                    caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
                }
                return networkResponse;
            });
        })
    );
});
