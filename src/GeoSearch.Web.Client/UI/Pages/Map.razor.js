// Keep mobile search interactions open while focus moves among controls or
// a pointer selection completes. Manual expansion stays owned by the page.
export function observeSearchPanel(panel, dotNetReference) {
    const abortController = new AbortController();
    const { signal } = abortController;
    let focusSession = false;
    let pointerInside = false;
    let temporaryExpanded = false;
    let disposed = false;

    function mobileFocusOverlayEnabled() {
        // The media-query boundaries live in _breakpoints.scss, not a duplicated
        // JavaScript matchMedia expression.
        return getComputedStyle(panel).getPropertyValue('--gs-mobile-search-focus').trim() === '1';
    }

    function update() {
        if (disposed) return;
        const focusedInside = panel.contains(document.activeElement);
        if (!focusedInside && !pointerInside) focusSession = false;

        const next = focusSession && mobileFocusOverlayEnabled() && (focusedInside || pointerInside);
        if (next !== temporaryExpanded) {
            temporaryExpanded = next;
            // The page may already be disposed when a browser event is delivered.
            void dotNetReference.invokeMethodAsync('SetTemporaryExpansion', next).catch(error => {
                if (!disposed) console.error('Could not update search panel expansion', error);
            });
        }
    }

    panel.addEventListener('focusin', event => {
        if (event.target.matches('.gs-search-bar input')) focusSession = true;
        update();
    }, { signal });

    panel.addEventListener('focusout', () => {
        // Wait until the next focused control has become document.activeElement.
        setTimeout(update, 0);
    }, { signal });

    panel.addEventListener('pointerdown', () => {
        if (focusSession) {
            pointerInside = true;
            update();
        }
    }, { signal });

    // A non-focusable result can blur the search input before its click handler.
    // Hold the sheet open through the click; restore preference afterwards.
    document.addEventListener('click', () => {
        if (pointerInside) {
            pointerInside = false;
            setTimeout(update, 0);
        }
    }, { signal });

    document.addEventListener('pointerdown', event => {
        if (!panel.contains(event.target)) {
            pointerInside = false;
            focusSession = false;
            update();
        }
    }, { signal });

    window.addEventListener('pointerup', () => {
        // Pointer gestures without a click (for example, scrolling results)
        // must not leave the temporary interaction pinned open.
        if (pointerInside) {
            setTimeout(() => {
                pointerInside = false;
                update();
            }, 0);
        }
    }, { signal });

    window.addEventListener('pointercancel', () => {
        pointerInside = false;
        update();
    }, { signal });
    window.addEventListener('resize', update, { signal });

    return {
        dismiss() {
            focusSession = false;
            pointerInside = false;
            update();
        },
        dispose() {
            disposed = true;
            abortController.abort();
        }
    };
}
