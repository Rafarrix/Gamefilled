(() => {
    const loadSearchEnhancement = () => {
        if (document.querySelector('script[data-gf-header-search]')) return;

        const script = document.createElement('script');
        script.src = '/js/header-search-v2.js';
        script.defer = true;
        script.dataset.gfHeaderSearch = 'true';
        document.head.appendChild(script);
    };

    const trimProfileHeroStats = () => {
        const heroStats = document.querySelector('.gf-profile-v2__hero-stats');
        if (!heroStats) return;

        [...heroStats.children].slice(2).forEach(item => item.remove());
    };

    const init = () => {
        loadSearchEnhancement();
        trimProfileHeroStats();

        const profile = document.getElementById('profile-li');
        if (!profile || typeof window.jQuery === 'undefined') {
            return;
        }

        const $ = window.jQuery;
        const $profile = $(profile);
        const $menu = $profile.find('.gf-profile-menu');
        const $toggle = $profile.find('.dropdown-toggle');
        const menuElement = $menu.get(0);
        let closeTimer = null;

        // Removes the legacy hover behavior registered in site.js.
        $profile.off('.profileHover');

        const isDesktop = () => window.matchMedia('(min-width: 992px)').matches;

        const openMenu = () => {
            if (!isDesktop()) return;

            window.clearTimeout(closeTimer);
            $profile.addClass('show');
            $menu.addClass('show');
            $toggle.attr('aria-expanded', 'true');
        };

        const closeMenu = () => {
            if (!isDesktop()) return;

            window.clearTimeout(closeTimer);
            closeTimer = window.setTimeout(() => {
                if ($profile.is(':hover') || $menu.is(':hover')) {
                    return;
                }

                $profile.removeClass('show');
                $menu.removeClass('show');
                $toggle.attr('aria-expanded', 'false');
            }, 220);
        };

        $profile.on('mouseenter.headerV2 focusin.headerV2', openMenu);
        $profile.on('mouseleave.headerV2 focusout.headerV2', closeMenu);
        $menu.on('mouseenter.headerV2', openMenu);
        $menu.on('mouseleave.headerV2', closeMenu);

        if (menuElement) {
            menuElement.addEventListener('pointermove', event => {
                const bounds = menuElement.getBoundingClientRect();
                const x = Math.max(0, Math.min(bounds.width, event.clientX - bounds.left));
                const y = Math.max(0, Math.min(bounds.height, event.clientY - bounds.top));

                menuElement.style.setProperty('--gf-menu-x', `${x}px`);
                menuElement.style.setProperty('--gf-menu-y', `${y}px`);
            });

            menuElement.addEventListener('pointerleave', () => {
                menuElement.style.setProperty('--gf-menu-x', '50%');
                menuElement.style.setProperty('--gf-menu-y', '0%');
            });
        }

        $(window).on('resize.headerV2', () => {
            window.clearTimeout(closeTimer);

            if (!isDesktop()) {
                $profile.removeClass('show');
                $menu.removeClass('show');
                $toggle.attr('aria-expanded', 'false');
            }
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init, { once: true });
    } else {
        init();
    }
})();