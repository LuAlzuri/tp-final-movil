# Turnero Peluquería 1.0.2 — Prueba Android

[Descargar APK](./Turnero-Peluqueria-arm64-prueba.apk?raw=true)

Esta rama de distribución contiene la APK 1.0.2 (código 3). Se mantiene el nombre de archivo usado por las versiones anteriores.

Incluye las correcciones del diálogo e historial de vetos, la gestión de reservas presenciales, los horarios argentinos de auditoría y el inicio del tutor con solo cerrar sesión en el footer.

## Instalación

1. Descargar `Turnero-Peluqueria-arm64-prueba.apk` en el teléfono.
2. Desinstalar la APK de prueba anterior: utiliza otra firma de desarrollo y Android no permite actualizar sobre una firma distinta.
3. Abrir la APK nueva e instalarla. Si Android lo solicita, permitir la instalación desde el navegador o gestor de archivos utilizado.
4. Iniciar sesión nuevamente. Los datos de la API se conservan; se elimina el estado local de la instalación anterior.

Requisitos: Android 7.0 (API 24) o superior, procesador ARM64 (`arm64-v8a`) y conexión a `https://trabajo-final-vet-mov.runasp.net/`.

## Verificación

- Versión 1.0.2; código 3; paquete `ar.turneropeluqueria.app`.
- Compilación Debug de prueba con ensamblados y runtime incluidos; no requiere Visual Studio ni depurador en el teléfono.
- Firma APK v2 y v3 verificada; archivo SHA-256 incluido para comprobar la integridad.
- JavaScript y ensamblado empaquetados verificados con la corrección del atributo rechazado por el WebView.
- No contiene el archivo privado de firma ni credenciales de usuarios.
- Instalación y ejecución en un teléfono físico todavía no verificadas; versión preliminar.

Código fuente: commit `e74cca82701933946d527887ee60e9f22a1ad1a2`, rama `Corregir-dialogo-vetos-android`.
