/* ============================================================================
   GAMEFILLED - SITE.JS

   Este ficheiro trata funcionalidades globais do site, sobretudo no header:
   - Dropdown do perfil em hover no desktop
   - Header loading bar
   - Autocomplete de jogos via IGDB
   - Navegação com Enter no autocomplete
   - Loading visual ao navegar entre páginas internas

   Organização:
   1. Dropdown do perfil
   2. API global do header loader
   3. Autocomplete IGDB
   4. Loading ao navegar em links/forms
   ============================================================================ */


/* ============================================================================
   PARTE 1 — DROPDOWN DO PERFIL (HOVER EM DESKTOP)
   ----------------------------------------------------------------------------
   Objetivo:
   - Permitir que o dropdown do perfil abra ao passar o rato por cima
   - Só em ecrãs desktop
   - Não quebrar o resto do JS se jQuery não existir
   ============================================================================ */
(function ($) {
    "use strict";

    // Se o jQuery não estiver carregado, esta funcionalidade é desativada
    // sem interromper o resto do JavaScript do site.
    if (typeof $ === "undefined") {
        console.warn("jQuery não encontrado — dropdown do perfil desativado.");
        return;
    }

    // Espera que o DOM esteja pronto.
    $(function () {

        /**
         * Ativa ou reativa o comportamento hover do dropdown do perfil.
         * É chamado no arranque e também no resize.
         */
        function enableHoverDropdown() {
            // Seleciona o item de navbar que contém o perfil.
            var $profile = $('#profile-li');

            // Remove handlers antigos com namespace profileHover,
            // para evitar duplicação de eventos em resizes sucessivos.
            $profile.off('mouseenter.profileHover mouseleave.profileHover');

            // Só ativa hover em desktop/tablet largo.
            // Em mobile, normalmente o comportamento deve ficar por clique.
            if (window.matchMedia("(min-width: 768px)").matches) {
                $profile.on('mouseenter.profileHover', function () {
                    // Adiciona classes Bootstrap-like de dropdown aberto.
                    $(this).addClass('show');
                    $(this).find('.dropdown-menu').addClass('show');

                    // Atualiza acessibilidade do botão toggle.
                    $(this).find('.dropdown-toggle').attr('aria-expanded', 'true');
                });

                $profile.on('mouseleave.profileHover', function () {
                    // Fecha o dropdown ao sair da área do perfil.
                    $(this).removeClass('show');
                    $(this).find('.dropdown-menu').removeClass('show');

                    // Atualiza estado ARIA.
                    $(this).find('.dropdown-toggle').attr('aria-expanded', 'false');
                });
            }
        }

        // Ativa o comportamento no carregamento inicial.
        enableHoverDropdown();

        // Debounce manual para não recalcular em todos os eventos de resize.
        var resizeTimer = null;

        $(window).on('resize', function () {
            clearTimeout(resizeTimer);

            // Reaplica a lógica com pequeno atraso para evitar excesso de chamadas.
            resizeTimer = setTimeout(enableHoverDropdown, 150);
        });
    });

}(jQuery));


/* ============================================================================
   PARTE 2 — HEADER LOADER (API GLOBAL)
   ----------------------------------------------------------------------------
   Objetivo:
   - Expor uma API simples global: window.HeaderLoader.start() / stop()
   - Mostrar uma barra de loading no topo durante pedidos/navegação
   - Suportar vários pedidos ao mesmo tempo com contador interno
   ============================================================================ */
(function () {
    // Procura o elemento visual da barra de loading.
    const loader = document.getElementById("header-loader");

    // Se não existir no layout atual, não faz nada.
    if (!loader) return;

    // Contador de operações ativas.
    // Isto evita esconder a barra cedo demais quando existem vários pedidos.
    let activeRequests = 0;

    // Cria uma pequena API global.
    window.HeaderLoader = {
        /**
         * Inicia ou mantém visível a barra de loading.
         */
        start() {
            activeRequests++;
            loader.classList.add("active");
        },

        /**
         * Termina um pedido ativo.
         * A barra só desaparece quando o contador chega a zero.
         */
        stop() {
            activeRequests = Math.max(0, activeRequests - 1);

            // Pequeno atraso para evitar flicker visual.
            if (activeRequests === 0) {
                setTimeout(() => {
                    loader.classList.remove("active");
                }, 200);
            }
        }
    };
})();


