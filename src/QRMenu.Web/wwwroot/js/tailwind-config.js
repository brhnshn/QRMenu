// Tailwind CDN config
(function() {
    if (typeof console !== 'undefined' && console.warn) {
        const orgWarn = console.warn;
        console.warn = function(...args) {
            if (args[0] && typeof args[0] === 'string' && args[0].includes('cdn.tailwindcss.com')) return;
            orgWarn.apply(console, args);
        };
    }

    window.tailwind = window.tailwind || {};
    window.tailwind.config = {
        important: true,
        darkMode: "class",
        theme: {
            extend: {
                "colors": {
                    "surface-dim": "#d5d4d4", "primary-dim": "#922c0c", "surface-tint": "#a23718", "on-primary-fixed-variant": "#581200", "tertiary-fixed": "#ffffff", "secondary-fixed-dim": "#f6ba2d", "surface-container-highest": "#dedcdc", "outline-variant": "#aeadac", "on-error-container": "#570008", "secondary-fixed": "#ffca57", "surface": "#f8f6f5", "on-background": "#2e2f2f", "error-container": "#fb5151", "surface-bright": "#f8f6f5", "inverse-surface": "#0e0e0e", "tertiary": "#5c5b5b", "surface-container-high": "#e4e2e2", "primary": "#a23718", "on-tertiary-container": "#636262", "on-primary": "#ffefec", "inverse-primary": "#fd7954", "tertiary-fixed-dim": "#f3f0f0", "surface-variant": "#dedcdc", "surface-container": "#eae8e7", "on-secondary": "#fff1dc", "on-error": "#ffefee", "tertiary-dim": "#504f4f", "error-dim": "#9f0519", "background": "#f8f6f5", "on-primary-fixed": "#000000", "primary-container": "#fd7954", "surface-container-lowest": "#ffffff", "on-surface": "#2e2f2f", "on-secondary-fixed": "#453100", "inverse-on-surface": "#9e9d9c", "primary-fixed": "#fd7954", "on-tertiary-fixed": "#505050", "on-surface-variant": "#5c5b5b", "primary-fixed-dim": "#ec6d49", "on-secondary-fixed-variant": "#694c00", "on-primary-container": "#480d00", "on-tertiary-fixed-variant": "#6e6d6d", "tertiary-container": "#ffffff", "on-tertiary": "#f5f2f1", "secondary-container": "#ffca57", "secondary": "#765600", "error": "#b31b25", "surface-container-low": "#f2f0f0", "outline": "#777776", "on-secondary-container": "#5d4300", "secondary-dim": "#674b00"
                },
                "borderRadius": { "DEFAULT": "0.25rem", "lg": "0.5rem", "xl": "0.75rem", "full": "9999px" },
                "fontFamily": { "headline": ["Plus Jakarta Sans"], "body": ["Be Vietnam Pro"], "label": ["Plus Jakarta Sans"] }
            }
        }
    };
})();
