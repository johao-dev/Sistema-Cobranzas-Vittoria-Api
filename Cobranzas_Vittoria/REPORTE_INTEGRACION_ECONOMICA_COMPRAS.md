# Reporte de implementación — Integración económica de Compras

Fecha de cierre: 21 de septiembre de 2026

## Resultado

Se implementó el bloque final de integración entre Compras, Control Presupuestario y Almacén. El flujo cubierto es:

1. Una Orden de Compra nace en `REGISTRADA`.
2. Al pasar a `APROBADA`, registra `COMPROMISO` por material y partida.
3. Una OC aprobada puede modificarse. Cada diferencia económica registra un nuevo `COMPROMISO` o una `LIBERACION`, con una versión y una clave de evento distintas.
4. Una OC aprobada puede anularse. La anulación libera el compromiso vigente.
5. Se permite una única Compra definitiva por OC y debe reproducir exactamente sus materiales y cantidades.
6. Al aceptar la Compra, en una sola transacción se registran `LIBERACION` del compromiso, `EJECUCION`, entrada de Kardex, estado `ACEPTADA` de Compra, estado `ATENDIDA` de OC e historial.
7. La OC `ATENDIDA` puede cerrarse manualmente como `CERRADA`.

No se implementó compra parcial ni reversión, conforme a las decisiones funcionales recibidas.

## Cambios de base de datos

### Migración versionada V2.2.0

Archivo: `Migrations/Versioned/V2_2_0__Integracion_Economica_Estructura.sql`

- Agrega `ControlPresupuestario.CentroCosto.IdProyecto`, su FK a `maestra.Proyecto` y un índice único filtrado. Esto establece una relación inequívoca entre el presupuesto y el proyecto de Compras.
- Agrega `compras.OrdenCompra.VersionEconomica` para identificar ajustes sucesivos de una OC aprobada.
- Agrega `compras.Compra.Estado` con los valores `REGISTRADA` y `ACEPTADA`, manteniendo sincronía con el booleano legado `Aceptada`.
- Normaliza los estados de OC a `REGISTRADA`, `APROBADA`, `ATENDIDA`, `CERRADA` y `ANULADA`.
- Crea la restricción de una sola Compra por OC.
- Crea `compras.TVP_MovimientoEconomico` para registrar lotes económicos atómicos.
- La migración es reintentable y se detiene sin eliminar datos si encuentra estados históricos ambiguos (`51401`) o más de una Compra por OC (`51402`).

### Procedimiento económico común

Archivo: `Migrations/Repeatable/2_StoredProcedures/R__Compras_Integracion_Economica.sql`

Se agregó `compras.usp_IntegracionEconomica_RegistrarLote`, que:

- valida proyecto, moneda, partida y versión presupuestaria;
- admite partidas de una versión `APROBADO` o `HISTORICO` para conservar operaciones ya iniciadas;
- rechaza que una operación nueva deje saldo negativo;
- serializa por presupuesto y partida mediante `sp_getapplock`, evitando que dos aprobaciones concurrentes consuman el mismo saldo;
- registra el lote dentro de la transacción llamadora y revierte el conjunto completo ante cualquier error;
- usa `ClaveEvento` para que cada evento económico tenga identidad estable y no se duplique.

### Procedimientos de Compras modificados

Archivo: `Migrations/Repeatable/2_StoredProcedures/R__Compras_SPs.sql`

- `usp_OrdenCompra_ActualizarEstado`: aplica la máquina de estados, genera compromiso al aprobar, liberación al anular e historial.
- `usp_OrdenCompra_Actualizar`: limita la edición según estado, protege identidad de OC aprobada y registra diferencias económicas versionadas.
- `usp_OrdenCompra_CrearDesdeRequerimiento`: conserva la creación normalizada con moneda explícita.
- `usp_Compra_Registrar`: exige OC aprobada, una Compra por OC y coincidencia completa de materiales y cantidades.
- `usp_Compra_Aceptar`: ejecuta el cierre económico y de almacén en una transacción atómica e idempotente.
- Los procedimientos de lectura de OC y Compra exponen los estados reales.
- Los procedimientos de crear y actualizar Requerimiento validan que cada partida pertenezca a la versión aprobada vigente, al proyecto y al Centro de Costo correspondiente.

### Control Presupuestario

Los repeatables de maestros y lecturas ahora reciben y devuelven `IdProyecto` en Centro de Costo. Para centros de tipo `PROYECTO`, el identificador es obligatorio en nuevas altas; además, se valida que el proyecto esté activo y no esté asociado a otro Centro de Costo.

## Cambios de aplicación y contratos HTTP

No se eliminó ninguna ruta. Se agregó un contrato y se modificó el comportamiento de siete contratos existentes: ocho métodos HTTP afectados en total.

