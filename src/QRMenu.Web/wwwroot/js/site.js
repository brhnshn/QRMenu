(function () {
    function getCsrfToken() {
        var meta = document.querySelector('meta[name="csrf-token"]');
        if (meta && meta.content) return meta.content;

        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function isUnsafeMethod(method) {
        return ['POST', 'PUT', 'PATCH', 'DELETE'].includes((method || 'GET').toUpperCase());
    }

    function isSameOrigin(input) {
        try {
            var url = typeof input === 'string'
                ? new URL(input, window.location.origin)
                : new URL(input.url, window.location.origin);
            return url.origin === window.location.origin;
        } catch (e) {
            return true;
        }
    }

    var originalFetch = window.fetch;
    window.fetch = function (input, init) {
        init = init || {};
        var method = init.method || (input && input.method) || 'GET';
        var token = getCsrfToken();

        if (token && isUnsafeMethod(method) && isSameOrigin(input)) {
            var headers = new Headers(init.headers || (input && input.headers) || {});
            if (!headers.has('RequestVerificationToken')) {
                headers.set('RequestVerificationToken', token);
            }
            init.headers = headers;
        }

        return originalFetch(input, init);
    };
})();
