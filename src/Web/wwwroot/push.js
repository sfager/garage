// Story S-5, browser side: register the service worker, ask permission once, and hand
// the resulting subscription back to the server.
window.garagePush = {
    isSupported: () => 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window,

    permission: () => (('Notification' in window) ? Notification.permission : 'unsupported'),

    /// Returns the current subscription's endpoint, or null if this browser has none.
    currentEndpoint: async () => {
        if (!window.garagePush.isSupported()) {
            return null;
        }

        const registration = await navigator.serviceWorker.getRegistration('/');
        const subscription = await registration?.pushManager.getSubscription();
        return subscription?.endpoint ?? null;
    },

    /// Subscribes this browser. Returns the subscription for the server to store, or a
    /// status string explaining why it could not.
    subscribe: async (publicKey) => {
        if (!window.garagePush.isSupported()) {
            return { status: 'unsupported' };
        }

        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
            return { status: permission === 'denied' ? 'denied' : 'dismissed' };
        }

        try {
            const registration = await navigator.serviceWorker.register('/sw.js', { scope: '/' });
            await navigator.serviceWorker.ready;

            // Reuse an existing subscription so the server does not accumulate duplicates.
            let subscription = await registration.pushManager.getSubscription();
            if (!subscription) {
                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: window.garagePush._urlBase64ToUint8Array(publicKey)
                });
            }

            const raw = subscription.toJSON();
            return {
                status: 'subscribed',
                endpoint: raw.endpoint,
                p256dh: raw.keys.p256dh,
                auth: raw.keys.auth
            };
        } catch (error) {
            return { status: 'failed', message: String(error && error.message ? error.message : error) };
        }
    },

    unsubscribe: async () => {
        const registration = await navigator.serviceWorker.getRegistration('/');
        const subscription = await registration?.pushManager.getSubscription();

        if (!subscription) {
            return null;
        }

        const endpoint = subscription.endpoint;
        await subscription.unsubscribe();
        return endpoint;
    },

    /// Shows a local notification, to prove the permission and the worker are live
    /// without waiting for the server's next sweep.
    testLocal: async (title, body) => {
        const registration = await navigator.serviceWorker.getRegistration('/');
        if (!registration) {
            return false;
        }

        await registration.showNotification(title, { body, icon: '/favicon.png', tag: 'garage-test' });
        return true;
    },

    /// The VAPID public key travels as base64url; PushManager wants raw bytes.
    _urlBase64ToUint8Array: (base64) => {
        const padding = '='.repeat((4 - (base64.length % 4)) % 4);
        const normalised = (base64 + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = window.atob(normalised);
        return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
    }
};
