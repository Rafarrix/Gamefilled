document.addEventListener('DOMContentLoaded', () => {
    const bell = document.getElementById('nav-notification-bell');
    const bellItem = bell?.closest('.nav-item');
    const indicator = bell?.querySelector('.notification-indicator');
    const menu = document.getElementById('notification-menu');
    const menuList = document.getElementById('notification-menu-list');
    const menuSummary = document.getElementById('notification-menu-summary');

    const relativeTime = value => {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return '';

        const seconds = Math.max(0, Math.floor((Date.now() - date.getTime()) / 1000));
        if (seconds < 60) return 'Just now';
        if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
        if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
        if (seconds < 604800) return `${Math.floor(seconds / 86400)}d ago`;

        return date.toLocaleDateString(undefined, {
            month: 'short',
            day: 'numeric',
            year: date.getFullYear() === new Date().getFullYear() ? undefined : 'numeric'
        });
    };

    const setUnreadCount = count => {
        if (!bell || !indicator) return;

        const unread = Number(count) || 0;
        if (unread <= 0) {
            indicator.hidden = true;
            indicator.textContent = '';
            bell.setAttribute('aria-label', 'Notifications');
            return;
        }

        indicator.hidden = false;
        indicator.textContent = unread > 99 ? '99+' : String(unread);
        bell.setAttribute('aria-label', `Notifications, ${unread} unread`);
    };

    const renderState = (title, message, icon = 'fa-bell-slash') => {
        if (!menuList) return;

        menuList.replaceChildren();
        const state = document.createElement('div');
        state.className = 'gf-notification-menu__state';

        const stateIcon = document.createElement('i');
        stateIcon.className = `fas ${icon}`;
        stateIcon.setAttribute('aria-hidden', 'true');

        const strong = document.createElement('strong');
        strong.textContent = title;

        const span = document.createElement('span');
        span.textContent = message;

        state.append(stateIcon, strong, span);
        menuList.appendChild(state);
    };

    const renderPreview = data => {
        if (!menuList || !menuSummary) return;

        const items = Array.isArray(data?.items) ? data.items : [];
        const unread = Number(data?.count) || 0;
        setUnreadCount(unread);
        menuSummary.textContent = unread > 0
            ? `${unread} unread update${unread === 1 ? '' : 's'}`
            : 'You are all caught up';

        if (items.length === 0) {
            renderState(
                'No notifications yet',
                'New followers and direct interactions will appear here.'
            );
            return;
        }

        menuList.replaceChildren();

        items.forEach(item => {
            const link = document.createElement('a');
            link.className = `gf-notification-preview${item.isRead ? '' : ' is-unread'}`;
            link.href = item.openUrl || '/notifications';

            const avatarWrap = document.createElement('span');
            avatarWrap.className = 'gf-notification-preview__avatar';

            const avatar = document.createElement('img');
            avatar.src = item.avatarUrl || '/lib/imgs/defaultavatar.png';
            avatar.alt = `${item.actorName || 'Player'} avatar`;
            avatar.loading = 'lazy';
            avatarWrap.appendChild(avatar);

            const copy = document.createElement('span');
            copy.className = 'gf-notification-preview__copy';

            const title = document.createElement('strong');
            title.textContent = item.title || 'Notification';

            const message = document.createElement('span');
            message.textContent = item.message || '';

            const time = document.createElement('small');
            time.textContent = relativeTime(item.createdAt);

            copy.append(title, message, time);
            link.append(avatarWrap, copy);

            if (!item.isRead) {
                const dot = document.createElement('span');
                dot.className = 'gf-notification-preview__dot';
                dot.setAttribute('aria-label', 'Unread');
                link.appendChild(dot);
            } else {
                const arrow = document.createElement('i');
                arrow.className = 'fas fa-arrow-right';
                arrow.setAttribute('aria-hidden', 'true');
                link.appendChild(arrow);
            }

            menuList.appendChild(link);
        });
    };

    const closeNotificationMenu = () => {
        if (!bell || !menu || menu.hidden) return;
        menu.hidden = true;
        bell.setAttribute('aria-expanded', 'false');
    };

    const openNotificationMenu = () => {
        if (!bell || !menu) return;
        menu.hidden = false;
        bell.setAttribute('aria-expanded', 'true');
        menu.querySelector('a, button')?.focus({ preventScroll: true });
    };

    if (bell && bellItem && indicator && menu && menuList) {
        setUnreadCount(0);

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
                    throw new Error('Notification preview unavailable.');

                return response.json();
            })
            .then(data => {
                if (data) renderPreview(data);
            })
            .catch(() => {
                setUnreadCount(0);
                if (menuSummary) menuSummary.textContent = 'Preview unavailable';
                renderState(
                    'Could not load updates',
                    'Open the full inbox to try again.',
                    'fa-triangle-exclamation'
                );
            });

        bell.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();

            if (menu.hidden) openNotificationMenu();
            else closeNotificationMenu();
        });

        menu.addEventListener('click', event => event.stopPropagation());
        menu.addEventListener('mousemove', event => {
            const bounds = menu.getBoundingClientRect();
            menu.style.setProperty('--gf-notification-x', `${event.clientX - bounds.left}px`);
            menu.style.setProperty('--gf-notification-y', `${event.clientY - bounds.top}px`);
        });
        menu.addEventListener('mouseleave', () => {
            menu.style.setProperty('--gf-notification-x', '50%');
            menu.style.setProperty('--gf-notification-y', '0%');
        });

        document.addEventListener('click', closeNotificationMenu);
        document.addEventListener('keydown', event => {
            if (event.key !== 'Escape' || menu.hidden) return;
            closeNotificationMenu();
            bell.focus();
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
