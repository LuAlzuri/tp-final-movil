# API del turnero de peluquería canina

## Imágenes de perros

MAUI envía `POST /api/perros/{id}/imagen` como `multipart/form-data`, con el archivo en el campo `archivo`. La subida y el borrado mediante `DELETE /api/perros/{id}/imagen` requieren una sesión de veterinario.

La ficha del perro devuelve `imagenUrl` como una ruta relativa o `null`. La app resuelve la ruta contra la URL base de la API. El archivo se sirve sin autenticación a cualquiera que tenga su URL.

Las imágenes se guardan en `wwwroot/uploads`. El despliegue requiere almacenamiento persistente en esa ruta, compartido si la API corre en varias instancias.
