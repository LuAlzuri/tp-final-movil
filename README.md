# Turnero Peluquería 1.0.1 — Prueba Android

APK de prueba actualizada con los últimos cambios de `main`, commit `cac5fad0421252d6a5ea93a9c6b96264854a2d4b`.

Incluye las mejoras de cierre de turnos y gestión de patologías integradas en el PR #37.

## Instalación

1. Descargar `Turnero-Peluqueria-arm64-prueba.apk`.
2. Si está instalada la APK anterior, desinstalarla primero: esta compilación usa una clave de desarrollo diferente y Android no permite actualizar sobre una firma distinta. Se elimina el estado local de la aplicación; los datos de la API se conservan.
3. Permitir temporalmente la instalación desde el navegador o gestor de archivos utilizado, abrir la APK e instalarla.
4. Iniciar sesión con una cuenta autorizada de la API configurada.

Requisitos: Android 7.0 (API 24) o superior y procesador ARM64 (`arm64-v8a`). Requiere conectividad con `https://trabajo-final-vet-mov.runasp.net/`.

## Estado de esta versión

- Versión: 1.0.1; código: 2.
- Compilación Debug de prueba con dependencias incluidas, sin necesidad de Visual Studio ni depurador en el celular.
- Firma de desarrollo APK v2 y v3 verificada. No es una publicación para Google Play.
- Compilación completada correctamente y metadatos del paquete verificados.
- Ejecución y acceso a cuentas no validados en un dispositivo; versión preliminar.
- Se usó `MauiXamlInflator=XamlC` como parámetro de compilación, sin modificar el proyecto.
- No incluye contraseñas de usuarios ni el archivo de la clave de firma.

El archivo `.sha256` permite comprobar la integridad de la descarga.
