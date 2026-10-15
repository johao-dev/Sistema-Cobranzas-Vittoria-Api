# Gestión de ítems de requerimientos en almacén

La parte migrada de Compras usa `Presentation → Application → Domain`; `Infrastructure`
implementa el puerto `IRequerimientoRepository`. `AddCompras()` registra las dependencias.
El resto de Requerimientos continúa en la implementación legacy.

Los tres endpoints requieren `requerimientos.gestionar_items_almacen` y un requerimiento
en estado `EnviadoAlmacen`:

| Método | Ruta | Respuesta |
| --- | --- | --- |
| POST | `/api/compras/requerimientos/{id}/items` | 201 e ítem persistido |
| PATCH | `/api/compras/requerimientos/{id}/items/{idDetalle}` | 200 e ítem persistido |
| DELETE | `/api/compras/requerimientos/{id}/items/{idDetalle}` | 204 |

POST admite `idMaterial`, `cantidad`, `idPresupuestoDetalle` opcional y `observacion`
opcional. PATCH admite únicamente `cantidad`, `idPresupuestoDetalle` y `observacion`:
omitir conserva; null limpia partida/observación; cantidad nula y PATCH vacío se rechazan.
El material es inmutable. La presencia de campos se transmite como `CamposItemRequerimiento`
hasta SQL para conservar cambios concurrentes sobre columnas omitidas.

El dominio valida las reglas del documento antes de persistir. Los SP vuelven a comprobar
estado, referencias, pertenencia, unicidad y último ítem bajo un bloqueo transaccional
del requerimiento padre. La lectura de cabecera e ítems usa el mismo mutex. La validación
de almacén y la edición legacy del borrador también participan en ese protocolo.
Las escrituras devuelven la fila persistida en su propia transacción, sin una segunda lectura.

Los errores de dominio se traducen en el middleware: 400 para entrada inválida, 404 para
recurso ausente y 409 para conflicto. Solo se traducen los errores SQL 51460–51469 del
contrato de ítems; errores técnicos y cancelaciones se propagan.

La migración V2.7.5 crea la capacidad para Almacenero y Administrador y añade las
restricciones de cantidad positiva y material único por requerimiento. Si existen datos
históricos incompatibles, exige conciliarlos y no borra filas. No amplía automáticamente
los permisos de roles personalizados. El permiso y endpoint anteriores de cantidades se
conservan para permitir la transición del frontend.

Pruebas relevantes: `Unit/Compras`, `RequerimientoItemRepositoryTests`,
`RequerimientoItemControllerTests` y las regresiones de Requerimientos y RBAC.
La configuración global de NUnit inicia un SQL Server desechable mediante Testcontainers.
