**Reporte de implementación: confirmación de gastos directos entre versiones de presupuesto**

Fecha: 30 de octubre de 2026.

La confirmación de un gasto pendiente ahora resuelve el detalle equivalente de la
versión `APROBADO` del mismo presupuesto. El gasto y su movimiento `EJECUCION`
quedan asociados al mismo detalle dentro de una única transacción. Aprobar una
nueva versión deja de bloquear por sí solo los gastos registrados anteriormente.

El error original se producía porque `usp_GastoDirecto_Confirmar` exigía que la
versión vinculada al gasto continuara en estado `APROBADO`. Al aprobar una revisión,
la versión anterior pasaba a `HISTORICO` y los gastos `REGISTRADO` conservaban su
detalle original. La confirmación rechazaba esa combinación antes de comprobar
el saldo vigente.

El comportamiento implementado es el siguiente:

| Situación | Resultado |
| --- | --- |
| Gasto registrado en v9 y presupuesto vigente v10 | Busca la misma partida en v10, valida saldo y confirma gasto y ejecución contra ese detalle. |
| Transcurrieron varias versiones desde el registro | Resuelve directamente la versión actualmente aprobada. |
| Gasto ya confirmado antes del cambio de versión | Conserva su detalle y movimiento originales; repetir la confirmación no duplica ejecución. |
| Anulación posterior a otras revisiones | Registra un único ajuste de decremento sobre el detalle donde se confirmó. |
| Falta saldo en la versión vigente | Rechaza sin cambiar el detalle original, el estado ni los movimientos del gasto pendiente. |
| La partida solamente existe en otro presupuesto | Rechaza; no utiliza detalles de otro `IdPresupuesto`. |
| No existe una única versión aprobada | Rechaza sin seleccionar arbitrariamente una versión. |
| El detalle de origen pertenece a `BORRADOR` o `ANULADO` | Rechaza; no convierte automáticamente esa referencia en una imputación aprobada. |
| Nueva versión omite una partida con gastos pendientes | La aprobación devuelve `PARTIDA_RELEVANTE_OMITIDA` y conserva los estados de las versiones. |
| La partida pendiente se conserva con monto cero | Permite aprobar la revisión, pero la confirmación continúa sujeta al saldo disponible. |

Los cambios se concentran en estos archivos:

| Archivo | Cambio |
| --- | --- |
| `Migrations/Repeatable/2_StoredProcedures/R__Contable_GastoDirecto_SPs.sql` | Resolución del detalle vigente al confirmar; actualización atómica del detalle y estado; orden de bloqueos coordinado en confirmación y anulación. |
| `Migrations/Repeatable/2_StoredProcedures/R__ControlPresupuestario_SPs.sql` | Cobertura de partidas con gastos `REGISTRADO` vinculados a versiones aprobadas o históricas al aprobar una nueva versión. |
| `Cobranzas_Vittoria.Tests/Integration/Contable/GastosDirectosControllerTests.cs` | Renombra el caso histórico sin versión aprobada para expresar el motivo real del rechazo y declara la fixture no paralelizable. |
| `Cobranzas_Vittoria.Tests/Integration/Contable/GastosDirectosVersionesTests.cs` | Añade 18 casos de integración sobre continuidad, errores, transacciones, idempotencia y concurrencia. |

La identidad presupuestaria se obtiene del detalle original mediante
`IdPresupuesto` e `IdCatalogoPartida`. La consulta de destino exige una única
versión `APROBADO`; no elige la versión por el mayor número, ya que podría existir
un borrador posterior. Se mantienen las validaciones de presupuesto activo,
partida activa y hoja, moneda y saldo acumulado.

La confirmación, la anulación y la aprobación coordinan sus escrituras mediante
el bloqueo `UPDLOCK, HOLDLOCK` de la fila de presupuesto. Confirmación y anulación
toman ese bloqueo antes del bloqueo de actualización del gasto. Tras adquirirlo,
releen la referencia del gasto y rechazan con `CONCURRENCIA` si una edición lo
cambió a otro presupuesto durante la localización inicial. Se conserva también
el bloqueo por presupuesto y partida que ya protegía la comprobación de saldo.
La anulación se ajustó para evitar invertir ese orden frente a una confirmación.

La búsqueda del detalle vigente se aplica solamente a `REGISTRADO`. Un reintento
de un gasto `CONFIRMADO` utiliza el detalle persistido y la misma clave de evento.
Si falla el registro del movimiento o cualquier validación, se revierte la
operación. Se verificó además que un rollback de una transacción externa revierte
tanto la reasignación como la ejecución.

Los reportes conservan sus cálculos actuales. El control vigente acumula los
movimientos aprobados e históricos; las consultas por versión atribuyen la nueva
ejecución a la versión en la que se confirmó. No se trasladan movimientos ya
confirmados al aprobar otras versiones. El detalle original de un gasto pendiente
se reemplaza al confirmarlo correctamente; no se añadió una columna separada para
conservar su referencia de registro inicial.

Se ejecutó la compilación del proyecto de pruebas:

```bash
dotnet build Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj --no-restore --nologo --verbosity quiet
```

Resultado: **0 errores**, con advertencias preexistentes de documentación XML,
nulabilidad y métodos asíncronos en otros archivos del proyecto.

La verificación final ejecutó todas las pruebas de integración de Contable y
Control Presupuestario contra SQL Server 2025 en un contenedor temporal de
Testcontainers, incluyendo la aplicación real de migraciones DbUp:

```bash
dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Integration.Contable|FullyQualifiedName~Integration.ControlPresupuestario' --logger 'trx;LogFileName=gastos-versiones-final.trx' --results-directory /tmp/vittoria-gastos-versiones --nologo --verbosity quiet
```

Resultado final: **214 aprobadas, 0 fallidas, 0 omitidas**. La ejecución de pruebas
informó una duración de 24 segundos, sin contar la preparación del entorno.
Evidencia local: `/tmp/vittoria-gastos-versiones/gastos-versiones-final.trx` y
`/tmp/vittoria-gastos-versiones-final.log`.

Los casos nuevos verifican el cambio de una y de tres revisiones antes de
confirmar; el saldo acumulado tras ejecuciones históricas; el aislamiento entre
presupuestos; el rechazo de orígenes no aprobados y de versiones inconsistentes;
los reintentos; la anulación; el rollback externo; y la cobertura de partidas con
pendientes actuales o históricos. Las pruebas concurrentes comprueban ambos
órdenes de aprobación y confirmación esperando el bloqueo real observado en SQL
Server, dos confirmaciones del mismo gasto, dos gastos que compiten por un saldo
insuficiente para ambos, y confirmación frente a anulación.

Para desplegar, se debe compilar y publicar la API actualizada y ejecutar su
arranque habitual: `Program.cs` sincroniza estos scripts repetibles mediante
`CREATE OR ALTER`. No se requieren nuevas tablas ni una migración masiva de datos.
Los gastos históricos que sigan pendientes podrán confirmarse por el endpoint o
SP habitual cuando exista su partida equivalente y saldo suficiente. Los gastos
que ya fueron reasignados con la solución temporal también son compatibles.

La modificación está implementada y verificada en el repositorio. No se desplegó
la aplicación ni se ejecutaron cambios contra la base de datos operativa del
usuario. La ejecución cubrió Contable y Control Presupuestario; no se ejecutó la
suite completa del repositorio.
