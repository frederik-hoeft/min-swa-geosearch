export function supportsWebGl2() {
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('webgl2');
    if (context === null) {
        return false;
    }

    // Release the probe context; MapLibre will create the actual rendering context.
    context.getExtension('WEBGL_lose_context')?.loseContext();
    return true;
}
