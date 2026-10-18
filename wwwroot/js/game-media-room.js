(() => {
    const init = () => {
        const room = document.querySelector('[data-media-room]');
        const lightbox = document.querySelector('[data-media-lightbox]');

        if (!room || !lightbox) {
            return;
        }

        const items = Array.from(room.querySelectorAll('[data-media-item]'));
        const stage = lightbox.querySelector('[data-media-stage]');
        const title = lightbox.querySelector('[data-media-title]');
        const counter = lightbox.querySelector('[data-media-counter]');
        const previousButton = lightbox.querySelector('[data-media-previous]');
        const nextButton = lightbox.querySelector('[data-media-next]');
        const closeButtons = lightbox.querySelectorAll('[data-media-close]');

        if (!stage || items.length === 0) {
            return;
        }

        let currentIndex = 0;
        let previouslyFocused = null;

        const clearStage = () => {
            stage.replaceChildren();
        };

        const render = index => {
            currentIndex = (index + items.length) % items.length;
            const item = items[currentIndex];
            const type = item.dataset.mediaType;
            const source = item.dataset.mediaSrc;
            const itemTitle = item.dataset.mediaTitle || 'Media';

            clearStage();

            if (type === 'video') {
                const iframe = document.createElement('iframe');
                iframe.src = source;
                iframe.title = itemTitle;
                iframe.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share';
                iframe.allowFullscreen = true;
                iframe.referrerPolicy = 'strict-origin-when-cross-origin';
                stage.appendChild(iframe);
            } else {
                const image = document.createElement('img');
                image.src = source;
                image.alt = itemTitle;
                stage.appendChild(image);
            }

            if (title) {
                title.textContent = itemTitle;
            }

            if (counter) {
                counter.textContent = `${currentIndex + 1} / ${items.length}`;
            }

            if (previousButton) previousButton.hidden = items.length < 2;
            if (nextButton) nextButton.hidden = items.length < 2;
        };

        const open = index => {
            previouslyFocused = document.activeElement;
            render(index);
            lightbox.hidden = false;
            lightbox.setAttribute('aria-hidden', 'false');
            document.body.classList.add('gf-media-lightbox-open');

            const closeButton = lightbox.querySelector('[data-media-close]');
            window.setTimeout(() => closeButton?.focus(), 20);
        };

        const close = () => {
            lightbox.hidden = true;
            lightbox.setAttribute('aria-hidden', 'true');
            document.body.classList.remove('gf-media-lightbox-open');
            clearStage();
            previouslyFocused?.focus?.();
        };

        items.forEach((item, index) => {
            item.addEventListener('click', () => open(index));
        });

        previousButton?.addEventListener('click', () => render(currentIndex - 1));
        nextButton?.addEventListener('click', () => render(currentIndex + 1));
        closeButtons.forEach(button => button.addEventListener('click', close));

        document.addEventListener('keydown', event => {
            if (lightbox.hidden) {
                return;
            }

            if (event.key === 'Escape') {
                event.preventDefault();
                close();
            } else if (event.key === 'ArrowLeft') {
                event.preventDefault();
                render(currentIndex - 1);
            } else if (event.key === 'ArrowRight') {
                event.preventDefault();
                render(currentIndex + 1);
            }
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init, { once: true });
    } else {
        init();
    }
})();
