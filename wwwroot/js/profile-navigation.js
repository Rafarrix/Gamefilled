document.addEventListener('DOMContentLoaded', () => {
    const bell = document.getElementById('nav-notification-bell');
    const bellItem = bell?.closest('.nav-item');
    const indicator = bell?.querySelector('.notification-indicator');

    if (bell && bellItem && indicator) {
        fetch('/api/notifications/unread', {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' }
        })
            .then(response => {
                if (response.status === 401) {
                    bellItem.remove();
                    return null;
                }

                if (!response.ok)
                    throw new Error('Notification count unavailable.');

                return response.json();
            })
            .then(data => {
                if (!data) return;

                const count = Number(data.count) || 0;
                if (count <= 0) {
                    indicator.hidden = true;
                    indicator.textContent = '';
                    bell.setAttribute('aria-label', 'Notifications');
                    return;
                }

                indicator.hidden = false;
                indicator.textContent = count > 99 ? '99+' : String(count);
                bell.setAttribute('aria-label', `Notifications, ${count} unread`);
            })
            .catch(() => {
                indicator.hidden = true;
                indicator.textContent = '';
            });
    }

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
