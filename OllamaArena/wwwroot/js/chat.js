window.ollamaHistory = {
    addEntry: function (entry) {
        let list = JSON.parse(localStorage.getItem("ollama_history") || "[]");
        list.push(entry);
        localStorage.setItem("ollama_history", JSON.stringify(list));
    },

    getHistory: function () {
        return JSON.parse(localStorage.getItem("ollama_history") || "[]");
    },

    clear: function () {
        localStorage.removeItem("ollama_history");
    }
};

window.chatInput = (function () {
    let isResetting = false;

    function insertNewlineAtCaret(el) {
        if (!el) return;
        const start = el.selectionStart;
        const end = el.selectionEnd;
        const value = el.value;
        el.value = value.slice(0, start) + '\n' + value.slice(end);
        el.selectionStart = el.selectionEnd = start + 1;
        autoResize(el);
    }

    function autoResize(el) {
        if (!el) return;

        if (isResetting || !el.value || el.value.trim() === '') {
            el.style.height = '';
            el.style.removeProperty('height');
            return;
        }

        el.style.height = 'auto';
        el.style.height = (el.scrollHeight + 2) + 'px';
    }

    function resetHeight(el) {
        if (!el) return;

        isResetting = true;

        el.style.height = '';
        el.style.removeProperty('height');

        let attempts = 0;
        const forceClear = () => {
            if (el) {
                el.style.height = '';
                el.style.removeProperty('height');
            }
            attempts++;
            if (attempts < 3) {
                requestAnimationFrame(forceClear);
            } else {
                isResetting = false;
            }
        };

        requestAnimationFrame(forceClear);
    }

    function attachHandlers(el, dotNetRef) {
        if (!el) return;

        if (el.__chatInputHandler) {
            el.__chatInputDotNetRef = dotNetRef;
            return;
        }

        const handler = function (e) {
            if (e.key === 'Enter') {

                // SHIFT+ENTER → nova linha
                if (e.shiftKey) {
                    e.preventDefault();
                    insertNewlineAtCaret(el);
                    return;
                }

                // ENTER normal → enviar
                e.preventDefault();

                el.dispatchEvent(new Event('change', { bubbles: true }));
                el.dispatchEvent(new Event('input', { bubbles: true }));

                const ref = el.__chatInputDotNetRef || dotNetRef;
                if (ref) ref.invokeMethodAsync('OnEnterPressedFromJs');
            }
        };

        const resizeHandler = function () { autoResize(el); };

        el.__chatInputHandler = handler;
        el.__chatInputResizeHandler = resizeHandler;
        el.__chatInputDotNetRef = dotNetRef;

        el.addEventListener('keydown', handler);
        el.addEventListener('input', resizeHandler);

        autoResize(el);
    }

    function detachHandlers(el) {
        if (!el) return;

        if (el.__chatInputHandler) {
            el.removeEventListener('keydown', el.__chatInputHandler);
            delete el.__chatInputHandler;
        }
        if (el.__chatInputResizeHandler) {
            el.removeEventListener('input', el.__chatInputResizeHandler);
            delete el.__chatInputResizeHandler;
        }
    }

    return { attachHandlers, detachHandlers, insertNewlineAtCaret, resetHeight };
})();

window.chatScroll = {

    getScrollState: function (el) {
        try {
            if (!el) return null;

            return {
                scrollTop: el.scrollTop,
                scrollHeight: el.scrollHeight,
                clientHeight: el.clientHeight
            };
        } catch {
            return null;
        }
    },

    isAtBottom: function (el, threshold = 80) {
        try {
            if (!el) return true;

            const distance = el.scrollHeight - el.scrollTop - el.clientHeight;
            return distance <= threshold;
        } catch {
            return true;
        }
    },

    scrollToBottom: function (el) {
        try {
            if (!el) return;

            requestAnimationFrame(() => {
                try {
                    requestAnimationFrame(() => {
                        el.scrollTop = el.scrollHeight;
                    });
                } catch { }
            });

        } catch { }
    },

    scrollDuringStream: function (el) {
        try {
            if (!el) return;

            const distance = el.scrollHeight - (el.scrollTop + el.clientHeight);
            const shouldStick = distance < 120;

            if (!shouldStick) return;

            el.scrollTop = el.scrollHeight;

        } catch { }
    }
};
