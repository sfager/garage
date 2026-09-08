// Story F-2: tapping the tab you are already on returns to the top of that section.
// A delegated listener rather than an @onclick handler: the router intercepts anchor
// clicks before Blazor's event delegation sees them, and scrolling needs no round trip.
document.addEventListener('click', (event) => {
    if (!event.target.closest('.tabbar a.active')) {
        return;
    }

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    window.scrollTo({ top: 0, behavior: reduceMotion ? 'auto' : 'smooth' });
});

window.garage = {
    // Story D-1: hand the document to the device's share sheet where there is one,
    // fall back to the clipboard, and finally to showing the link for manual copying.
    // Each outcome is reported so the button never looks like it did nothing.
    share: async (title, url) => {
        if (navigator.share) {
            try {
                await navigator.share({ title, url });
                return 'shared';
            } catch (error) {
                // Dismissing the sheet is a decision, not a failure.
                if (error && error.name === 'AbortError') {
                    return 'cancelled';
                }
                // Anything else falls through to the clipboard.
            }
        }

        try {
            await navigator.clipboard.writeText(url);
            return 'copied';
        } catch {
            return 'unavailable';
        }
    },

    // Story R-4: hands the generated CSV to the browser as a download.
    downloadCsv: (fileName, content) => {
        const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    },

    // Selects everything in the focused field, so a link can be copied in one gesture.
    selectAll: () => document.activeElement?.select?.(),

    // Brings a field into view and puts the cursor in it, for actions whose target
    // is already on screen — "＋ Reading" and the odometer entry, for instance.
    focusField: (id) => {
        const el = document.getElementById(id);
        if (!el) {
            return;
        }

        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        el.scrollIntoView({ block: 'center', behavior: reduceMotion ? 'auto' : 'smooth' });
        el.focus();
        el.select?.();
    }
};
