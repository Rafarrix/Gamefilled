(() => {
    const init = () => {
        const profile = document.getElementById('profile-li');
        if (!profile || typeof window.jQuery === 'undefined') {
            return;
        }

        const $ = window.jQuery;
        const $profile = $(profile);
        const $menu = $profile.find('.gf-profile-menu');
        const $toggle = $profile.find('.dropdown-toggle');
        let closeTimer = null;

        // Remove o comportamento hover antigo definido em site.js.
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
