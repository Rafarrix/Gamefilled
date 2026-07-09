(() => {
    const init = () => {
        const drawer = document.querySelector('[data-game-filter-drawer]');
        const overlay = document.querySelector('[data-game-filter-overlay]');
        const openButton = document.querySelector('[data-game-filter-open]');
        const closeButtons = document.querySelectorAll('[data-game-filter-close]');

        if (!drawer || !overlay || !openButton) {
            return;
        }

        let previouslyFocused = null;

        const focusableSelector = [
            'a[href]',
            'button:not([disabled])',
            'input:not([disabled])',
            'select:not([disabled])',
            'textarea:not([disabled])',
            '[tabindex]:not([tabindex="-1"])'
        ].join(',');

        const isOpen = () => document.body.classList.contains('gf-filter-drawer-open');

        const openDrawer = () => {
            previouslyFocused = document.activeElement;
            document.body.classList.add('gf-filter-drawer-open');
            drawer.setAttribute('aria-hidden', 'false');
            overlay.setAttribute('aria-hidden', 'false');
            openButton.setAttribute('aria-expanded', 'true');

            const firstFocusable = drawer.querySelector(focusableSelector);
            if (firstFocusable) {
                window.setTimeout(() => firstFocusable.focus(), 30);
            }
        };

        const closeDrawer = () => {
            document.body.classList.remove('gf-filter-drawer-open');
            drawer.setAttribute('aria-hidden', 'true');
            overlay.setAttribute('aria-hidden', 'true');
            openButton.setAttribute('aria-expanded', 'false');

            if (previouslyFocused && typeof previouslyFocused.focus === 'function') {
                previouslyFocused.focus();
            } else {
                openButton.focus();
            }
        };

        openButton.addEventListener('click', openDrawer);
        overlay.addEventListener('click', closeDrawer);
        closeButtons.forEach(button => button.addEventListener('click', closeDrawer));

        document.addEventListener('keydown', event => {
            if (!isOpen()) {
                return;
            }

            if (event.key === 'Escape') {
                event.preventDefault();
                closeDrawer();
                return;
            }

            if (event.key !== 'Tab') {
                return;
            }

            const focusable = Array.from(drawer.querySelectorAll(focusableSelector))
                .filter(element => element.offsetParent !== null);

            if (focusable.length === 0) {
                return;
            }

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
