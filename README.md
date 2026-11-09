# API del turnero de peluquería canina

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
