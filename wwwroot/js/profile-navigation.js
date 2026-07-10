document.addEventListener('DOMContentLoaded', () => {
    const match = window.location.pathname.match(/^\/u\/([^/]+)\/?$/i);
    if (!match) return;

    const username = decodeURIComponent(match[1]);
    const base = `/u/${encodeURIComponent(username)}`;

    document.querySelectorAll('a[href*="?tab=followers"]').forEach(link => {
        link.setAttribute('href', `${base}/followers`);
        link.classList.add('gf-profile-direct-link');
    });

    document.querySelectorAll('a[href*="?tab=following"]').forEach(link => {
        link.setAttribute('href', `${base}/following`);
        link.classList.add('gf-profile-direct-link');
    });
});
