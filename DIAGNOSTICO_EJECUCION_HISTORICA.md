# Diagnóstico de ejecución histórica del Panel de Control Presupuestario

Fecha: 5 de noviembre de 2026. Revisión del backend: `c1684ed70ce5a88b175a84df1ed52438a35eeacf`.

**Alcance:** análisis estático del código, SQL versionado y pruebas existentes del checkout. No se modificaron código, migraciones, procedimientos ni pruebas; no se consultó una base de datos ni se ejecutaron pruebas. Los fragmentos y ejemplos siguientes documentan el diagnóstico, no constituyen una implementación. Las referencias son relativas a la raíz del backend y sus líneas corresponden a esta revisión.

## 1. Resumen ejecutivo

El backend contiene **dos modelos de lectura deliberadamente distintos**:

- **Por versión:** `ResumenAsync()` y `SaldoAsync()` leen `vw_PresupuestoResumen` y `vw_Saldo`. Los movimientos se agrupan por `IdPresupuestoDetalle`, que pertenece a una versión concreta. No existe arrastre entre versiones en estas consultas.
- **Consolidado vigente:** `VigenteAsync()` lee `vw_ControlPresupuestarioVigente`, que toma únicamente el presupuesto del snapshot `APROBADO` y acumula compromisos y ejecución netos de las versiones `APROBADO` e `HISTORICO`, por presupuesto y partida. `EjecucionDiariaAsync()` aplica ese alcance histórico a la serie temporal.

`GET /consultas/resumen` y `GET /consultas/saldo`, cuando no reciben versión ni estado, añaden el filtro `EstadoPresupuesto = APROBADO`. Por ello muestran **movimientos de la versión aprobada**, y no todos los movimientos del presupuesto. En cambio, `GET /consultas/arbol` y **también `GET /consultas/dashboard`** ya utilizan la lectura consolidada. Las cuatro rutas llevan el prefijo `/api/control-presupuestario`.

**Causa confirmada en el código:** seleccionar las filas aprobadas de una consulta por versión no equivale a consolidar el presupuesto. Esto explica la discrepancia entre `resumen`/`saldo` y `arbol` cuando existen movimientos históricos netos distintos de cero, dentro del mismo presupuesto y las mismas partidas.

**Límite de la conclusión:** no se confirma que `/consultas/dashboard` descarte movimientos históricos. Su implementación actual los incorpora tanto en rubros como en la curva. Si un widget servido exclusivamente por ese endpoint muestra solo ejecución de la versión 12, será necesario comparar solicitud/respuesta, datos y objetos SQL desplegados. Tampoco se verificó qué endpoint utiliza cada componente del frontend ni el estado real de MC2.

No se encontró evidencia de una acumulación indebida del presupuesto histórico en las consultas generales predeterminadas: el filtro aprobado evita sumar esos snapshots, y la vista consolidada nunca suma su importe presupuestado. No se recomienda modificar las vistas históricas ni reemplazar globalmente `SaldoAsync()`.

La solución propuesta es reutilizar `VigenteAsync()` para los indicadores generales, mediante una opción explícita de consolidación en `resumen` y `saldo`, conservando el comportamiento actual y las lecturas por versión. El dashboard mantiene su lógica de exclusión de partidas exclusivamente sobre `rubrosFiltrados`.

## 2. Mapa de dependencias y recorrido hasta HTTP

### 2.1. Consultas financieras

Prefijo de todas las filas: `/api/control-presupuestario/consultas`. Todas pasan por `ConsultaPresupuestariaController` y requieren `control_presupuestario.reporte.ver`.

| GET | Handler | Método de `IConsultaPresupuestariaRepository` | Procedimiento financiero | Vista y dependencia SQL | Contrato de salida |
| --- | --- | --- | --- | --- | --- |
| `/resumen` | `ResumenHandler` | `ResumenAsync(filtro)` | Ninguno; SELECT directo | `vw_PresupuestoResumen` → detalles, versiones, ledger y catálogos | `ResumenResponse`: `totales`, `porCentroCosto`, `partidasExcedidas` |
| `/saldo` | `SaldoHandler` | `SaldoAsync(filtro)` | Ninguno | `vw_Saldo` → `vw_PresupuestoResumen` | Arreglo de `SaldoPartidaResponse` |
| `/vigente` | `VigenteHandler` | `VigenteAsync(filtro)` | Ninguno | `vw_ControlPresupuestarioVigente` → `vw_PresupuestoResumen` + detalles aprobados | Arreglo de `SaldoPartidaResponse`, con movimientos consolidados |
| `/arbol` | `ObtenerArbolVigenteHandler` | `VigenteAsync(filtro)` | Ninguno para importes | `vw_ControlPresupuestarioVigente` | `ArbolPresupuestarioResult`: `encabezado`, `totales`, `nodos` |
| `/dashboard` | `ObtenerDashboardHandler` | `VigenteAsync(filtro)` + `EjecucionDiariaAsync(ids)` | Ninguno para importes | `vw_ControlPresupuestarioVigente` + `vw_EjecucionDiariaPorPartida` | `Tablero`: `encabezado`, `resumen`, `rubros`, `rubrosFiltrados`, `semanas`, `alCorte`, `desviacionPorRubro` |
| `/presupuesto-vs-comprometido` | `PresupuestoVsComprometidoHandler` | `PresupuestoVsComprometidoAsync` | Ninguno | `vw_PresupuestoVsComprometido` → `vw_PresupuestoResumen` | Comparación por versión |
| `/presupuesto-vs-ejecutado` | `PresupuestoVsEjecutadoHandler` | `PresupuestoVsEjecutadoAsync` | Ninguno | `vw_PresupuestoVsEjecutado` → `vw_PresupuestoResumen` | Comparación por versión |
| `/gastos-por-partida` | `GastosPorPartidaHandler` | `GastosPorPartidaAsync` | Ninguno | `vw_GastosPorPartida` → `vw_PresupuestoResumen` | Agrupación que conserva versión |
| `/gastos-por-centro-costo` | `GastosPorCentroCostoHandler` | `GastosPorCentroCostoAsync` | Ninguno | `vw_GastosPorCentroCosto` → `vw_PresupuestoResumen` | Agrupación que conserva presupuesto y versión |

