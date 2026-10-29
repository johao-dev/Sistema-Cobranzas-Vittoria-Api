# StockProyecto: limpieza posterior a la inicialización

Fecha de verificación: 29/10/2026, America/Lima.

Se retiran la activación administrativa y su control previo. Se conservan la
distribución existente, la aplicación automática de diferencias, la validación
de saldos, las transacciones y el bloqueo exclusivo `Inventario:Movimientos`.
No se cambia la API pública ni se reconstruye stock desde el histórico.

## Objetos analizados

Se buscaron referencias en todo el repositorio y se contrastaron con
`sys.objects`, `sys.tables` y `sys.sql_expression_dependencies` de la base local
`VittoriaComprasDB_Dev`. La base tenía la inicialización activada. La validación
de DbUp se realizó sobre una copia desechable restaurada desde un backup
`COPY_ONLY`; no se aplicó la limpieza sobre la base original.

### Tablas y tipos

| Objeto de `almacen` | Uso y decisión |
| --- | --- |
| `StockProyectoInicializacion` | Marca administrativa de activación. Se elimina junto con sus restricciones `PK_StockProyectoInicializacion` y `CK_StockProyectoInicializacion_Unica`. |
| `StockProyecto` | Distribución operativa por material/proyecto. Se conserva íntegra, con su restricción de saldo no negativo y sus claves. |
| `KardexEntrada`, `KardexSalida`, `KardexSalidaDetalle`, `KardexStock` | Movimientos y stock global. Se conservan. |
| `TransferenciaProyecto`, `TransferenciaProyectoDetalle` | Reasignaciones operativas. Se conservan. |
| `TVP_StockProyectoCantidad` | Diferencias firmadas de entradas, salidas y transferencias. Se conserva aunque también lo utilizaba la inicialización. |
| `TVP_TransferenciaProyectoItem`, `TVP_KardexSalidaItem` | Contratos operativos. Se conservan. |
| Cuatro tablas `*_BKP_20261029_PreReinicioStockProyecto` | Backups administrativos de nombres variables. Se excluyen de la migración automática; se detallan más adelante. |

### Procedimientos auxiliares

| Procedimiento de `almacen` | Clasificación | Decisión |
| --- | --- | --- |
| `usp_StockProyecto_Inicializar` | Necesario únicamente durante inicialización; obsoleto tras la puesta en marcha. | Eliminar. Reemplazaba la distribución y marcaba su activación. |
| `usp_StockProyecto_ExigirInicializacion` | Bloqueo previo a inicialización; obsoleto tras la puesta en marcha. | Eliminar. Solo consultaba la marca administrativa. |
| `usp_StockProyecto_ValidarCambios` | Necesario durante operación normal. | Conservar. Exige transacción y bloqueo exclusivo, y evita saldos negativos. Retirar únicamente su llamada a `ExigirInicializacion`. |
| `usp_StockProyecto_AplicarCambios` | Necesario durante operación normal. | Conservar. Valida, actualiza o crea las filas por proyecto y verifica consistencia. |
| `usp_StockProyecto_VerificarConsistencia` | Necesario durante operación normal. | Conservar. Rechaza diferencias entre el stock global y la suma por proyecto para los materiales afectados. |

Los tres auxiliares permanentes podrían integrarse en cada operación, pero eso
duplicaría reglas compartidas. No se encontró una necesidad para reemplazarlos.

### Procedimientos operativos y de consulta

Se revisaron las tres operaciones de `usp_KardexEntrada_*`, las tres de
`usp_KardexSalida_*`, `usp_TransferenciaProyecto_Registrar`,
`usp_StockProyecto_Listar`, `usp_TransferenciaProyecto_Listar` y
`usp_Kardex_StockActual_Listar`. También se buscaron dependencias en todos los
repeatables, incluidas las vistas de Almacén y el trigger de especialidad del
material. No contienen otras definiciones de inicialización que deban retirarse.

En la base anterior, `sys.sql_expression_dependencies` confirmó referencias a los
objetos retirados desde las siete operaciones de escritura, `ValidarCambios`,
`Inicializar`, `ExigirInicializacion` y la restricción administrativa de la tabla.

### Errores y traducción

Se revisaron `Inventario/Domain/Model/CodigosErrorInventario.cs`,
`Inventario/Infrastructure/Persistence/TraductorErroresSql.cs`,
`Application/Common/SqlExceptionTranslator.cs` y sus pruebas.

| Código | Decisión |
| --- | --- |
| `STOCK_PROYECTO_NO_INICIALIZADO` / SQL 51115 | Retirado de SQL operativo y del catálogo C#. |
| `STOCK_PROYECTO_YA_INICIALIZADO` / SQL 51117 | Retirado de SQL operativo y del catálogo C#. |
| `STOCK_PROYECTO_INSUFICIENTE` / SQL 51114 | Conservado: disponibilidad del proyecto. |
| `STOCK_PROYECTO_INCONSISTENTE` / SQL 51116 | Conservado: igualdad entre stock global y distribución. |
| `CONTEXTO_INVENTARIO_INVALIDO` / SQL 51118 | Conservado: transacción y bloqueo obligatorio. |
| `PROYECTO_MOVIMIENTO_REQUERIDO` / SQL 51119 | Conservado: evita revertir movimientos históricos sin proyecto y dañar la distribución actual. |
| `INVENTARIO_OCUPADO`, `STOCK_INSUFICIENTE`, `STOCK_INCONSISTENTE_AL_ELIMINAR`, `PROYECTOS_IGUALES` | Conservados: reglas operativas permanentes. |

