// The browser reports color-scheme changes; the .NET preference service decides
// whether they affect the active MudBlazor theme (manual choices take precedence).
export function observeSystemDarkMode(dotNetObserver) {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = event => {
        void dotNetObserver.invokeMethodAsync("OnSystemDarkModeChanged", event.matches);
    };

    media.addEventListener("change", onChange);

    return {
        getCurrentDarkMode: () => media.matches,
        dispose: () => media.removeEventListener("change", onChange)
    };
}
