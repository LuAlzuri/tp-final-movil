const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const contexto = { window: {} };
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../Turnero.App/wwwroot/js/veteapp.js'), 'utf8'), contexto);
const api = contexto.window.veteApp;

class Dialogo extends EventTarget {
    dataset = {};
    open = false;
    suscripciones = 0;
    addEventListener(...argumentos) {
        this.suscripciones++;
        super.addEventListener(...argumentos);
    }
    showModal() { this.open = true; }
    close() { this.open = false; }
    escapar() {
        const evento = new Event('cancel', { cancelable: true });
        if (this.dispatchEvent(evento)) this.close();
        return evento;
    }
}

const veto = new Dialogo();
veto.dataset.ocupado = 'false';
api.abrirConfirmacion(veto);
assert.equal(veto.open, true, 'La confirmación se abre');
assert.equal(veto.escapar().defaultPrevented, false, 'El diálogo libre admite Escape');
assert.equal(veto.open, false);

api.abrirConfirmacion(veto);
veto.dataset.ocupado = 'true';
assert.equal(veto.escapar().defaultPrevented, true, 'El cambio de estado bloquea Escape durante el envío');
assert.equal(veto.open, true, 'El diálogo permanece abierto durante la operación');
veto.dataset.ocupado = 'false';
assert.equal(veto.escapar().defaultPrevented, false, 'El bloqueo se libera al finalizar');
assert.equal(veto.open, false);

api.abrirConfirmacion(veto);
assert.equal(veto.suscripciones, 1, 'Reabrir el diálogo no duplica manejadores');
veto.dataset.ocupado = 'true';
api.cerrarConfirmacion(veto);
assert.equal(veto.open, false, 'La operación puede cerrar el diálogo programáticamente al completarse');

const otro = new Dialogo();
api.abrirConfirmacion(otro);
assert.equal(otro.escapar().defaultPrevented, false, 'Los demás diálogos conservan su comportamiento');
assert.equal(otro.open, false);
assert.equal(veto.dataset.ocupado, 'true', 'Cada diálogo mantiene un estado independiente');
console.log('Diálogos verificados: apertura, cierre, Escape durante el envío y reapertura sin suscripciones duplicadas.');
