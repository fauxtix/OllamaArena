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
