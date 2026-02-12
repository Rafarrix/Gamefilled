/* ============================================================
   site.js
   - Header: dropdown hover (perfil)
   - Header: autocomplete IGDB
   - Mostra: "Nome (Ano)"
   - Enter sem seleção -> abre /search (submit do form)
   - Enter com seleção -> abre o jogo selecionado (/games/{id:int})
   - ✅ NOVO: Header loading bar (durante fetch IGDB e navegação interna)
   ============================================================ */


/* ============================================================
   PARTE 1 — DROPDOWN DO PERFIL (HOVER DESKTOP)
   ============================================================ */
(function ($) {
    "use strict";

    // Se o jQuery não existir, não quebra o resto do JS
    if (typeof $ === "undefined") {
        console.warn("jQuery não encontrado — dropdown do perfil desativado.");
        return;
    }

    $(function () {

        function enableHoverDropdown() {
            var $profile = $('#profile-li');

            // limpa handlers antigos (evita duplicar eventos)
            $profile.off('mouseenter.profileHover mouseleave.profileHover');

            // só no desktop
            if (window.matchMedia("(min-width: 768px)").matches) {
                $profile.on('mouseenter.profileHover', function () {
                    $(this).addClass('show');
                    $(this).find('.dropdown-menu').addClass('show');
                    $(this).find('.dropdown-toggle').attr('aria-expanded', 'true');
                });

                $profile.on('mouseleave.profileHover', function () {
                    $(this).removeClass('show');
                    $(this).find('.dropdown-menu').removeClass('show');
                    $(this).find('.dropdown-toggle').attr('aria-expanded', 'false');
                });
            }
        }

        enableHoverDropdown();

        // re-aplica ao redimensionar
        var resizeTimer = null;
        $(window).on('resize', function () {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(enableHoverDropdown, 150);
        });
    });

}(jQuery));


/* ============================================================
   HEADER LOADER — API simples
   - Mostra/Esconde a linha de loading por baixo do header
   - Usa um contador para aguentar vários requests ao mesmo tempo
   ============================================================ */
(function () {
    const loader = document.getElementById("header-loader");
    if (!loader) return;

    let activeRequests = 0;

    window.HeaderLoader = {
        start() {
            activeRequests++;
            loader.classList.add("active");
        },

        stop() {
            activeRequests = Math.max(0, activeRequests - 1);

            // só esconde quando já não há requests
            if (activeRequests === 0) {
                setTimeout(() => {
                    loader.classList.remove("active");
                }, 200);
            }
        }
    };
})();


/* ============================================================
   PARTE 2 — AUTOCOMPLETE IGDB (HEADER SEARCH)
   ============================================================ */