| Contrato | Cambio observable |
|---|---|
| `POST /api/compras/compras/{id}/aceptar` | Nuevo. Acepta `{ "idUsuario": int?, "observacion": string? }`; realiza presupuesto, Kardex y estados de forma atómica. Repetir la aceptación ya completada es idempotente. |
| `POST /api/compras/compras` | Ahora solo admite una OC `APROBADA`, una Compra por OC y materiales/cantidades exactamente iguales a la OC. Los totales se calculan en servidor desde los items; los totales enviados en el DTO ya no gobiernan el registro. |
| `GET /api/compras/compras` | `Estado` deja de ser el literal fijo `Comprado` y devuelve `REGISTRADA` o `ACEPTADA`; también expone `Aceptada`. |
| `GET /api/compras/compras/{id}` | Devuelve el estado real de la Compra y el booleano de compatibilidad. |
| `PATCH /api/compras/ordenes-compra/{id}/estado` | Solo permite las transiciones definidas. Aprobar compromete saldo, anular libera y cerrar requiere una OC atendida. |
| `PUT /api/compras/ordenes-compra/{id}` | Solo edita OC `REGISTRADA` o `APROBADA`. En aprobadas no permite cambiar requerimiento/moneda ni invalidar una Compra existente; los cambios de importe ajustan el ledger. |
| `POST` y `PUT /api/compras/requerimientos` | Las partidas deben corresponder al proyecto y a la versión presupuestaria aprobada vigente. |

Los errores SQL de negocio del rango `51400–51499` ahora se traducen a HTTP `409 Conflict`, con un código estable tomado del prefijo del mensaje. Los errores SQL no clasificados conservan el tratamiento general existente.

La aplicación incorporó el DTO `CompraAceptarDto`, el nuevo método en controlador/servicio/repositorio y el campo `Estado` en la entidad `Compra`. El registro de Compra pasó de SQL construido en el repositorio al procedimiento almacenado transaccional.

## Integridad y reglas implementadas

- No hay compromiso sin partida presupuestaria demostrada.
- Proyecto de Requerimiento, proyecto de Centro de Costo y moneda de OC/presupuesto deben coincidir.
- Una nueva operación usa una versión presupuestaria aprobada; una operación económica ya iniciada puede continuar sobre su versión histórica.
- El saldo se valida sobre presupuesto menos comprometido menos ejecutado, incorporando el impacto completo del lote en curso.
- La aprobación sin saldo hace rollback y deja la OC en `REGISTRADA`.
- La aceptación fallida no deja movimientos, Kardex ni estados parciales.
- La aceptación genera claves distintas por Compra, material y tipo (`LIBERACION`/`EJECUCION`).
- Una Compra registrada bloquea cambios de materiales/cantidades de la OC, pero permite un cambio de precio compatible; una Compra aceptada bloquea la edición económica.
- No se generan movimientos históricos automáticamente porque no existe evidencia suficiente para inferirlos con seguridad.

## Conciliación histórica y despliegue

Se agregó `Migrations/Manual/Conciliacion_Historica_Integracion_Economica.sql`. Es deliberadamente de solo lectura y lista:

- estados de OC no canónicos;
- OC con múltiples Compras;
- líneas sin partida;
- Centros de Costo sin proyecto;
- incompatibilidades de proyecto o moneda;
- eventos económicos esperados y movimientos ya existentes.

Para producción se debe ejecutar primero este script sobre una copia restaurada, documentar las decisiones y corregir los datos con evidencia. Después se ejecuta DbUp. Si V2.2.0 arroja `51401` o `51402`, se concilian los registros reportados y se vuelve a iniciar la aplicación; la migración está preparada para ese reintento.

El proceso no reconstruye movimientos históricos de forma automática. Si se decide cargarlos, cada inserción debe tener respaldo documental y `ClaveEvento` única.

## Pruebas

La validación se ejecutó contra SQL Server local en Docker mediante Testcontainers; no se utilizó una base de producción.

- Pruebas específicas del flujo económico: aprobación y saldo, edición con aumento/disminución, anulación, aceptación, cierre, idempotencia, moneda/proyecto, versión histórica y compatibilidad con Compra registrada.
- Pruebas de migración desde V2.0.9: 9/9 aprobadas, incluidos estados ambiguos, compras duplicadas, reintentos y conservación de datos.
- Suite completa: **820 aprobadas, 0 fallidas, 0 omitidas**.
- `git diff --check`: sin errores de espacios o marcadores de conflicto.
- Compilación: correcta; permanecen advertencias de nulabilidad preexistentes en el proyecto de pruebas.

## Alcance pendiente deliberado

- No se agregó identificación del usuario autenticado ni nuevos permisos RBAC; `IdUsuario` continúa siendo opcional según la decisión funcional.
- No existe compra parcial, reversión ni reapertura de una OC cerrada/anulada.
- La conciliación de producción requiere una decisión humana basada en documentación histórica.
