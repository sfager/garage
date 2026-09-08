// Story S-5's receiving end. The service worker runs whether or not the app is open,
// which is the whole point — the user is told without having to remember to look.

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

self.addEventListener('push', (event) => {
    let payload = { title: 'Garage', body: 'Something needs attention.', url: '/', tag: 'garage' };

    try {
        if (event.data) {
            payload = { ...payload, ...event.data.json() };
        }
    } catch {
        // A payload we cannot parse still deserves a notification, just a generic one.
    }

    event.waitUntil(self.registration.showNotification(payload.title, {
        body: payload.body,
        // The tag collapses repeats of the same subject rather than stacking them.
        tag: payload.tag,
        icon: '/favicon.png',
        badge: '/favicon.png',
        data: { url: payload.url }
    }));
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const target = event.notification.data?.url || '/';

    // Focus a tab that is already open rather than piling up new ones.
    event.waitUntil((async () => {
        const clientList = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });

        for (const client of clientList) {
            if ('focus' in client) {
                await client.focus();
                if ('navigate' in client) {
                    await client.navigate(target);
                }
                return;
            }
        }

        await self.clients.openWindow(target);
    })());
});
