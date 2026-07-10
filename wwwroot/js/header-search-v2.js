(() => {
    const dropdown = document.getElementById("igdb-dropdown");
    if (!dropdown) return;

    let isFormatting = false;

    function splitLabel(value) {
        const text = (value || "").trim();
        const match = text.match(/^(.*)\s\((\d{4})\)$/);

        return match
            ? { title: match[1].trim(), year: match[2] }
            : { title: text, year: "" };
    }

    function formatDropdown() {
        if (isFormatting) return;
        isFormatting = true;

        try {
            dropdown.querySelectorAll(".igdb-item").forEach(item => {
                if (item.dataset.gfFormatted === "true") return;

                const titleNode = item.querySelector(".igdb-title");
                const parsed = splitLabel(titleNode?.textContent);

                item.innerHTML = `
                    <span class="igdb-meta">
                        <span class="igdb-title">${escapeHtml(parsed.title || "Untitled")}</span>
                        ${parsed.year ? `<span class="igdb-year">${escapeHtml(parsed.year)}</span>` : ""}
                    </span>
                    <span class="igdb-open" aria-hidden="true">
                        <i class="fas fa-arrow-right"></i>
                    </span>`;

                item.dataset.gfFormatted = "true";
            });

            dropdown.querySelectorAll(".igdb-footer").forEach(footer => {
                const value = footer.textContent?.trim() || "";

                if (value.startsWith("Sem resultados para")) {
                    const term = value.match(/"([^"]+)"/)?.[1] || "this search";
                    footer.innerHTML = `No games found for <strong>${escapeHtml(term)}</strong>.`;
                }
                else if (value.includes("Enter para abrir")) {
                    footer.textContent = "Use ↑ ↓ to browse · Enter to open · Esc to close";
                }
                else if (value.includes("Erro a carregar resultados")) {
                    footer.textContent = "Suggestions are temporarily unavailable.";
                }
            });
        }
        finally {
            isFormatting = false;
        }
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    dropdown.addEventListener("pointermove", event => {
        const rect = dropdown.getBoundingClientRect();
        const x = Math.max(0, Math.min(rect.width, event.clientX - rect.left));
        const y = Math.max(0, Math.min(rect.height, event.clientY - rect.top));

        dropdown.style.setProperty("--gf-search-menu-x", `${x}px`);
        dropdown.style.setProperty("--gf-search-menu-y", `${y}px`);
    });

    dropdown.addEventListener("pointerleave", () => {
        dropdown.style.setProperty("--gf-search-menu-x", "50%");
        dropdown.style.setProperty("--gf-search-menu-y", "0%");
    });

    new MutationObserver(formatDropdown).observe(dropdown, {
        childList: true,
        subtree: true
    });

    formatDropdown();
})();