(function () {

    /* ---------------------------
       Config
    ---------------------------- */
    const DEBUG = true;
    const MAX_RESULTS = 25;

    function log(...a) { if (DEBUG) console.log(...a); }
    function warn(...a) { if (DEBUG) console.warn(...a); }
    function error(...a) { if (DEBUG) console.error(...a); }

    /* ---------------------------
       Helpers
    ---------------------------- */
    function debounce(fn, ms) {
        let t = null;
        return (...args) => {
            clearTimeout(t);
            t = setTimeout(() => fn.apply(null, args), ms);
        };
    }

    function qs(sel, root = document) { return root.querySelector(sel); }
    function qsa(sel, root = document) { return [...root.querySelectorAll(sel)]; }
    function isVisible(el) { return el && !el.classList.contains("d-none"); }

    function escapeHtml(s) {
        return String(s ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    // UNIX (segundos) -> ano
    function unixToYear(unix) {
        if (!unix || typeof unix !== "number") return "";
        try {
            const y = new Date(unix * 1000).getFullYear();
            return Number.isFinite(y) ? String(y) : "";
        } catch {
            return "";
        }
    }

    // score simples: jogo base (category 0) + texto + popularidade
    const MAIN_GAME = 0;

    function titleScore(term, name) {
        const t = (term || "").trim().toLowerCase();
        const n = (name || "").trim().toLowerCase();
        if (!t || !n) return 0;

        let score = 0;

        if (n === t) score += 1200;
        if (n.startsWith(t)) score += 800;
        if (n.includes(t)) score += 250;

        // penaliza coisas típicas (mas não remove)
        const bad = ["season", "chapter", "episode", "dlc", "demo", "trial", "pack", "bundle", "expansion"];
        for (const b of bad) if (n.includes(b)) score -= 350;

        return score;
    }

    function popularityScore(g) {
        const follows = Number(g?.follows ?? 0);
        const ratings = Number(g?.totalRatingCount ?? 0);
        return Math.log10(follows + 1) * 350 + Math.log10(ratings + 1) * 180;
    }

    function baseGameBoost(g) {
        return (g?.category === MAIN_GAME) ? 2500 : 0;
    }

    /* ----------------------------
       DOM
    ----------------------------- */
    const input = qs("#nav-bar-search");
    const wrap = qs("#nav-search-wrap");
    const dropdown = qs("#igdb-dropdown");

    // se não existir, saímos sem rebentar nada
    if (!input || !wrap || !dropdown) return;

    /* ----------------------------
       State
    ----------------------------- */
    let items = [];
    let activeIndex = -1;
    let lastTerm = "";
    let abortController = null;

    function openDropdown() {
        dropdown.classList.remove("d-none");
    }

    function closeDropdown() {
        dropdown.classList.add("d-none");
        dropdown.innerHTML = "";
        items = [];
        activeIndex = -1;
    }

    function setActive(index) {
        activeIndex = index;

        const els = qsa(".igdb-item", dropdown);
        els.forEach((el, i) => el.classList.toggle("active", i === index));

        if (els[index]) {
            els[index].scrollIntoView({ block: "nearest" });
        }
    }

    /* ----------------------------
       Render
    ----------------------------- */
    function renderDropdown(term, results) {
        items = results || [];
        activeIndex = -1;

        if (!items.length) {
            dropdown.innerHTML = `
                <div class="igdb-footer">
                    Sem resultados para "<strong>${escapeHtml(term)}</strong>".
                </div>`;
            openDropdown();
            return;
        }

        const htmlItems = items.map((g, idx) => {
            const name = g.name || "Untitled";
            const year = unixToYear(g.bestReleaseDate ?? g.firstReleaseDate);

            // Label: Nome (Ano)
            const label = year
                ? `${escapeHtml(name)} (${escapeHtml(year)})`
                : `${escapeHtml(name)}`;

            // ✅ O TEU SITE USA /games/{id:int}
            // Logo: temos de ir por ID NUMÉRICO, nunca por slug.
            const id = Number(g.id);
            const href = Number.isFinite(id) && id > 0 ? `/games/${id}` : "#";

            return `
                <a class="igdb-item" role="option" data-idx="${idx}" href="${href}">
                    <div class="igdb-meta">
                        <p class="igdb-title">${label}</p>
                    </div>
                </a>`;
        }).join("");

        dropdown.innerHTML = `
            ${htmlItems}
            <div class="igdb-footer">Enter para abrir · Esc para fechar</div>
        `;

        openDropdown();
    }

    /* ----------------------------
       Fetch (✅ COM LOADER)
    ----------------------------- */
    async function fetchResults(term) {
        term = (term || "").trim();

        // mínimo de 2 chars
        if (term.length < 2) {
            closeDropdown();
            return;
        }

        // evita re-fetch se não mudou e dropdown já está aberto
        if (term === lastTerm && isVisible(dropdown)) return;
        lastTerm = term;

        // cancela pedidos anteriores
        if (abortController) abortController.abort();
        abortController = new AbortController();

        try {
            // ✅ 4) Mostrar loading bar durante o fetch do autocomplete
            window.HeaderLoader?.start();

            const url = `/api/igdbsearch?term=${encodeURIComponent(term)}`;
            log("IGDB fetch ->", url);

            const res = await fetch(url, {
                signal: abortController.signal,
                headers: { "Accept": "application/json" }
            });

            if (!res.ok) throw new Error(`HTTP ${res.status}`);

            const data = await res.json();

            // aceita: { responseText: "..." } OU array OU { results: [...] }
            let gamesArray = [];

            if (data && typeof data.responseText === "string") {
                try { gamesArray = JSON.parse(data.responseText); }
                catch (e) { error("Falha parse responseText", e); gamesArray = []; }
            }
            else if (Array.isArray(data)) {
                gamesArray = data;
            }
            else if (data && Array.isArray(data.results)) {
                gamesArray = data.results;
            }

            // Normaliza nomes de campos (IGDB + DTOs C#)
            let normalized = gamesArray.map(x => ({
                id: x.id ?? x.Id,
                name: x.name ?? x.Name,
                slug: x.slug ?? x.Slug,

                category: x.category ?? x.Category ?? null,

                firstReleaseDate:
                    x.first_release_date ??
                    x.firstReleaseDate ??
                    x.FirstReleaseDate ??
                    null,

                bestReleaseDate:
                    x.bestReleaseDate ??
                    x.best_release_date ??
                    x.BestReleaseDate ??
                    null,

                follows: x.follows ?? x.Follows ?? 0,

                totalRatingCount:
                    x.total_rating_count ??
                    x.totalRatingCount ??
                    x.TotalRatingCount ??
                    0
            }));

            const shortTerm = term.length <= 4;

            // ordenação: jogo base + match de título + popularidade
            normalized.sort((a, b) => {
                const aScore =
                    baseGameBoost(a) +
                    titleScore(term, a.name) +
                    (shortTerm ? popularityScore(a) : popularityScore(a) * 0.6);

                const bScore =
                    baseGameBoost(b) +
                    titleScore(term, b.name) +
                    (shortTerm ? popularityScore(b) : popularityScore(b) * 0.6);

                return bScore - aScore;
            });

            normalized = normalized.slice(0, MAX_RESULTS);

            renderDropdown(term, normalized);
        } catch (err) {
            if (err && err.name === "AbortError") return;

            warn("Erro IGDB:", err);
            dropdown.innerHTML = `<div class="igdb-footer">Erro a carregar resultados.</div>`;
            openDropdown();
        } finally {
            // ✅ 4) Esconder loading bar quando termina (ok/erro)
            window.HeaderLoader?.stop();
        }
    }

    const fetchDebounced = debounce(fetchResults, 200);

    /* ----------------------------
       Eventos do input
    ----------------------------- */
    input.addEventListener("input", (e) => fetchDebounced(e.target.value));

    input.addEventListener("focus", () => {
        const term = input.value.trim();
        if (term.length >= 2 && !isVisible(dropdown)) fetchDebounced(term);
    });

    // ENTER:
    // - se houver item ativo: abre o jogo (via href)
    // - se não houver item ativo: deixa o submit normal do form (abre /search)
    input.addEventListener("keydown", (e) => {
        if (!isVisible(dropdown)) return;

        const els = qsa(".igdb-item", dropdown);
        if (!els.length) return;

        if (e.key === "ArrowDown") {
            e.preventDefault();
            setActive(Math.min(activeIndex + 1, els.length - 1));
        }
        else if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive(Math.max(activeIndex - 1, 0));
        }
        else if (e.key === "Enter") {
            if (activeIndex >= 0 && els[activeIndex]) {
                // se o item tiver href "#", não faz nada
                const href = els[activeIndex].getAttribute("href") || "#";
                if (href !== "#") {
                    e.preventDefault();

                    // ✅ 5) Ao navegar, mostra loading bar imediatamente
                    window.HeaderLoader?.start();

                    window.location.href = href; // navega direto
                }
            }
            // ✅ sem item ativo: NÃO bloqueia -> o browser faz submit do form (/search)
        }
        else if (e.key === "Escape") {
            e.preventDefault();
            closeDropdown();
        }
    });

    /* ----------------------------
       Mouse + clique fora
    ----------------------------- */
    dropdown.addEventListener("mousemove", (e) => {
        const a = e.target.closest(".igdb-item");
        if (!a) return;
        const idx = parseInt(a.getAttribute("data-idx"), 10);
        if (!Number.isNaN(idx)) setActive(idx);
    });

    // evita o input perder o foco ao clicar no dropdown
    dropdown.addEventListener("mousedown", (e) => e.preventDefault());

    document.addEventListener("click", (e) => {
        if (!wrap.contains(e.target)) closeDropdown();
    });

    input.addEventListener("blur", () => {
        setTimeout(() => {
            if (!wrap.contains(document.activeElement)) closeDropdown();
        }, 120);
    });

})();


/* ============================================================
   PARTE 3 — LOADING AO MUDAR DE PÁGINA (links internos)
   - ✅ 5) Mostra a linha quando clicas num link interno (/...)
   - Ignora: anchors (#), javascript:, mailto:, tel:
   - Ignora: Ctrl/Shift/Alt/Meta (abrir em nova tab, etc.)
   ============================================================ */
(function () {
    document.addEventListener("click", (e) => {
        const a = e.target.closest("a[href]");
        if (!a) return;

        // se já foi prevenido por outra coisa, não mexemos
        if (e.defaultPrevented) return;

        const href = a.getAttribute("href") || "";

        // ignora links que não são navegação normal
        if (!href || href === "#") return;
        if (href.startsWith("#")) return;
        if (href.startsWith("javascript:")) return;
        if (href.startsWith("mailto:")) return;
        if (href.startsWith("tel:")) return;

        // se for para outra origem (http/https) não mexemos
        if (href.startsWith("http://") || href.startsWith("https://")) return;

        // se o user está a abrir em nova tab/janela, não faz sentido mostrar loader
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;

        // só links internos
        if (href.startsWith("/")) {
            window.HeaderLoader?.start();
        }
    });

    // também ativa quando há submit de forms (ex: /search)
    document.addEventListener("submit", (e) => {
        const form = e.target;
        if (!form || form.tagName !== "FORM") return;

        // se for GET/POST normal -> mostra loader
        window.HeaderLoader?.start();
    });

    // segurança: quando a página termina de carregar, garante que o loader não fica preso
    window.addEventListener("pageshow", () => {
        // se o browser voltou do cache (bfcache) pode ficar ativo -> limpamos
        const loader = document.getElementById("header-loader");
        if (loader) loader.classList.remove("active");
    });
})();
