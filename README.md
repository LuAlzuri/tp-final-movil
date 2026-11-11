# Turnero Peluquería 1.0 — Prueba Android

APK para probar la aplicación en celulares Android ARM64 y obtener capturas de las pantallas por rol.

## Instalación

1. Descargar `Turnero-Peluqueria-arm64-prueba.apk` en el celular.
2. Permitir temporalmente la instalación de aplicaciones desde el navegador o gestor de archivos utilizado.
3. Abrir el APK e instalarlo.
4. Iniciar sesión con una cuenta autorizada de la API configurada.

Requisitos: Android 7.0 (API 24) o superior y procesador ARM64 (`arm64-v8a`). Requiere conectividad con la API `https://trabajo-final-vet-mov.runasp.net/`.

## Estado de esta versión

- Versión de aplicación: 1.0, código 1.
- Compilación Debug de prueba, firmada con una clave de desarrollo. No es una publicación en Google Play.
- APK con dependencias incluidas; no necesita Visual Studio ni un depurador en el celular.
- Compilación completada con cero errores y advertencias; firma APK v2 y v3 verificada.
- Ejecución y acceso a cuentas todavía no validados en un dispositivo: el emulador del entorno no permitió completar la prueba. Esta publicación debe marcarse como versión preliminar.
- Se usó `MauiXamlInflator=XamlC` como opción de compilación local, sin cambiar el proyecto.

Código equivalente al commit de main `55cde380c55e57bc7dc89f8f119f16221e5be120`. El APK no incluye contraseñas de usuarios ni el archivo de la clave de firma.
