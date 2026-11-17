window.veteApp = (() => {
    const dialogosPreparados = new WeakSet();
    return {
        volver() {
            if (!(window.navigation?.canGoBack ?? (window.history.length > 1))) {
                return false;
            }

            window.history.back();
            return true;
        },
        abrirConfirmacion(dialogo) {
            if (!dialogosPreparados.has(dialogo)) {
                dialogo.addEventListener("cancel", evento => {
                    if (dialogo.dataset.ocupado === "true") {
                        evento.preventDefault();
                    }
                });
                dialogosPreparados.add(dialogo);
            }
            dialogo.showModal();
        },
        cerrarConfirmacion(dialogo) {
            dialogo.close();
        }
    };
})();
