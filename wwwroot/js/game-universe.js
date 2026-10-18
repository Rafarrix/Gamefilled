(() => {
    const ensurePageStyles = () => {
        if (document.querySelector('link[data-game-page-tweaks]')) {
            return;
        }

        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = '/css/game-page-tweaks.css';
        link.dataset.gamePageTweaks = 'true';
        document.head.appendChild(link);
    };

    const init = () => {
        ensurePageStyles();

        const openButton = document.querySelector('[data-universe-open]');
        const drawer = document.querySelector('[data-universe-drawer]');
        const overlay = document.querySelector('[data-universe-overlay]');
        const closeButtons = document.querySelectorAll('[data-universe-close]');

        if (!openButton || !drawer || !overlay) {
            return;
        }

        let previouslyFocused = null;
        const focusableSelector = [
            'a[href]',
            'button:not([disabled])',
            '[tabindex]:not([tabindex="-1"])'
        ].join(',');

        const isOpen = () => document.body.classList.contains('gf-universe-open');

        const openDrawer = () => {
            previouslyFocused = document.activeElement;
            document.body.classList.add('gf-universe-open');
            drawer.setAttribute('aria-hidden', 'false');
            overlay.setAttribute('aria-hidden', 'false');
            openButton.setAttribute('aria-expanded', 'true');

            window.setTimeout(() => {
                drawer.querySelector(focusableSelector)?.focus();
            }, 20);
        };

        const closeDrawer = () => {
            document.body.classList.remove('gf-universe-open');
            drawer.setAttribute('aria-hidden', 'true');
            overlay.setAttribute('aria-hidden', 'true');
            openButton.setAttribute('aria-expanded', 'false');
            previouslyFocused?.focus?.();
        };

        openButton.addEventListener('click', openDrawer);
        overlay.addEventListener('click', closeDrawer);
        closeButtons.forEach(button => button.addEventListener('click', closeDrawer));

        document.addEventListener('keydown', event => {
            if (!isOpen()) return;

            if (event.key === 'Escape') {
                event.preventDefault();
                closeDrawer();
                return;
            }

            if (event.key !== 'Tab') return;

            const focusable = Array.from(drawer.querySelectorAll(focusableSelector))
                .filter(element => element.offsetParent !== null);

            if (focusable.length === 0) return;

            const first = focusable[0];
            const last = focusable[focusable.length - 1];

            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init, { once: true });
    } else {
        init();
    }
})();
