// Story V-3: read the VIN off the door jamb instead of typing seventeen characters.
//
// VIN plates carry the number as a Code 39 / Code 128 barcode, which BarcodeDetector can
// read natively where it exists (Chrome and Android). There is deliberately no OCR
// fallback: a wrong VIN read confidently is worse than no VIN, and the manual field is
// always right there.
window.garageVin = {
    _stream: null,
    _timer: null,

    isSupported: () => 'BarcodeDetector' in window && !!navigator.mediaDevices?.getUserMedia,

    /// Starts the camera and polls for a barcode. Resolves through the .NET callback.
    start: async (videoId, dotNetRef) => {
        const video = document.getElementById(videoId);
        if (!video) {
            return 'no-video';
        }

        if (!navigator.mediaDevices?.getUserMedia) {
            return 'unsupported';
        }

        try {
            window.garageVin._stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: 'environment' }
            });
        } catch (error) {
            // Denied, or no camera on this device.
            return error && error.name === 'NotAllowedError' ? 'denied' : 'unavailable';
        }

        video.srcObject = window.garageVin._stream;
        await video.play().catch(() => { });

        if (!('BarcodeDetector' in window)) {
            // The camera is up so the user can still see what they are pointing at, but
            // nothing will be read automatically.
            return 'no-detector';
        }

        const detector = new BarcodeDetector({
            formats: ['code_39', 'code_128', 'qr_code', 'data_matrix']
        });

        window.garageVin._timer = setInterval(async () => {
            try {
                const found = await detector.detect(video);
                if (found.length === 0) {
                    return;
                }

                const value = (found[0].rawValue || '').trim().toUpperCase();
                if (value.length >= 17) {
                    // VIN barcodes are sometimes prefixed with an "I" issuer character.
                    await dotNetRef.invokeMethodAsync('OnVinScanned', value.slice(-17));
                }
            } catch {
                // A frame that cannot be decoded is the normal case, not an error.
            }
        }, 400);

        return 'scanning';
    },

    stop: () => {
        if (window.garageVin._timer) {
            clearInterval(window.garageVin._timer);
            window.garageVin._timer = null;
        }

        if (window.garageVin._stream) {
            window.garageVin._stream.getTracks().forEach(track => track.stop());
            window.garageVin._stream = null;
        }
    }
};