Evidencia: [controller, líneas 18–74](Cobranzas_Vittoria/ControlPresupuestario/Presentation/Controller/ConsultaPresupuestariaController.cs#L18), [repositorio, líneas 22–78](Cobranzas_Vittoria/ControlPresupuestario/Infrastructure/Persistence/Repository/ConsultaPresupuestariaRepository.cs#L22), [vistas](Cobranzas_Vittoria/Migrations/Repeatable/1_Views/R__ControlPresupuestario_Views.sql#L12).

**No hay SP intermedio en los cuatro métodos solicitados.** El repositorio usa `Dapper.QueryAsync<T>` sobre SELECT parametrizados. El antiguo `ControlPresupuestario.usp_Dashboard_Datos` se elimina mediante [V2.6.2, líneas 7–16](Cobranzas_Vittoria/Migrations/Versioned/V2_6_2__ControlPresupuestario_Retira_Dashboard_SP.sql#L7); modificar o restaurar ese SP no corregiría la implementación actual.

### 2.2. Lecturas auxiliares de árbol y dashboard

| Función | Repositorio | SP | Fuente y efecto |
| --- | --- | --- | --- |
| Obtener centro | `ICentroCostoRepository.ObtenerAsync` | `ControlPresupuestario.usp_CentroCosto_Obtener` | `CentroCosto` y datos descriptivos; comprueba existencia |
| Seleccionar presupuestos | `IPresupuestoRepository.ListarAsync(true, idCentroCosto, null, null)` | `ControlPresupuestario.usp_Presupuesto_Listar` | Presupuestos activos y resumen de estados de sus versiones |
| Reconstruir jerarquía | `ICatalogoPartidaRepository.ListarAsync(new FiltroPartidas())` | `ControlPresupuestario.usp_CatalogoPartida_Listar` | Catálogo, padres y niveles; no calcula ejecución |

Los SP están en [lecturas SQL, líneas 6, 173 y 190](Cobranzas_Vittoria/Migrations/Repeatable/2_StoredProcedures/R__ControlPresupuestario_Lecturas_SPs.sql#L6). El listado devuelve `IdPresupuestoVersionAprobada` solo cuando existe exactamente una aprobada; `Presupuesto.TieneVersionAprobada` depende de ese identificador. Los handlers retienen esos presupuestos y filtran las filas consolidadas por sus IDs; si hay monedas distintas, devuelven `MONEDAS_MIXTAS`.

Evidencia: [PresupuestoRepository, líneas 19–26](Cobranzas_Vittoria/ControlPresupuestario/Infrastructure/Persistence/Repository/PresupuestoRepository.cs#L19), [modelo Presupuesto, línea 35](Cobranzas_Vittoria/ControlPresupuestario/Domain/Model/Presupuesto.cs#L35), [árbol, líneas 37–55](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Arbol/ObtenerArbolVigenteHandler.cs#L37), [dashboard, líneas 43–60](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Dashboard/ObtenerDashboardHandler.cs#L43).

### 2.3. Transformaciones y nombres HTTP

1. `ConsultaPresupuestariaRequest.AQuery()` transmite los filtros. `ConsultaPresupuestariaQuery.AFiltro()` aplica el estado por defecto y la exclusión de presupuestos inactivos; `Normalizado()` valida/normaliza el estado.
2. El repositorio materializa entidades Dapper y `ConsultaPresupuestariaMapper` copia los importes a los modelos de lectura. No elimina versiones ni añade arrastre durante el mapeo.
3. `SaldoHandler` y `VigenteHandler` delegan sin recalcular importes. `SaldoPartidaResponse.Desde()` renombra `MontoComprometido` a `comprometido`, `MontoEjecutado` a `ejecutado` y `EstadoPresupuesto` a `estado`, conservando `montoPresupuestado` y los identificadores de versión/detalle.
4. `ResumenHandler` agrupa por `(IdCentroCosto, IdPresupuesto, IdPresupuestoVersion)`, suma importes, recalcula porcentajes y marca excesos. Luego suma esos grupos para `totales`. No obtiene movimientos que la consulta SQL haya excluido.
5. El árbol pasa las filas a `ArbolPresupuestarioResult.ConstruirAsync()` y `ArbolPresupuestario.Construir()`: acumula hojas en sus categorías y calcula totales desde hojas, evitando sumar otra vez los subtotales de padres.
6. El dashboard agrupa filas consolidadas por rubro y ejecuciones diarias por fecha/rubro. `TableroPresupuestario` suma esos importes y construye distribución, curva y desviaciones. El controller devuelve `Tablero` directamente.

Referencias: [query, líneas 17–20](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/ConsultaPresupuestariaQuery.cs#L17), [mapper, líneas 9–31 y 84–135](Cobranzas_Vittoria/ControlPresupuestario/Infrastructure/Persistence/Mapper/ConsultaPresupuestariaMapper.cs#L9), [DTO HTTP, líneas 7–17 y 68–73](Cobranzas_Vittoria/ControlPresupuestario/Presentation/Dto/ConsultaPresupuestaria/ConsultaPresupuestariaResponses.cs#L7), [ResumenHandler, líneas 22–77](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Resumen/ResumenHandler.cs#L22), [árbol de dominio, líneas 35–98](Cobranzas_Vittoria/ControlPresupuestario/Domain/Service/ArbolPresupuestario.cs#L35).

## 3. Cálculos financieros y tratamiento de versiones

### 3.1. `vw_PresupuestoResumen` y `vw_Saldo`: movimientos del detalle de una versión

El CTE `Movimientos` agrupa por `mp.IdPresupuestoDetalle`. Cada detalle enlaza a `PresupuestoVersion`, y esta al presupuesto. No hay agrupación inicial por presupuesto/partida entre versiones ni un filtro intrínseco de estado en la vista base.

Definiendo `C` = COMPROMISO, `L` = LIBERACION, `E` = EJECUCION y `A` = AJUSTE, las sumas se calculan así:

```text
Presupuestado = PresupuestoDetalle.MontoPresupuestado
Comprometido  = SUM(C) - SUM(L)
                + SUM(A de COMPROMISO/INCREMENTO)
                - SUM(A de COMPROMISO/DECREMENTO)
Ejecutado     = SUM(E)
                + SUM(A de EJECUCION/INCREMENTO)
                - SUM(A de EJECUCION/DECREMENTO)
Saldo         = Presupuestado - Comprometido - Ejecutado
MontoExcedido = ABS(Saldo) si Saldo < 0; en otro caso 0
Excedido      = Saldo < 0
```

`vw_Saldo` proyecta la vista base y agrega las dos expresiones de exceso. No consolida ni elige la versión vigente. Las sumas SQL de movimientos se convierten a `DECIMAL(28,2)` y los detalles sin movimientos reciben ceros mediante `COALESCE`.

Fragmentos determinantes de [vistas, líneas 17–37, 71–94 y 185–220](Cobranzas_Vittoria/Migrations/Repeatable/1_Views/R__ControlPresupuestario_Views.sql#L17):

```sql
GROUP BY mp.IdPresupuestoDetalle
-- ...
LEFT JOIN Movimientos m
    ON m.IdPresupuestoDetalle = pd.IdPresupuestoDetalle
```

Una ejecución no se resta automáticamente del compromiso en esta fórmula. La liberación del compromiso y la ejecución son efectos separados del ledger; no corresponde sustituir la regla por `COMPROMISO - EJECUCION`. Tampoco corresponde sumar `TotalAjustes` como un importe adicional: los ajustes ya intervienen con su afectación y dirección.

Las vistas leen el ledger, no vuelven a evaluar el estado de cada compra o gasto de origen. La validez económica depende de los movimientos y sus compensaciones registrados por los flujos de escritura. El [SP de registro, líneas 425–449](Cobranzas_Vittoria/Migrations/Repeatable/2_StoredProcedures/R__ControlPresupuestario_SPs.sql#L425) exige montos positivos y coherencia entre tipo, afectación y dirección.

### 3.2. `vw_ControlPresupuestarioVigente`: presupuesto actual y arrastre neto

La vista usa tres pasos:

1. `VersionesVigentes`: versiones cuyo estado es `APROBADO`.
2. `Movimientos`: suma los compromisos y ejecuciones netos de `vw_PresupuestoResumen` con estado `APROBADO` o `HISTORICO`, agrupando por `(IdPresupuesto, IdCatalogoPartida)`.
3. `Partidas`: detalles reales de las versiones aprobadas. Se enlazan con el acumulado por presupuesto/partida, y el saldo se recalcula con el monto presupuestado de ese detalle aprobado.

Fragmento de [vistas, líneas 352–385 y 429–435](Cobranzas_Vittoria/Migrations/Repeatable/1_Views/R__ControlPresupuestario_Views.sql#L352):

```sql
WHERE ep.Codigo = 'APROBADO'
-- ... CTE Movimientos:
FROM ControlPresupuestario.vw_PresupuestoResumen r
WHERE r.EstadoPresupuesto IN ('APROBADO', 'HISTORICO')
GROUP BY r.IdPresupuesto, r.IdCatalogoPartida
-- ... saldo:
SELECT actual.MontoPresupuestado - COALESCE(m.MontoComprometido, 0)
    - COALESCE(m.MontoEjecutado, 0) AS SaldoDisponible
```

Por tanto:

- Conserva `IdPresupuestoVersion` e `IdPresupuestoDetalle` **del snapshot aprobado**, aunque los importes comprometido y ejecutado incluyan otros detalles históricos. Esos identificadores no identifican el conjunto completo de movimientos incluidos.
- No suma presupuestos históricos, no mueve asientos y no copia el ledger a la versión actual.
- Excluye `BORRADOR` y `ANULADO` del arrastre. Sin versión aprobada no hay filas, aunque existan versiones históricas.
- La consolidación requiere el mismo `IdPresupuesto` y el mismo `IdCatalogoPartida`; compartir código o nombre no basta. Tampoco consolida entre presupuestos distintos del centro de costo dentro de SQL: sus filas se suman después, cuando corresponde.
- La vista no filtra por sí misma `Presupuesto.Activo`, `CentroCosto.Activo` o `CatalogoPartida.Activo`. Los filtros adicionales dependen del consumidor.
- No reconstruye partidas ausentes del snapshot vigente. El [SP de aprobación, líneas 286–344](Cobranzas_Vittoria/Migrations/Repeatable/2_StoredProcedures/R__ControlPresupuestario_SPs.sql#L286) impide omitir partidas con compromiso o ejecución netos no cero, o gastos directos `REGISTRADO` pendientes. Una partida económicamente relevante debe conservar un detalle real, incluso con presupuesto cero. Esto protege las aprobaciones realizadas por ese flujo; no demuestra la integridad de snapshots cargados por otras vías.

El mismo SP convierte la aprobada anterior en `HISTORICO` y el borrador objetivo en `APROBADO`, dejando una sola línea base ([líneas 348–367](Cobranzas_Vittoria/Migrations/Repeatable/2_StoredProcedures/R__ControlPresupuestario_SPs.sql#L348)). Al cambiar de versión, una consulta por versión deja de mostrar los movimientos de la anterior; la consolidada sigue incluyéndolos.

### 3.3. `vw_EjecucionDiariaPorPartida`: serie histórica neta

Lee directamente el ledger, enlaza detalle → versión y filtra `APROBADO/HISTORICO`. Solo incluye `EJECUCION` y `AJUSTE` de afectación `EJECUCION`; aplica signo positivo a la ejecución y al incremento, y negativo al decremento. Agrupa por presupuesto, partida y `CONVERT(DATE, mp.Fecha)` ([vistas, líneas 450–472](Cobranzas_Vittoria/Migrations/Repeatable/1_Views/R__ControlPresupuestario_Views.sql#L450)).

`EjecucionDiariaAsync()` acepta solo una colección de IDs de presupuesto, añade `WHERE IdPresupuesto IN @Ids` y devuelve vacío para una colección vacía. No recibe versión, estado, moneda ni intervalo de fechas; la selección de presupuestos corresponde al handler.

En `TableroPresupuestario`, el ejecutado del resumen procede de los rubros consolidados; `alCorte.realAcumulado` y la curva proceden de las ejecuciones diarias. La fecha de corte es el máximo entre hoy y la fecha del último movimiento de ejecución, por lo que también incluye movimientos futuros registrados. El plan acumulado es una interpolación lineal entre fechas del presupuesto, no un cronograma financiero almacenado. La desviación global es `Ejecutado - Presupuestado`, **no** el saldo disponible; `TableroResumen` no expone saldo. Referencia: [servicio del tablero, líneas 67–143](Cobranzas_Vittoria/ControlPresupuestario/Domain/Service/TableroPresupuestario.cs#L67).

### 3.4. Filtros y ausencia de versión

Regla decisiva de [ConsultaPresupuestariaQuery, líneas 17–20](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/ConsultaPresupuestariaQuery.cs#L17):

```csharp
string.IsNullOrWhiteSpace(EstadoPresupuesto) && IdPresupuestoVersion is null
    ? EstadoPorDefecto : EstadoPresupuesto
// EstadoPorDefecto = "APROBADO"
```

| Solicitud o llamada | Resultado en `resumen`/`saldo` | Resultado en `vigente` |
| --- | --- | --- |
| HTTP sin versión ni estado | Solo filas aprobadas; movimientos de sus propios detalles | Filas aprobadas con movimientos históricos acumulados |
| Solo centro de costo | Igual que la fila anterior, limitado al centro | Consolidado de los presupuestos seleccionados de ese centro |
| Solo presupuesto | Igual, limitado al presupuesto; no arrastra históricos | Consolidado de ese presupuesto |
| Versión explícita, sin estado | Se elimina el estado predeterminado; lee esa versión en cualquier estado | Solo devuelve datos si esa versión es la aprobada; los movimientos siguen consolidados |
| Estado explícito sin versión | Lee las versiones de ese estado; `HISTORICO` puede devolver varias | Solo puede coincidir `APROBADO`; `HISTORICO`, `BORRADOR` y `ANULADO` no producen filas |
| Versión y estado explícitos | Ambos se combinan con AND; una contradicción devuelve vacío | Ambos filtran la identidad aprobada, sin recortar el ledger interno |
| Sin `incluirInactivos=true` | Excluye presupuestos inactivos | También los excluye mediante el filtro HTTP común |
| Llamada directa con `new FiltroConsultaPresupuestaria()` | Todas las versiones y presupuestos, incluidos inactivos; el repositorio no aplica los defaults HTTP | La propia vista limita a aprobadas; puede incluir presupuestos inactivos |

`ConstruirFiltro()` combina centro, presupuesto, versión, moneda, partida y estado mediante igualdades parametrizadas y AND; no infiere una versión a partir del centro ni del presupuesto ([repositorio, líneas 81–102](Cobranzas_Vittoria/ControlPresupuestario/Infrastructure/Persistence/Repository/ConsultaPresupuestariaRepository.cs#L81)). El filtro por partida se omite únicamente en `GastosPorCentroCostoAsync()`.

`arbol` y `dashboard` no reciben `idPresupuestoVersion`, `estadoPresupuesto` ni `incluirInactivos`. Usan filtros de dominio directos y restringen el resultado a los IDs activos con una única aprobada seleccionados previamente. No pasan por `ConsultaPresupuestariaQuery.AFiltro()`.

## 4. Causas identificadas y grado de certeza

### 4.1. Diferencia confirmada: lectura aprobada por versión frente a consolidada

La combinación de estos hechos determina la discrepancia:

1. `ResumenAsync()` y `SaldoAsync()` seleccionan vistas por detalle/versión.
2. `AFiltro()` selecciona `APROBADO` de forma predeterminada.
3. `ResumenHandler` suma esas filas sin consultar la cadena histórica; `SaldoHandler` las devuelve directamente.
4. Árbol y dashboard usan `VigenteAsync()`, cuyo acumulado sí atraviesa estados históricos.

**Es un desajuste confirmado respecto del uso de `resumen`/`saldo` como indicadores generales acumulados.** La consulta histórica aislada tiene un comportamiento coherente con su contrato actual. No sería correcto considerar defectuosa cualquier lectura que no arrastre históricos.

Ejemplo ilustrativo de una misma partida y presupuesto, sin representar los datos reales de MC2:

| Fuente | Presupuestado | Comprometido neto | Ejecutado neto |
| --- | ---: | ---: | ---: |
| Versión 11, ahora histórica | 1 000 | 100 | 300 |
| Versión 12, aprobada | 1 200 | 50 | 200 |

| Consulta | Presupuestado | Comprometido | Ejecutado | Saldo |
| --- | ---: | ---: | ---: | ---: |
| `resumen`/`saldo` predeterminados | 1 200 | 50 | 200 | 950 |
| `vigente`/árbol; importes de dashboard | 1 200 | 150 | 500 | 550 |

En el dashboard, el saldo de la última fila es una identidad financiera derivable, no un campo presente en su resumen. El ejemplo muestra omisión de movimientos históricos en la lectura por versión y sobreestimación del saldo si se interpreta como disponible global; no muestra suma indebida de presupuestos anteriores.

### 4.2. Hipótesis no confirmada: pérdida histórica dentro de `/dashboard`

El código actual contradice esa atribución general: [ObtenerDashboardHandler, líneas 53–55 y 91–105](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Dashboard/ObtenerDashboardHandler.cs#L53) usa la vista consolidada para rubros y la diaria histórica para la curva. No llama a `ResumenAsync()` ni `SaldoAsync()`.

Queda por comprobar si el componente visible llamado “dashboard” usa realmente `/dashboard`, si mezcla datos de `/resumen` o `/saldo`, y si la aplicación y vistas desplegadas coinciden con este checkout. El nombre de una pantalla no demuestra su origen HTTP.

### 4.3. Posibles inconsistencias adicionales; no causas acreditadas de MC2

- **Snapshot incompleto:** la vista vigente solo devuelve detalles aprobados existentes; la diaria puede contener movimientos de partidas históricas ausentes. Con datos fuera de las reglas de aprobación, resumen/rubros podrían diferir de curva/al corte. Se debe revisar cobertura antes de cambiar consultas. Una partida omitida con neto final cero también puede conservar actividad temporal legítima en la curva sin aportar al total final.
- **Identidad de partidas o presupuestos distinta:** el arrastre no sigue códigos o nombres coincidentes con IDs diferentes. Es una propiedad confirmada del join, pero su ocurrencia en MC2 no se verificó.
- **Varias versiones aprobadas:** la vista no selecciona el máximo `NumeroVersion`; expone las aprobadas existentes. El listado auxiliar identifica la inconsistencia y árbol/dashboard no seleccionan el presupuesto sin una única aprobada; los reportes directos no tienen esa misma selección. El flujo de aprobación normal previene esa situación, pero no se validaron los datos desplegados.
- **Monedas:** árbol/dashboard rechazan mezclas; `ResumenHandler` suma las filas recibidas sin separar los totales por moneda ni rechazarlas. Es un riesgo independiente de las versiones, no evidencia de la discrepancia reportada. El consumidor debe delimitar la moneda y cualquier modo consolidado nuevo debe evitar sumar monedas distintas.
- **Contadores:** `ResumenTotales.CentrosCosto` usa la cantidad de grupos centro/presupuesto/versión, no un DISTINCT de centros. Es una diferencia semántica observable, ajena al cálculo histórico y que requiere validación funcional separada.

El resumen sin filtros HTTP no suma snapshots históricos. Una llamada directa al repositorio sin estado, o una solicitud explícita de varios históricos, sí puede producir filas con presupuestos de varias versiones; sumarlas representa ese conjunto histórico, no el presupuesto vigente. No se identificó ese escenario como origen del problema descrito.

## 5. Componentes afectados, consumidores y riesgos de cambiar SQL

| Componente/funcionalidad | Dependencia observada | Riesgo de reemplazar la lectura existente |
| --- | --- | --- |
| Reportes de resumen, saldo y comparativos | Vistas históricas y filtros comunes | Cambiaría la consulta explícita de versiones, borradores e históricos y sus porcentajes |
| Árbol vigente y dashboard | `VigenteAsync()`; dashboard también `EjecucionDiariaAsync()` | Cambiar la base común puede alterar totales, rubros, excesos, curva y desviaciones que ya consolidan |
| Árbol de una versión | `ObtenerArbolVersionHandler` → `SaldoAsync(IdPresupuestoVersion)` | Una sustitución global por `VigenteAsync()` dejaría vacíos borradores/históricos, o mostraría ejecución acumulada donde se espera la de esa versión |
| Ajustes manuales | `RegistrarAjusteHandler` → `SaldoDetalleAsync(idDetalle)` → `vw_Saldo` | El saldo participa en la validación de incrementos; cambiar la vista altera una escritura de negocio |
| Selección de partidas para gastos directos | `contable.usp_GastoDirecto_PartidasDisponibles` → `vw_ControlPresupuestarioVigente` | Alteraría las opciones y saldos publicados para registrar gastos |
| Importación de gastos directos | Handler de importación y `GastoDirectoRepository.CrearLoteAsync()` consumen las partidas disponibles | Cambiaría validaciones previas y dentro de la transacción del lote |

Rutas adicionales confirmadas:

- `GET /api/control-presupuestario/presupuestos/{id}/versiones/{versionId}/arbol` → [ObtenerArbolVersionHandler, líneas 31–42](Cobranzas_Vittoria/ControlPresupuestario/Application/PresupuestoVersion/Arbol/ObtenerArbolVersionHandler.cs#L31).
- `POST /api/control-presupuestario/presupuestos/{id}/versiones/{versionId}/partidas/{detalleId}/ajustes` → [RegistrarAjusteHandler, líneas 35–46](Cobranzas_Vittoria/ControlPresupuestario/Application/PresupuestoDetalle/RegistrarAjuste/RegistrarAjusteHandler.cs#L35). La lectura por detalle es independiente de `SaldoAsync()`; modificar solo ese método no cambia `SaldoDetalleAsync()`, pero modificar `vw_Saldo` sí.
- `GET /api/contable/gastos-directos/partidas-disponibles?idCentroCosto=...` → [SP, líneas 246–265](Cobranzas_Vittoria/Migrations/Repeatable/2_StoredProcedures/R__Contable_GastoDirecto_SPs.sql#L246). Aunque el comentario menciona partidas “con saldo”, el SELECT no exige `SaldoDisponible > 0`; no se debe introducir ese filtro incidentalmente.
- `POST /api/contable/gastos-directos/importar` → [ImportarGastosDirectosHandler](Cobranzas_Vittoria/Contable/GastosDirectos/Application/Importar/ImportarGastosDirectosHandler.cs#L92) y [CrearLoteAsync, líneas 115–118](Cobranzas_Vittoria/Contable/GastosDirectos/Infrastructure/Persistence/Repository/GastoDirectoRepository.cs#L115).

**Dependencias SQL transitivas:** las cinco vistas derivadas de resumen (`vw_Saldo`, ambos comparativos y ambos gastos agrupados) y la consolidada dependen de `vw_PresupuestoResumen`. Convertir esta base en consolidada alteraría toda la cadena y podría volver a sumar el arrastre desde la vista vigente. La diaria, en cambio, consulta el ledger directamente.

**Flujos relacionados que escriben los datos, no consumidores de estas vistas:** `compras.usp_IntegracionEconomica_RegistrarLote` acepta referencias `APROBADO/HISTORICO`, registra compromiso/liberación/ejecución y calcula disponibilidad con sus propias consultas al ledger ([integración económica, líneas 47, 103–126 y 179](Cobranzas_Vittoria/Migrations/Repeatable/2_StoredProcedures/R__Compras_Integracion_Economica.sql#L47)). No se debe afirmar que modificar estas vistas cambia automáticamente todas las validaciones de compras; sí debe verificarse que la lectura del panel conserve las mismas reglas económicas.

La validación de saldo de ajustes manuales por detalle frente a disponibilidad consolidada merece una revisión funcional separada; no se propone modificarla indirectamente como parte de la corrección de lectura. La búsqueda de consumidores cubre el backend de este checkout, no clientes externos, informes SQL particulares ni otros despliegues.

## 6. Propuesta de corrección, sin implementación

### 6.1. Mantener dos alcances explícitos y reutilizar la consolidación existente

1. Conservar `ResumenAsync()`, `SaldoAsync()`, `SaldoDetalleAsync()` y las vistas históricas con su significado actual. Conservar también las consultas explícitas por estado/versión y el árbol de versión.
2. Ofrecer un modo opcional `consolidado=true` **solo en `/consultas/resumen` y `/consultas/saldo`**. Sin esa opción, mantener respuestas y semántica actuales. En modo consolidado, ambos leen `VigenteAsync()` con los filtros de centro, presupuesto, partida, moneda y presupuestos activos/inactivos aplicables.
3. Para resumen consolidado, reutilizar la agregación de totales, grupos y partidas excedidas sobre las filas de saldo vigente. Extraer únicamente la composición compartida necesaria, o adaptar las filas a una entrada común de lectura. No duplicar la fórmula de consolidación ni calcularla sumando presupuestos históricos en C#.
4. Rechazar `consolidado=true` combinado con `idPresupuestoVersion` o un `estadoPresupuesto` no vacío con un error 400 documentado: una consulta por versión/estado debe seguir su alcance histórico explícito. Validar la presencia de esos parámetros **antes** de aplicar `APROBADO` por defecto, para no confundir un valor implícito con uno solicitado.
5. Mantener los DTO de respuesta: los importes cambian de alcance solo cuando se solicita. En consolidado, documentar que los IDs de versión/detalle corresponden a la línea base aprobada. Mantener `incluirInactivos` como opción HTTP; para conciliar con árbol/dashboard, comparar solicitudes con presupuestos activos y el mismo conjunto de IDs. Evitar totales consolidados entre monedas distintas; si aparecen, exigir `idMoneda` o devolver un error de monedas mixtas en el modo nuevo.
6. Cambiar los consumidores del Panel que necesiten indicadores generales para solicitar el modo consolidado. Primero confirmar sus solicitudes reales. Los componentes que ya usan `/dashboard` o `/arbol` conservan sus fuentes.

Ejemplo de contrato propuesto, aún no disponible:

```http
GET /api/control-presupuestario/consultas/resumen?idCentroCosto=10&consolidado=true
GET /api/control-presupuestario/consultas/saldo?idPresupuesto=20&consolidado=true

# Continúa disponible la lectura individual:
GET /api/control-presupuestario/consultas/saldo?idPresupuestoVersion=120
```

Como alternativa de menor cambio para una tabla de saldo, el frontend puede consumir directamente `/consultas/vigente`, que ya devuelve `SaldoPartidaResponse` con acumulados. Esto evita ampliar `/saldo`; aun así haría falta resolver la fuente de los totales de `/resumen`. La opción aditiva propuesta permite conservar los endpoints usados por el Panel y separar el alcance sin cambiar sus estructuras JSON.

No se recomienda cambiar silenciosamente el comportamiento predeterminado de todos los reportes, ampliar el filtro a “todos los estados”, sumar snapshots ni reubicar movimientos históricos. Cambiar solamente el estado por defecto tampoco resolvería la combinación “presupuesto vigente + ejecución histórica”.

### 6.2. Archivos previstos para una implementación posterior

| Archivo/ruta | Cambio previsto |
| --- | --- |
| `Cobranzas_Vittoria/ControlPresupuestario/Presentation/Controller/ConsultaPresupuestariaController.cs` | Recibir el modo optativo en las dos acciones y transmitirlo; preservar las otras rutas |
| `Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Resumen/ResumenHandler.cs` | Añadir selección explícita de fuente y composición compartida de `ResumenResult` |
| `Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Saldo/SaldoHandler.cs` | Elegir `SaldoAsync()` o `VigenteAsync()` para este caso de uso |
| Requests/queries específicos de resumen y saldo, existentes o nuevos en sus carpetas | Representar y validar el modo, sin extender involuntariamente todas las consultas comunes |
| `Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/PresupuestoInactivoYReportesApiTests.cs` | Asegurar compatibilidad por defecto, filtros y presupuestos inactivos |
| `Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/ControlPresupuestarioContratoApiTests.cs` | Comparación entre múltiples versiones y contratos del modo nuevo |
| Pruebas unitarias del resumen/saldo en `Cobranzas_Vittoria.Tests/Unit/ControlPresupuestario/` | Selección de fuente, agregación y conflictos de filtros |
| Documentación del contrato y consumidores frontend del Panel, a localizar | Activar explícitamente los indicadores acumulados y documentar su semántica |

No se prevén cambios en `IConsultaPresupuestariaRepository`, su implementación, las vistas o los SP: `VigenteAsync()` ya expone los datos requeridos. Tampoco es necesario cambiar `ResumenResponse` o `SaldoPartidaResponse`. Las rutas frontend concretas quedan pendientes porque este diagnóstico se limita al backend.

### 6.3. Preservación del filtro de exclusión del dashboard

La exclusión actual resuelve padres y descendientes mediante `ExclusionPartidas.Resolver()`, filtra las partidas originales **antes** de agrupar y modifica únicamente `rubrosFiltrados`. Mantiene `encabezado`, `resumen`, `rubros`, `semanas`, `alCorte` y `desviacionPorRubro` para los mismos filtros previos. `idPartidaPadre`, a diferencia de las exclusiones, sí delimita toda la rama del dashboard.

La corrección propuesta no modifica `ObtenerDashboardHandler`, `ObtenerDashboardQuery`, `ExclusionPartidas` ni `TableroPresupuestario`. No debe propagarse `partidasExcluidas` al WHERE de las vistas, ni recortar el ledger/curva/totales para hacerlo coincidir con el donut. Referencias: [handler, líneas 60 y 107–124](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Dashboard/ObtenerDashboardHandler.cs#L107), [documentación existente, líneas 7–11](IMPLEMENTACION_FILTRO_PARTIDAS.md#L7).

## 7. Compatibilidad, riesgos y regresión recomendada

### 7.1. Riesgos y controles

- **Compatibilidad semántica:** conservar las solicitudes actuales. El modo explícito permite revisar cada consumidor antes de activar el acumulado y evita alterar reportes históricos.
- **Integridad del snapshot:** comprobar que las partidas con efecto económico histórico tengan detalle real en la aprobada. No fabricar detalles ni reasignar asientos para ocultar inconsistencias.
- **Escrituras financieras:** no cambiar las vistas compartidas con ajustes o gastos directos para resolver una diferencia del panel. Verificar sus flujos como regresión.
- **Moneda y alcance:** conciliar sobre el mismo centro, presupuesto(s), moneda y rama. No comparar un donut con exclusiones contra totales que deliberadamente permanecen completos.
- **Concurrencia:** las lecturas de presupuestos, vigente y diaria se realizan en consultas separadas; una aprobación o un movimiento concurrente puede producir diferencias transitorias. Si hay evidencia de ese escenario, evaluar una lectura consistente para el dashboard como tarea específica, sin atribuirlo de antemano al filtro de versión.
- **Despliegue:** comprobar objetos efectivos de SQL Server antes de modificar migraciones. La existencia de scripts correctos en Git no demuestra que se hayan aplicado al entorno afectado.
- **Rendimiento:** el acumulado recorre varias versiones. Medir con volumen real y revisar planes si es necesario; no hay medición en este análisis ni evidencia para exigir nuevos índices.

### 7.2. Casos de regresión propuestos

| Caso | Comprobación esperada |
| --- | --- |
| Una única aprobada | Consultas históricas y consolidadas coinciden |
| Varias históricas y una aprobada | Presupuestado solo de la aprobada; compromiso/ejecución netos de toda la cadena en modo consolidado |
| Lectura explícita de versión histórica/aprobada | Conserva exclusivamente los movimientos de sus detalles; no arrastra |
| Borrador y anulado explícitos | Consultables por versión/estado en modo histórico; no contribuyen al consolidado |
| Modo consolidado con versión o estado explícitos | 400 según el contrato propuesto; no ignorar filtros silenciosamente |
| Compromiso histórico liberado y ejecución posterior | El neto se conserva al cruzar versiones; no restar ejecución dos veces |
| Ajustes de ambas afectaciones y direcciones | Incrementos suman, decrementos restan; se conservan centavos y netos negativos |
| Detalle vigente con presupuesto cero e históricos no cero | Mantiene ejecución, compromiso, saldo negativo y exceso correctos |
| Presupuesto inactivo | Reportes predeterminados lo excluyen; lectura histórica con `incluirInactivos=true` lo conserva |
| Centro con varios presupuestos o monedas | Mismos IDs y moneda al conciliar; no sumar monedas diferentes en modo consolidado |
| Conciliación del dashboard | `resumen.ejecutado = SUM(rubros.ejecutado) = alCorte.realAcumulado` con datos válidos y alcance equivalente; coincide con total de ejecución del árbol sin filtro de rama |
| Fechas anteriores/futuras y compensaciones tardías | Curva basada en fecha real del asiento, fecha de corte conforme a regla existente y total final neto correcto |
| Exclusión de hoja/padre, duplicados y todas las partidas | Solo cambia `rubrosFiltrados`; excluir antes de agrupar; porcentajes calculados sobre lo incluido |
| Rama y nivel junto con exclusiones | Rama limita todo el tablero; exclusiones afectan solo distribución filtrada |
| Árbol por versión y ajustes manuales | Conservan sus fuentes y validaciones actuales |
| Partidas disponibles e importación de gastos | No cambia el saldo publicado ni la selección/validación del lote |

Pruebas existentes útiles, **leídas pero no ejecutadas en este trabajo**:

- [PresupuestoInactivoYReportesApiTests, líneas 51–96](Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/PresupuestoInactivoYReportesApiTests.cs#L51): defaults de estado, borrador explícito e inactivos. No prueba acumulación entre varias versiones.
- [ControlPresupuestarioContratoApiTests, líneas 258–319](Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/ControlPresupuestarioContratoApiTests.cs#L258): saldo, resumen y dashboard en una versión; ese caso no detecta la divergencia histórica.
- [ControlPresupuestarioSpsTests, línea 249](Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/ControlPresupuestarioSpsTests.cs#L249): arrastre con detalle real de monto cero y movimientos posteriores en histórico; documenta la intención de la vista vigente.
- [ObtenerDashboardHandlerTests, líneas 26–173](Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/ControlPresupuestario/ObtenerDashboardHandlerTests.cs#L26): exclusiones, agrupación, signos y conservación del resto del tablero. Sus repositorios en memoria no verifican SQL.
- [ArbolYEstructuraApiTests, líneas 205, 234 y 280](Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/ArbolYEstructuraApiTests.cs#L205): árbol, niveles y contrato HTTP de exclusiones.
- [GastosDirectosVersionesTests](Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/Contable/GastosDirectosVersionesTests.cs): efectos de confirmar/anular gastos entre revisiones y disponibilidad vigente.

## 8. Información adicional necesaria y conciliación en el entorno afectado

Para cerrar el diagnóstico operativo de MC2 se necesita:

1. **Solicitudes y respuestas reales** de los componentes discrepantes: URL completa, filtros y campos mostrados, junto con la respuesta de árbol/dashboard usada como comparación. No hace falta compartir credenciales ni tokens.
2. **Identidad y estado de MC2:** `IdCentroCosto`, `IdPresupuesto`, moneda, versiones y estados, confirmación de cuál es la aprobada y existencia de presupuestos inactivos o con códigos similares. “Versión 12” no identifica por sí sola un `IdPresupuestoVersion` global.
3. **Definiciones desplegadas** de las cuatro vistas analizadas, versión de aplicación y registro de migraciones aplicadas; comprobar en particular el reemplazo del antiguo SP de dashboard y la vista de arrastre.
4. **Desglose del ledger** por presupuesto, versión, partida, tipo, afectación y dirección para una partida concreta con diferencia. Confirmar que la cadena mantenga los mismos IDs de presupuesto/partida y que las anulaciones estén compensadas.
5. **Cobertura del snapshot aprobado:** detectar partidas con compromiso o ejecución histórica netos no cero sin detalle en la aprobada; verificar una sola aprobada y ausencia de cargas fuera del flujo normal.
6. **Decisión funcional del Panel:** cuáles tarjetas/tablas representan situación general y cuáles una versión individual. También confirmar que exclusiones solo deben afectar al donut y que la fecha de corte actual debe seguir incluyendo movimientos futuros.

Conciliación sugerida, de solo lectura y para una misma partida:

- Comparar `vw_PresupuestoResumen` filtrada por la versión aprobada con sus filas `APROBADO/HISTORICO` desglosadas por versión.
- Comprobar que `vw_ControlPresupuestarioVigente.MontoEjecutado` coincide con la suma neta de esas versiones y que su presupuestado coincide únicamente con el detalle aprobado.
- Verificar compromiso acumulado y recalcular `Saldo = presupuesto aprobado - compromiso acumulado - ejecución acumulada`.
- Comparar con la suma completa de `vw_EjecucionDiariaPorPartida` para ese presupuesto/partida, sin recortar fechas, y después con las respuestas HTTP bajo el mismo alcance.
- Si las vistas concilian y HTTP no, revisar filtros y versión del backend; si HTTP concilia y la pantalla no, revisar selección de endpoint, campo y transformación en el cliente.

Hasta contar con esa evidencia, queda fundamentada la diferencia de alcance entre las consultas del backend y la propuesta de corrección; no queda acreditado el importe real faltante de MC2 ni un defecto de consolidación en el dashboard desplegado.
