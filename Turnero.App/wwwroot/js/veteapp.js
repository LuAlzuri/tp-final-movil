window.veteApp = {
    volver() {
        if (!(window.navigation?.canGoBack ?? (window.history.length > 1))) {
            return false;
        }

        window.history.back();
        return true;
    },
    abrirConfirmacion(dialogo) {
        dialogo.showModal();
    },
    cerrarConfirmacion(dialogo) {
        dialogo.close();
    }
};
