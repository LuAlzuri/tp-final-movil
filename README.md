# Turnero de peluquería canina

API REST para gestionar tutores, perros, cupos, turnos, vetos, auditoría y reportes. Usa ASP.NET Core sobre `net10.0`, EF Core 10.0.14 y SQL Server.

La API usa JwtBearer 10.0.14, Swashbuckle 10.3.1, QuestPDF 2026.9.1, SkiaSharp y sus assets nativos de Linux 4.153.1, Serilog.Extensions.Logging 10.0.0 y Serilog.Sinks.File 7.0.0. La herramienta local `dotnet-ef` es 10.0.14.

## Configuración y arranque

Desde la raíz del repositorio, copiá la configuración de ejemplo si todavía no tenés una configuración local y ajustá `ConnectionStrings:DefaultConnection` para tu instancia de SQL Server. Ejecutá los siguientes comandos en PowerShell 7.

```powershell
if (-not (Test-Path turnero_peluqueria/appsettings.json)) {
    Copy-Item turnero_peluqueria/appsettings.example.json turnero_peluqueria/appsettings.json
}
$env:Jwt__Key = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Una vez guardada la cadena de conexión en el archivo local, restaurá las dependencias y aplicá las migraciones.

```powershell
dotnet restore trabajo_final.sln
dotnet tool restore
dotnet ef database update --project turnero_peluqueria
```

Conservá la misma clave JWT entre reinicios si querés que las sesiones existentes sigan vigentes. La configuración local está ignorada por Git. Para crear el primer administrador, completá los datos de `Bootstrap` en el archivo local o mediante variables de entorno y ejecutá el comando de arranque una sola vez.

```powershell
dotnet run --project turnero_peluqueria -- --crear-administrador
dotnet run --project turnero_peluqueria --launch-profile https
```

En desarrollo, Swagger está en `https://localhost:7122/swagger`. Iniciá sesión en `POST /api/autenticacion/iniciar-sesion` con los campos `email` y `contrasena` y usá el token como `Authorization: Bearer <token>`. El administrador da de alta a los veterinarios y cada veterinario puede dar de alta a tutores. El cambio de contraseña invalida las sesiones previas.

## Imágenes de perros

MAUI envía `POST /api/perros/{id}/imagen` como `multipart/form-data`, con el archivo en el campo `archivo`. La subida y el borrado mediante `DELETE /api/perros/{id}/imagen` requieren una sesión de veterinario.

La ficha del perro devuelve `imagenUrl` como una ruta relativa o `null`. La app resuelve la ruta contra la URL base de la API. El archivo se sirve sin autenticación a cualquiera que tenga su URL.

Las imágenes se guardan en `wwwroot/uploads`. El despliegue requiere almacenamiento persistente en esa ruta, compartido si la API corre en varias instancias.

## Aplicación Android

`Turnero.App` es la aplicación móvil desarrollada con .NET MAUI Blazor Hybrid. Para probarla en Visual Studio, instalá la carga de trabajo de .NET MAUI y el SDK de Android. Seleccioná `Turnero.App` como proyecto de inicio y ejecutalo en un emulador o dispositivo Android.

La app consume la API publicada en MonsterASP. La URL base se configura en [MauiProgram.cs](Turnero.App/MauiProgram.cs). Para probar contra una API local, usá una URL que sea accesible desde el emulador o dispositivo.

Las grillas de informes consultan las rutas `/api/reportes/{tipo}/datos`. En producción requieren que esté desplegada una versión de la API que incluya esas rutas. La actualización de informes no requiere migraciones de base de datos.

## Verificación

```powershell
dotnet build trabajo_final.sln
dotnet run --project tests/VerificacionModelo
dotnet run --project tests/VerificacionAutenticacion
```

La verificación de API usa SQLite en memoria por defecto. Para probar las migraciones y la concurrencia con SQL Server y Kestrel, indicá una instancia de prueba con permiso para crear y eliminar bases. El verificador crea y limpia una base de nombre aleatorio.

```powershell
$env:TURNERO_SQLSERVER_PRUEBAS = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet run --project tests/VerificacionAutenticacion -- --sqlserver
```

La documentación sigue en proceso de finalización.

[Documentación del proyecto y diagramas de casos de uso](docs/Alzuri_Documentacion_proyectoFinal_completado.docx)

## Hitos a cumplir desde el front:

| Rol | Caso de uso | Pantalla o acción en la app | Estado |
| --- | --- | --- | --- |
| Todos | Iniciar y cerrar sesión. | Inicio de sesión y menú del rol | Realizado |
| Todos | Consultar el perfil propio. | Falta la pantalla de perfil | Pendiente |
| Todos | Cambiar la contraseña propia. | Falta el formulario de cambio de contraseña | Pendiente |
| Tutor | Consultar sus perros. | Inicio del tutor y menú de cada mascota | Realizado |
| Tutor | Consultar cupos y servicios. | Solicitar turno | Realizado |
| Tutor | Solicitar un turno con uno a diez servicios. | Solicitar turno → Confirmar | Realizado |
| Tutor | Modificar los servicios de un turno pendiente. | Falta la acción en Estados de turnos | Pendiente |
| Tutor | Cancelar un turno pendiente o aprobado. | Estados de turnos → Cancelar | Realizado |
| Tutor | Consultar el estado y el historial de sus turnos. | Estados de turnos y Consultar historial | Realizado |
| Veterinario | Gestionar tutores y teléfonos. | Listado de tutores → Detalle | Realizado |
| Veterinario | Gestionar perros y agregar o reemplazar sus fotos. | Detalle del tutor → Sus perros → Gestionar | Realizado |
| Veterinario | Quitar la foto de un perro. | Falta la acción en Editar perro | Pendiente |
| Veterinario | Consultar la ficha del perro y sus patologías. | Detalle del perro | Realizado |
| Veterinario | Asignar, editar y quitar patologías de un perro. | Faltan las acciones en Detalle del perro | Pendiente |
| Veterinario | Gestionar cupos diarios y consultar los turnos aprobados de cada fecha. | Cupos | Realizado |
| Veterinario | Gestionar los catálogos de servicios, tratamientos y patologías. | Falta el acceso para el rol Veterinario | Pendiente |
| Veterinario | Consultar, aprobar, rechazar y cancelar turnos según su estado. | Turnos → Detalle del turno | Realizado |
| Veterinario | Completar la atención y registrar los servicios realizados y las observaciones. | Detalle del turno → Completar | Realizado |
| Veterinario | Registrar tratamientos y sus motivos al completar un turno. | Falta la selección de tratamientos en Completar | Pendiente |
| Veterinario | Consultar, aplicar y levantar vetos. | Faltan las acciones en Detalle del tutor | Pendiente |
| Veterinario | Registrar una reserva presencial para un tutor con veto vigente. | Falta el formulario de reserva presencial | Pendiente |
| Administrador | Gestionar veterinarios. | Gestionar veterinarios | Realizado |
| Administrador | Gestionar catálogos. | Gestionar catálogos | Realizado |
| Administrador | Consultar movimientos de auditoría. | Consultar auditoría → Movimientos | Realizado |
| Administrador | Consultar errores de la API. | Consultar auditoría → Errores | Realizado |
| Administrador | Consultar informes por período y generar sus PDF. | Generar informes | Realizado |
