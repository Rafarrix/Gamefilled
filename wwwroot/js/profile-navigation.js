document.addEventListener('DOMContentLoaded', () => {
    // The notification inbox is not implemented yet. Do not expose a broken
    // destination in the global navigation; the bell returns with the real feature.
    document.getElementById('nav-notification-bell')?.closest('.nav-item')?.remove();

    const profileRoute = window.location.pathname.match(/^\/u\/([^/]+)\/?$/i);

    // Large social lists remain dedicated routes, but the profile content tabs
    // (Activity and Reviews) stay inside the profile shell.
    if (profileRoute) {
        const username = decodeURIComponent(profileRoute[1]);
        const base = `/u/${encodeURIComponent(username)}`;

        const dedicatedRoutes = {
            followers: 'followers',
            following: 'following'
        };

        document.querySelectorAll('a[href*="?tab="]').forEach(link => {
            let url;

            try {
                url = new URL(link.href, window.location.origin);
            } catch {
                return;
            }

            const tab = url.searchParams.get('tab')?.toLowerCase();

            // Lists does not have a real data model yet, so it is not exposed as
            // an active profile destination until the feature is implemented.
            if (tab === 'lists') {
                link.remove();
                return;
            }

            const route = tab ? dedicatedRoutes[tab] : null;
            if (!route) return;

            link.setAttribute('href', `${base}/${route}`);
            link.classList.add('gf-profile-direct-link');
        });
    }

    // Keep the user dropdown consistent on every page: Activity and Reviews
    // open the matching tab inside the profile instead of a parallel layout.
    document.querySelectorAll('.gf-profile-menu a[href]').forEach(link => {
        const href = link.getAttribute('href') || '';

        if (/\/u\/[^/]+\/(lists|journal)\/?$/i.test(href)) {
            link.remove();
            return;
        }

        const match = href.match(/^\/u\/([^/]+)\/(activity|reviews)\/?$/i);
        if (!match) return;

        const username = match[1];
        const tab = match[2].toLowerCase();
        link.setAttribute('href', `/u/${username}?tab=${tab}`);
    });
});