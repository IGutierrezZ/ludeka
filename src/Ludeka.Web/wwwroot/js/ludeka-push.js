// Interop de Notificaciones Web Push para PWA Móvil y Navegador — Ludeka (INC-149)
(function () {
    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
        const base64 = (base64String + padding)
            .replace(/-/g, '+')
            .replace(/_/g, '/');

        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);

        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }

    function arrayBufferToBase64Url(buffer) {
        if (!buffer) return '';
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary)
            .replace(/\+/g, '-')
            .replace(/\//g, '_')
            .replace(/=+$/, '');
    }

    window.ludekaPush = {
        isSupported: function () {
            return ('serviceWorker' in navigator) && ('PushManager' in window) && ('Notification' in window);
        },

        getPermissionState: function () {
            if (!this.isSupported()) {
                return 'unsupported';
            }
            return Notification.permission; // 'default', 'granted', 'denied'
        },

        getExistingEndpoint: async function () {
            if (!this.isSupported()) return null;
            try {
                const registration = await navigator.serviceWorker.ready;
                const subscription = await registration.pushManager.getSubscription();
                return subscription ? subscription.endpoint : null;
            } catch {
                return null;
            }
        },

        subscribePush: async function (vapidPublicKey) {
            if (!this.isSupported()) {
                throw new Error('Este navegador o dispositivo no soporta notificaciones Web Push.');
            }

            const permission = await Notification.requestPermission();
            if (permission !== 'granted') {
                throw new Error('Permiso de notificaciones denegado por el navegador.');
            }

            const registration = await navigator.serviceWorker.ready;
            let subscription = await registration.pushManager.getSubscription();

            if (!subscription) {
                const convertedVapidKey = urlBase64ToUint8Array(vapidPublicKey);
                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: convertedVapidKey
                });
            }

            const p256dh = arrayBufferToBase64Url(subscription.getKey('p256dh'));
            const auth = arrayBufferToBase64Url(subscription.getKey('auth'));

            return {
                endpoint: subscription.endpoint,
                p256dh: p256dh,
                auth: auth,
                userAgent: navigator.userAgent || 'Navegador'
            };
        },

        unsubscribePush: async function () {
            if (!this.isSupported()) return null;
            try {
                const registration = await navigator.serviceWorker.ready;
                const subscription = await registration.pushManager.getSubscription();
                if (subscription) {
                    const endpoint = subscription.endpoint;
                    await subscription.unsubscribe();
                    return endpoint;
                }
            } catch (e) {
                console.warn('[WebPush] Error al cancelar suscripción:', e);
            }
            return null;
        }
    };
})();
