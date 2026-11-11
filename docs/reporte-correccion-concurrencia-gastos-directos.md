# Corrección de concurrencia en Gastos Directos

Fecha: 11 de noviembre de 2026.

**Resultado final: suite completa con 1.570 casos aprobados, cero fallos y cero omitidos.** Incluye dos pruebas nuevas de regresión que fallan con el SQL original. Se conservan también los resultados fallidos de la investigación; los cierres intermitentes del contenedor SQL son un problema independiente que no se considera corregido.

## Causa reproducida

Se reprodujeron las dos respuestas HTTP 500 informadas en:

- `Concurrencia_ConfirmarDosVecesTrasRevision_CreaUnaEjecucion`.
- `Concurrencia_ConfirmarYAnularTrasRevision_NoDejaEjecucionNeta`.

La excepción original es **SQL Server 1205: víctima de interbloqueo**. Se capturaron los cuerpos de las respuestas y los eventos `xml_deadlock_report` de `system_health` antes de que se destruyera el contenedor. La reproducción completa con diagnóstico terminó con 18 fallos: estos dos interbloqueos y otros 16 por el arranque de un contenedor de Notificaciones.

El gráfico muestra dos sesiones con niveles distintos:

| Sesión | Recurso retenido | Recurso esperado |
| --- | --- | --- |
| `SERIALIZABLE` | Bloqueo compartido `S` en `contable.GastoDirecto`, adquirido al localizar su presupuesto. | Bloqueo `U` de `ControlPresupuestario.Presupuesto`. |
| `READ COMMITTED` | Bloqueo `U` del presupuesto. | Conversión a `X` sobre el mismo gasto para actualizar su detalle/estado. |

La sesión serializable conserva el bloqueo de la lectura inicial hasta finalizar la transacción. Eso introduce el orden gasto → presupuesto, aunque los procedimientos pretendían usar presupuesto → gasto. Cada sesión espera a la otra y SQL Server aborta una de ellas.