La traducción SQL funciona por rango y prefijo; no tenía traducciones específicas
de inicialización que eliminar. Las excepciones de negocio restantes se conservan.

### Servicios, repositorios, DTOs y endpoints

Se revisaron `StockProyectoService` / `IStockProyectoService`,
`KardexInventarioService` / `IKardexInventarioService`,
`StockProyectoRepository` / `IStockProyectoRepository`,
`KardexEntradaRepository` / `IKardexEntradaRepository`,
`KardexSalidaRepository` / `IKardexSalidaRepository` y
`KardexStockRepository` / `IKardexStockRepository`.

Se revisaron `StockProyectoController`, `KardexInventarioController`, los
validadores `KardexInventarioValidator` y `TransferenciaProyectoValidator`,
y los DTOs `KardexEntradaCreateDto`, `KardexEntradaResponseDto`,
`KardexSalidaCreateDto`, `KardexSalidaItemCreateDto`, `KardexSalidaResponseDto`,
`KardexStockActualResponseDto` y `TransferenciaProyectoCreateDto`.

No había servicios, repositorios, DTOs ni endpoints exclusivos de inicialización.
No se eliminó ninguno. Consultas, filtros y exportación mantienen sus contratos.

### Tests y migraciones

Se revisaron `StockProyectoTests`, `StockProyectoMigracionTests`,
`InventarioMigracionTests`, `NormalizacionInventarioTests`,
`KardexInventarioControllerTests`, `KardexInventarioExportTests`,
`KardexControllerTests`, `IntegrationTestBase` y las pruebas unitarias de
Inventario y traducción SQL. La suite completa también verifica los otros módulos.

Migraciones relacionadas revisadas:

- `V1_2_0__Almacen_EntradasSalidas_DDL.sql` y `V1_4_1__Almacen_Stock_Global.sql`.
- `V2_7_8__Inventario_Especialidad_Desde_Material.sql`.
- `V2_8_0__Inventario_TablasFiltroProyecto.sql`.
- `V2_8_1__Inventario_Inicializacion_Tipos.sql`.
- `R__Almacen_SPs.sql` y `R__Almacen_StockProyecto_SPs.sql`.
- `R__Almacen_Views.sql` y `R__Inventario_Material_Especialidad.sql`.

## Objetos eliminados

La nueva migración elimina físicamente solo:

1. `almacen.usp_StockProyecto_Inicializar`: la distribución inicial ya fue cargada.
2. `almacen.usp_StockProyecto_ExigirInicializacion`: desaparece la futura activación.
3. `almacen.StockProyectoInicializacion`: su única responsabilidad era esa marca.

Se retiran las dos constantes de error de activación y estas pruebas exclusivas:

- `Inicializacion_BloqueaTodosLosFlujosAntesDeActivar`.
- `Inicializacion_ExigeCargaCompleta_NoReconstruyeHistoricoYSeActivaUnaVez`.

`IntegrationTestBase` deja de ejecutar la activación después de cada reset.
`README.md` describe la operación permanente y enlaza este reporte.

## Objetos conservados

`ValidarCambios`, `AplicarCambios` y `VerificarConsistencia` siguen siendo parte
del modelo operativo. Se mantienen la restricción `Stock >= 0`, los TVP compartidos,
las validaciones de origen/destino, los errores operativos y todos los datos de
inventario. Las siete escrituras conservan `XACT_ABORT`, transacción,
`sp_getapplock`, rollback y propagación de errores.

## SP operativos modificados

En todas las filas se retira únicamente la llamada a
`almacen.usp_StockProyecto_ExigirInicializacion`.

| Procedimiento | Llamadas permanentes que quedan |
| --- | --- |
| `usp_KardexEntrada_Registrar` | `AplicarCambios`. |
| `usp_KardexEntrada_Actualizar` | `ValidarCambios` antes de persistir; `AplicarCambios` después. |
| `usp_KardexEntrada_Eliminar` | `ValidarCambios` antes de persistir; `AplicarCambios` después. |
| `usp_KardexSalida_Registrar` | `ValidarCambios` antes de persistir; `AplicarCambios` después. |
| `usp_KardexSalida_Actualizar` | `ValidarCambios` antes de persistir; `AplicarCambios` después. |
| `usp_KardexSalida_Eliminar` | `AplicarCambios`. |
| `usp_TransferenciaProyecto_Registrar` | `AplicarCambios` y `VerificarConsistencia` antes del commit. |

Además, `AplicarCambios` sigue llamando a `ValidarCambios` y
`VerificarConsistencia`. Se conserva la segunda verificación de la transferencia
porque ocurre después de persistir su cabecera y detalles, incluidos sus triggers.
No se introdujeron otros refactors.

