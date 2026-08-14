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