El comportamiento depende de las conexiones disponibles en el pool. El proyecto utiliza transacciones `Serializable` en la importación de gastos y en Notificaciones. La documentación de [aislamiento y conexiones agrupadas de ADO.NET](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql/snapshot-isolation-in-sql-server#managing-concurrency-with-isolation-levels) explica que una conexión reutilizada puede conservar su aislamiento. La evidencia local confirma que una de las solicitudes estaba realmente en `serializable (4)`; no se atribuye una conexión física concreta a un módulo emisor sin trazarla.

## Corrección SQL

Archivo: `Cobranzas_Vittoria/Migrations/Repeatable/2_StoredProcedures/R__Contable_GastoDirecto_SPs.sql`.

En `contable.usp_GastoDirecto_Confirmar` y `contable.usp_GastoDirecto_Anular`, la consulta inicial de localización aplica `WITH (READCOMMITTEDLOCK)` a:

- `contable.GastoDirecto`.
- `ControlPresupuestario.PresupuestoDetalle`.
- `ControlPresupuestario.PresupuestoVersion`.

Así esa lectura libera sus bloqueos compartidos al terminar, aun si la sesión llegó con aislamiento `SERIALIZABLE`. La modificación se limita a localizar el presupuesto; después se mantienen el mutex del presupuesto, el bloqueo del gasto y las revalidaciones de referencias, estados y saldo.

La semántica de liberación de estos bloqueos está documentada en [READCOMMITTEDLOCK, Microsoft Learn](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17#readcommittedlock). No se utiliza lectura sucia, no se cambia el aislamiento general de las conexiones y no se introducen reintentos.

Es un script repetible ya integrado en el arranque. **Para aplicar el cambio a la base del entorno de desarrollo hay que recompilar y reiniciar la API.** En esta sesión se aplicó únicamente en las bases aisladas de las pruebas; no se modificó una base empresarial.

## Pruebas que fuerzan el problema

En `Cobranzas_Vittoria.Tests/Integration/Contable/GastosDirectosVersionesTests.cs` se agregaron dos casos de `Concurrencia_LocalizacionSerializable_RespetaPresupuestoAntesDelGasto`:

1. Dos conexiones y transacciones explícitas, una `ReadCommitted` y otra `Serializable`.
2. La primera adquiere el mutex del presupuesto.
3. La segunda empieza a confirmar o anular el gasto y se comprueba mediante las DMV que espera a la primera sesión.
4. La primera confirma el gasto; después se liberan las transacciones en orden.
5. Se verifican el estado, una única ejecución, el ajuste de anulación cuando corresponde y el saldo final.

**Con el SQL original, ambos casos fallaron con SQL 1205. Con el SQL corregido, ambos pasaron**, junto con las dos pruebas HTTP originales: cuatro casos aprobados. Las repeticiones temporales usadas para investigar la intermitencia fueron retiradas del código final.

Las aserciones de los tres escenarios HTTP concurrentes de revisión ahora incluyen URL, estado y cuerpo de ambas respuestas. Un futuro 500 podrá mostrar el error original en el fallo de NUnit, sin tener que buscarlo entre todos los logs.

## Consola y archivos de diagnóstico

Se añadió `Cobranzas_Vittoria.Tests/Setup/LogMigracionPrueba.cs`, una implementación de `IUpgradeLog` de DbUp. Escribe el detalle completo en un archivo temporal y lo registra como adjunto NUnit, en lugar de enviar toda la migración a la consola.

Se sustituyeron las ocho llamadas a `LogToConsole()` de las pruebas de migración de:

- `Compras/NormalizacionMigracionesTests.cs`.
- `Contable/MigracionGastosHistoricosTests.cs`.
- `Inventario/InventarioMigracionTests.cs`.
- `Inventario/StockProyectoMigracionTests.cs`.

Se conservan las excepciones y las llamadas existentes a `LogScriptOutput`, incluida la evidencia de auditoría SQL. Los logs originales están en el directorio temporal `vittoria-pruebas/migraciones`; VSTest también recoge sus adjuntos con los resultados. Los mensajes de compilación preexistentes y los fallos reales de pruebas siguen apareciendo. El logging productivo de la API no se cambió.

## Problema independiente de contenedores

También se reprodujeron cierres de SQL Server durante `OneTimeSetUp`:

- `AppLoader: Failed to load LSA: 0xc0070102` en algunas ejecuciones.
- Un fallo fatal SQLPAL, razón `0x00000002`, con stack en `samsrv.dll`/`lsasrv.dll`/`lsass.exe`, en otra ejecución.

Después del cierre, el script `handle-crash.sh` intenta capturar diagnósticos y genera mensajes `Permission denied` sobre `/proc` y otras rutas. Docker también informa `container ... is not running`. En las capturas de esta sesión esos mensajes aparecen después del fallo fatal; por sí solos no acreditan un problema de permisos del socket Docker.

Una variante de `NotificacionesPersistenceTests` tiene 16 casos con el mismo `OneTimeSetUp`; un único cierre de su contenedor produjo exactamente 16 fallos. Cuando falló el contenedor compartido de Integration y una variante de Notificaciones, hubo 709 fallos derivados del arranque.

El interbloqueo de Gastos Directos está corregido y tiene regresión determinista. **La causa interna de los cierres del motor SQL durante el arranque sigue sin determinarse.** No se modificaron permisos del host, opciones de seguridad de Docker ni el usuario de los contenedores.

Entorno registrado: SDK .NET 8.0.132, Docker 29.9.3, kernel `7.3.3-300.fc45.x86_64`, 12 CPU y aproximadamente 15 GiB de RAM reportados por Docker. Imagen existente `mcr.microsoft.com/mssql/server:2025-latest`.

## Resultados y evidencia

| Ejecución | Total | Aprobados | Fallidos | Diagnóstico |
| --- | ---: | ---: | ---: | --- |
| Pruebas HTTP aisladas, con repetición temporal | 2 | 2 | 0 | No reprodujo la dependencia del pool del conjunto completo. |
| Suite previa, primer intento | 1.568 | 859 | 709 | Cierres de SQL Server en setup. |
| Suite previa con captura de deadlocks | 1.568 | 1.550 | 18 | Dos SQL 1205 y 16 fallos de arranque de SQL Server. |
| Regresión determinista antes del cambio | 2 | 0 | 2 | Ambos SQL 1205. |
| Regresión y pruebas HTTP después del cambio | 4 | 4 | 0 | Confirmación y anulación correctas. |
| Primera suite con corrección | 1.570 | 1.554 | 16 | Todos los casos de concurrencia corregidos pasaron; una variante de Notificaciones no arrancó. |
| Repetición completa con corrección | 1.570 | 1.570 | 0 | Contenedores arrancados; todas las pruebas aprobadas, sin omisiones. |

La última ejecución registró además 66 adjuntos con diagnósticos de migración. La compilación terminó correctamente; las advertencias observadas son preexistentes. `git diff --check` pasó.

[Resumen de ejecuciones y gráficos de interbloqueo](evidencias/gastos-directos-concurrencia-validacion.json). Los TRX completos y logs locales se guardaron en `/tmp/vittoria-concurrencia/` y `/tmp/vittoria-concurrencia-*.log`; el gráfico original se conservó en `/tmp/vittoria-concurrencia/deadlock-original.xml`.

Comandos principales desde la raíz del backend:

```bash
# Con el SQL original: ambos casos nuevos fallaron con SQL 1205.
dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-restore --filter 'Name~Concurrencia_LocalizacionSerializable' \
  --logger 'trx;LogFileName=regresion-antes.trx' --results-directory /tmp/vittoria-concurrencia

# Con el SQL corregido: las dos regresiones y los dos casos HTTP originales pasaron.
dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-restore \
  --filter 'Name~Concurrencia_LocalizacionSerializable|Name~Concurrencia_ConfirmarDosVecesTrasRevision|Name~Concurrencia_ConfirmarYAnularTrasRevision' \
  --logger 'trx;LogFileName=regresion-despues.trx' --results-directory /tmp/vittoria-concurrencia

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-restore --logger 'trx;LogFileName=suite-corregida.trx' \
  --results-directory /tmp/vittoria-concurrencia

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-build --no-restore --logger 'trx;LogFileName=suite-corregida-repeticion.trx' \
  --results-directory /tmp/vittoria-concurrencia

git diff --check
```

Las ejecuciones se redirigieron a logs locales para conservar las excepciones completas. VSTest y Docker requirieron ejecución autorizada fuera del sandbox.
