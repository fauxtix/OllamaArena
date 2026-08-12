window.appViewport = {
    getWidth: function () {
        return window.innerWidth || document.documentElement.clientWidth || 0;
    },

    subscribeResize: function (dotNetRef) {
        const handler = function () {
            dotNetRef.invokeMethodAsync('OnViewportResized', window.innerWidth || document.documentElement.clientWidth || 0);
        };
        window.appViewport._resizeHandler = handler;
        window.addEventListener('resize', handler);
    },

    unsubscribeResize: function () {
        if (window.appViewport._resizeHandler) {
            window.removeEventListener('resize', window.appViewport._resizeHandler);
            window.appViewport._resizeHandler = null;
        }
    }
};
