# Cobranzas_Vittoria API Final

API .NET 8 con arquitectura por carpetas (n-capas dentro de un proyecto):
- Controllers
- Services
- Repositories
- Interfaces
- DTOs
- Entities
- Data
- Middleware

## Desarrollo con Docker

Este proyecto ahora puede ejecutarse sin instalar .NET 8 ni SQL Server en el host.
El flujo recomendado es editar el código desde el host y ejecutar la API o los tests
desde contenedores Docker.

### Servicios disponibles

- `db`: SQL Server 2025 para desarrollo local.
- `app`: contenedor con .NET 8 SDK para correr la API con `dotnet watch`.
- `test`: contenedor con .NET 8 SDK preparado para ejecutar `dotnet test` y dar soporte a `Testcontainers`.

### Primer arranque

Desde esta carpeta:

```bash
docker compose up -d --build db app
```

En el primer arranque la API ahora:

1. espera a que SQL Server acepte conexiones,
2. crea `VittoriaComprasDB_Dev` si todavía no existe,
3. aplica las migraciones DbUp.

La API quedará disponible en:

- `http://localhost:5000/swagger`

### Ver logs de la API

```bash
docker compose logs -f app
```

### Detener el entorno

```bash
docker compose down
```

Si también quieres eliminar volúmenes:

```bash
docker compose down -v
```

## Ejecutar comandos .NET dentro del contenedor

Como el código fuente se monta desde el host, puedes seguir editando normalmente y
ejecutar comandos desde Docker:

```bash
docker compose exec app dotnet restore
docker compose exec app dotnet build
```

## Ejecutar tests dentro de Docker

Los tests de integración usan `Testcontainers`, por lo que el servicio `test` monta
el socket Docker del host para poder levantar contenedores auxiliares durante la
ejecución. Ese servicio se ejecuta como `root` para evitar problemas de permisos
contra `/var/run/docker.sock`.

Ejecuta:

```bash
docker compose run --rm --profile test test
```

Si quieres lanzar un comando de prueba distinto:

```bash
docker compose run --rm --profile test test bash -lc "dotnet test ./Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj --filter FullyQualifiedName~Swagger"
```

## Detalles importantes del entorno

1. El `compose.yml` sobrescribe la cadena de conexión de desarrollo para que la API
   apunte al servicio `db` dentro de la red Docker.
2. `Program.cs` espera a que SQL Server acepte conexiones, crea la base si falta y
   luego ejecuta DbUp. Esto evita fallos de arranque en el primer boot.
3. `wwwroot/uploads` se monta desde el host para que los archivos subidos no se
   pierdan al recrear el contenedor `app`.
4. `dotnet watch` usa sondeo de archivos para detectar cambios desde bind mounts.

## Archivos Docker relevantes

- `Dockerfile`
- `compose.yml`
- `.dockerignore`

## Nota sobre devcontainer

No se agregó un `devcontainer.json` porque el flujo objetivo de este repositorio no
depende de abrir el proyecto dentro de un contenedor desde el editor. Aquí el flujo
es: editar en el host y ejecutar dentro de Docker.