/* ============================================================================
   PARTE 3 — AUTOCOMPLETE IGDB NO HEADER
   ----------------------------------------------------------------------------
   Objetivo:
   - Pesquisar jogos à medida que o utilizador escreve
   - Mostrar dropdown de resultados
   - Ordenar resultados com um score simples
   - Permitir navegação com teclado
   - Enter:
       -> com item ativo: abre /games/{id}
       -> sem item ativo: deixa o form submeter normalmente para /search
   ============================================================================ */
(function () {

    /* ------------------------------------------------------------------------
       CONFIGURAÇÃO
       ------------------------------------------------------------------------ */
    const DEBUG = true;
    const MAX_RESULTS = 25;

    // Wrappers simples para logs condicionais.
    function log(...a) { if (DEBUG) console.log(...a); }
    function warn(...a) { if (DEBUG) console.warn(...a); }
    function error(...a) { if (DEBUG) console.error(...a); }

    /* ------------------------------------------------------------------------
       HELPERS
       ------------------------------------------------------------------------ */

    /**
     * Cria uma versão "debounced" de uma função.
     * Só executa após um intervalo sem novas chamadas.
     */
    function debounce(fn, ms) {
        let t = null;

        return (...args) => {
            clearTimeout(t);
            t = setTimeout(() => fn.apply(null, args), ms);
        };
    }

    /**
     * Shortcut para querySelector.
     */
    function qs(sel, root = document) {
        return root.querySelector(sel);
    }

    /**
     * Shortcut para querySelectorAll convertido em array.
     */
    function qsa(sel, root = document) {
        return [...root.querySelectorAll(sel)];
    }

    /**
     * Verifica se um elemento existe e não está escondido via d-none.
     */
    function isVisible(el) {
        return el && !el.classList.contains("d-none");
    }

    /**
     * Escapa HTML para evitar problemas de render e XSS em strings dinâmicas.
     */
    function escapeHtml(s) {
        return String(s ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    /**
     * Converte timestamp UNIX em segundos para ano.
     */
    function unixToYear(unix) {
        if (!unix || typeof unix !== "number") return "";

        try {
            const y = new Date(unix * 1000).getFullYear();
            return Number.isFinite(y) ? String(y) : "";
        } catch {
            return "";
        }
    }

    /* ------------------------------------------------------------------------
       SCORING / ORDENAÇÃO DOS RESULTADOS
       ------------------------------------------------------------------------ */

    // Categoria do IGDB para "main game".
    const MAIN_GAME = 0;

    /**
     * Dá score ao nome do jogo consoante o match com o termo.
     * Favorece igualdade exata, prefixo e inclusão simples.
     */
    function titleScore(term, name) {
        const t = (term || "").trim().toLowerCase();
        const n = (name || "").trim().toLowerCase();

        if (!t || !n) return 0;

        let score = 0;

        if (n === t) score += 1200;
        if (n.startsWith(t)) score += 800;
        if (n.includes(t)) score += 250;

        // Penaliza resultados que costumam ser menos desejáveis
        // numa pesquisa principal de jogo.
        const bad = ["season", "chapter", "episode", "dlc", "demo", "trial", "pack", "bundle", "expansion"];
        for (const b of bad) {
            if (n.includes(b)) score -= 350;
        }

        return score;
    }

    /**
     * Dá score com base em popularidade aproximada.
     */
    function popularityScore(g) {
        const follows = Number(g?.follows ?? 0);
        const ratings = Number(g?.totalRatingCount ?? 0);

        return Math.log10(follows + 1) * 350 + Math.log10(ratings + 1) * 180;
    }

    /**
     * Favorece jogos base.
     */
    function baseGameBoost(g) {
        return (g?.category === MAIN_GAME) ? 2500 : 0;
    }

    /* ------------------------------------------------------------------------
       ELEMENTOS DOM
       ------------------------------------------------------------------------ */
    const input = qs("#nav-bar-search");
    const wrap = qs("#nav-search-wrap");
    const dropdown = qs("#igdb-dropdown");

    // Se o header de pesquisa não existir nesta página/layout, saímos.
    if (!input || !wrap || !dropdown) return;

    /* ------------------------------------------------------------------------
       STATE INTERNO
       ------------------------------------------------------------------------ */
    let items = [];
    let activeIndex = -1;
    let lastTerm = "";
    let abortController = null;

    /**
     * Mostra o dropdown.
     */
    function openDropdown() {
        dropdown.classList.remove("d-none");
    }

    /**
     * Fecha e limpa o dropdown.
     */
    function closeDropdown() {
        dropdown.classList.add("d-none");
        dropdown.innerHTML = "";
        items = [];
        activeIndex = -1;
    }

    /**
     * Define o item atualmente ativo no dropdown.
     */
    function setActive(index) {
        activeIndex = index;

        const els = qsa(".igdb-item", dropdown);

        // Marca visualmente o item ativo.
        els.forEach((el, i) => el.classList.toggle("active", i === index));

        // Garante que o item ativo fica visível no scroll do dropdown.
        if (els[index]) {
            els[index].scrollIntoView({ block: "nearest" });
        }
    }

    /* ------------------------------------------------------------------------
       RENDER DO DROPDOWN
       ------------------------------------------------------------------------ */

    /**
     * Renderiza os resultados no dropdown.
     */
    function renderDropdown(term, results) {
        items = results || [];
        activeIndex = -1;

        // Caso não existam resultados.
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

            // Formato mostrado ao utilizador: Nome (Ano)
            const label = year
                ? `${escapeHtml(name)} (${escapeHtml(year)})`
                : `${escapeHtml(name)}`;

            // A tua rota usa ID numérico: /games/{id}
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

    /* ------------------------------------------------------------------------
       FETCH DOS RESULTADOS
       ------------------------------------------------------------------------ */

    /**
     * Faz pedido ao endpoint de pesquisa IGDB do projeto.
     */
    async function fetchResults(term) {
        term = (term || "").trim();

        // Não pesquisa com menos de 2 caracteres.
        if (term.length < 2) {
            closeDropdown();
            return;
        }

        // Evita novo fetch do mesmo termo se já estiver aberto.
        if (term === lastTerm && isVisible(dropdown)) return;
        lastTerm = term;

        // Cancela pedido anterior se ainda estiver em curso.
        if (abortController) abortController.abort();
        abortController = new AbortController();

        try {
            // Ativa a barra de loading durante o pedido.
            window.HeaderLoader?.start();

            const url = `/api/igdbsearch?term=${encodeURIComponent(term)}`;
            log("IGDB fetch ->", url);

            const res = await fetch(url, {
                signal: abortController.signal,
                headers: { "Accept": "application/json" }
            });

            if (!res.ok) {
                throw new Error(`HTTP ${res.status}`);
            }

            const data = await res.json();

            // Aceita vários formatos de resposta para maior robustez.
            let gamesArray = [];

            if (data && typeof data.responseText === "string") {
                try {
                    gamesArray = JSON.parse(data.responseText);
                } catch (e) {
                    error("Falha parse responseText", e);
                    gamesArray = [];
                }
            }
            else if (Array.isArray(data)) {
                gamesArray = data;
            }
            else if (data && Array.isArray(data.results)) {
                gamesArray = data.results;
            }

            // Normaliza possíveis nomes de propriedades vindos do backend/DTOs.
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

            // Ordenação por:
            // 1. jogo base
            // 2. score textual
            // 3. popularidade
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

            // Limita o número de resultados renderizados.
            normalized = normalized.slice(0, MAX_RESULTS);

            renderDropdown(term, normalized);
        } catch (err) {
            // AbortError é esperado quando se cancela um pedido anterior.
            if (err && err.name === "AbortError") return;

            warn("Erro IGDB:", err);

            dropdown.innerHTML = `<div class="igdb-footer">Erro a carregar resultados.</div>`;
            openDropdown();
        } finally {
            // Esconde o loader independentemente de sucesso/erro.
            window.HeaderLoader?.stop();
        }
    }

    // Versão debounced do fetch.
    const fetchDebounced = debounce(fetchResults, 200);

    /* ------------------------------------------------------------------------
       EVENTOS DO INPUT
       ------------------------------------------------------------------------ */

    // Pesquisa ao escrever.
    input.addEventListener("input", (e) => {
        fetchDebounced(e.target.value);
    });

    // Reabre resultados quando ganha foco, se já houver termo suficiente.
    input.addEventListener("focus", () => {
        const term = input.value.trim();

        if (term.length >= 2 && !isVisible(dropdown)) {
            fetchDebounced(term);
        }
    });

    // Navegação por teclado.
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
            // Se houver item ativo, navega para esse jogo.
            if (activeIndex >= 0 && els[activeIndex]) {
                const href = els[activeIndex].getAttribute("href") || "#";

                if (href !== "#") {
                    e.preventDefault();

                    // Mostra loader antes da navegação real.
                    window.HeaderLoader?.start();

                    window.location.href = href;
                }
            }
            // Se não houver item ativo, deixa o submit normal do form acontecer.
        }
        else if (e.key === "Escape") {
            e.preventDefault();
            closeDropdown();
        }
    });

    /* ------------------------------------------------------------------------
       MOUSE / FOCO / CLIQUE FORA
       ------------------------------------------------------------------------ */

    // Hover visual dos itens por movimento do rato.
    dropdown.addEventListener("mousemove", (e) => {
        const a = e.target.closest(".igdb-item");
        if (!a) return;

        const idx = parseInt(a.getAttribute("data-idx"), 10);
        if (!Number.isNaN(idx)) {
            setActive(idx);
        }
    });

    // Evita que o input perca foco ao clicar dentro do dropdown.
    dropdown.addEventListener("mousedown", (e) => e.preventDefault());

    // Fecha ao clicar fora da área de pesquisa.
    document.addEventListener("click", (e) => {
        if (!wrap.contains(e.target)) {
            closeDropdown();
        }
    });

    // Fecha quando sai do input e o foco já não está dentro do wrapper.
    input.addEventListener("blur", () => {
        setTimeout(() => {
            if (!wrap.contains(document.activeElement)) {
                closeDropdown();
            }
        }, 120);
    });

})();