## Migraciones

Repeatables modificados: `R__Almacen_SPs.sql` y
`R__Almacen_StockProyecto_SPs.sql`. No vuelven a crear los objetos retirados.

Nueva versionada:
`V2_8_2__Inventario_Retira_Inicializacion_StockProyecto.sql`.
Usa `DROP PROCEDURE IF EXISTS` y `DROP TABLE IF EXISTS` para los tres objetos
administrativos confirmados. Ninguna migración versionada histórica se modificó.
`V2_8_1` conserva la creación histórica de la tabla y de los TVP; en una instalación
desde cero, `V2_8_2` retira después la tabla y conserva los tipos operativos.

Las definiciones históricas necesarias para reproducir el upgrade se guardan en
`Cobranzas_Vittoria.Tests/Integration/Inventario/Fixtures/StockProyecto_PreLimpieza.sql`.
Es un recurso del ensamblado de tests. El proyecto de la API excluye esa carpeta;
DbUp de la aplicación no descubre ni ejecuta este fixture.

## Backups administrativos

Se confirmó la existencia en la base local de:

- `almacen.KardexEntrada_BKP_20261029_PreReinicioStockProyecto`.
- `almacen.KardexSalida_BKP_20261029_PreReinicioStockProyecto`.
- `almacen.KardexSalidaDetalle_BKP_20261029_PreReinicioStockProyecto`.
- `almacen.KardexStock_BKP_20261029_PreReinicioStockProyecto`.

No se eliminan mediante la migración versionada. Se proporciona el script separado
`Migrations/Manual/Inventario_Backups_PreReinicio_20261029_Limpiar.sql`, con esos
cuatro nombres explícitos, control de dependencias y transacción. DbUp no ejecuta
`Migrations/Manual`. Su uso administrativo requiere revisar que esos respaldos
ya no sean necesarios. Backups con otras fechas o nombres requieren su propia
revisión; el script no los toca.

## Verificación

| Verificación | Resultado |
| --- | --- |
| `dotnet build backend.sln --no-restore` | Correcto: 0 errores; 38 advertencias existentes de documentación XML, nulabilidad y métodos async sin await. |
| `dotnet test backend.sln --no-build --no-restore` | 1.188 correctos, 0 fallidos, 0 omitidos. SQL Server 2025 con Testcontainers. |
| `StockProyectoTests` | 32 casos correctos: entradas, modificaciones, eliminaciones, salidas, transferencias, filtros, concurrencia, saldo insuficiente, consistencia, contexto de bloqueo y rollback. |
| `StockProyectoMigracionTests` | 2 casos correctos: base vacía operativa sin activación y base inicializada con datos/transferencias/backups preservados. También verifican ausencia de referencias obsoletas, reejecución de repeatables y bitácora versionada única. |
| DbUp sobre copia del estado real | Correcto. Primera ejecución: solo V2.8.2 entre las versionadas. Segunda ejecución: 0 versionadas pendientes. Repeatables correctos en ambas ejecuciones. |
| Preservación del inventario real | Huellas SHA-256 de las filas serializadas de 11 tablas idénticas antes/después: 7 tablas operativas y 4 backups. 0 referencias remanentes a inicialización y 0 diferencias entre stock global y suma por proyecto. |
| Script manual de backups en otra base desechable | Correcto: una dependencia impide borrar; sin ella elimina solo los cuatro nombres; conserva un backup de otro nombre; admite reejecución. |
| `git diff --check` | Correcto. |

Los dos tests de migración ejecutan todos los repeatables actuales, usando
`NullJournal`, en el mismo orden versionadas/repeatables que `Program.cs`.
El caso inicializado reproduce procedimientos anteriores reales y ejecuta los
siete movimientos después del upgrade. Los tests de operación conservan la
cobertura de rollback y consistencia previamente existente.

Evidencias locales de esta ejecución:

- Build: `/tmp/vittoria-stock-build-final.log`.
- Suite: `/tmp/vittoria-stock-tests.log`.
- Resultados TRX: `/tmp/vittoria-stock-test-results/stock-post-inicializacion.trx`.
- Copia del estado real: `/tmp/vittoria-stock-dbup-real.log`.
- Script administrativo: `/tmp/vittoria-stock-manual-validacion.log`.

### Riesgos y pendientes

La migración está preparada y validada; la base local original conserva sus objetos
administrativos y sus cuatro backups. La copia temporal de validación y su archivo
de backup se eliminaron al terminar.

DbUp ejecuta las versionadas antes de los repeatables. El despliegue debe completar
ambas fases antes de admitir escrituras, porque los procedimientos anteriores
todavía referencian la activación durante ese intervalo. La aplicación ya ejecuta
esas fases antes de empezar a servir peticiones; si hay otras instancias activas,
hay que coordinar su parada durante el despliegue.

No se cargan distribuciones automáticamente: una base antigua con stock positivo
sin regularizar no es el estado de despliegue de esta limpieza. Una base nueva
vacía opera directamente. La eliminación administrativa de los backups reales
queda separada del despliegue, según lo solicitado.
