document.addEventListener('DOMContentLoaded', () => {
    const match = window.location.pathname.match(/^\/u\/([^/]+)\/?$/i);
    if (!match) return;

    const username = decodeURIComponent(match[1]);
    const base = `/u/${encodeURIComponent(username)}`;
    const routes = {
        followers: 'followers',
        following: 'following',
        activity: 'activity',
        reviews: 'reviews'
    };

    document.querySelectorAll('a[href*="?tab="]').forEach(link => {
        let url;

        try {
            url = new URL(link.href, window.location.origin);
        } catch {
            return;
        }

        const tab = url.searchParams.get('tab')?.toLowerCase();
        const route = tab ? routes[tab] : null;
        if (!route) return;

        link.setAttribute('href', `${base}/${route}`);
        link.classList.add('gf-profile-direct-link');
    });
});