/* ============================================================================
   PARTE 4 — LOADING AO NAVEGAR ENTRE PÁGINAS INTERNAS
   ----------------------------------------------------------------------------
   Objetivo:
   - Mostrar a barra de loading ao clicar em links internos
   - Mostrar a barra ao submeter formulários
   - Ignorar ligações especiais (#, mailto, tel, javascript, links externos)
   - Não interferir com abrir em nova tab/janela
   ============================================================================ */
(function () {
    document.addEventListener("click", (e) => {
        const a = e.target.closest("a[href]");
        if (!a) return;

        // Se outro código já preveniu a ação, não fazemos nada.
        if (e.defaultPrevented) return;

        const href = a.getAttribute("href") || "";

        // Ignora links não navegáveis ou especiais.
        if (!href || href === "#") return;
        if (href.startsWith("#")) return;
        if (href.startsWith("javascript:")) return;
        if (href.startsWith("mailto:")) return;
        if (href.startsWith("tel:")) return;

        // Ignora links absolutos para outras origens.
        if (href.startsWith("http://") || href.startsWith("https://")) return;

        // Ignora ações de abrir em nova tab/janela.
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;

        // Só mostra loader em navegação interna do site.
        if (href.startsWith("/")) {
            window.HeaderLoader?.start();
        }
    });

    // Também mostra loading em submit de formulários normais.
    document.addEventListener("submit", (e) => {
        const form = e.target;
        if (!form || form.tagName !== "FORM") return;

        window.HeaderLoader?.start();
    });

    // Segurança extra:
    // Se a página voltar do bfcache do browser, limpa o loader.
    window.addEventListener("pageshow", () => {
        const loader = document.getElementById("header-loader");
        if (loader) {
            loader.classList.remove("active");
        }
    });
})();