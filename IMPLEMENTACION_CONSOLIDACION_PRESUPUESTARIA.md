# Implementación de consolidación presupuestaria

Fecha: 5 de noviembre de 2026. Alcance: backend .NET 8; consultas del Panel de Control y trazabilidad de movimientos. Implementación en el árbol de trabajo, sin commit ni despliegue.

## 1. Resultado y diseño

Los seis reportes aceptan `consolidado=true`. En ese modo leen `VigenteAsync()` y, por tanto, `ControlPresupuestario.vw_ControlPresupuestarioVigente`: presupuesto exclusivamente de la versión aprobada y compromiso/ejecución netos de las versiones APROBADO/HISTORICO del mismo presupuesto y partida. Las respuestas conservan sus DTO actuales.

La aplicación comparte la lectura, validación de moneda y proyecciones en `ConsultaConsolidada`. Resumen y gastos por centro reutilizan la misma agrupación; cada comparativo mantiene su fórmula específica. Gastos por partida proyecta una fila por partida de la base aprobada: la restricción única `(IdPresupuestoVersion, IdCatalogoPartida)` garantiza esa cardinalidad. No se acumulan nuevamente los movimientos en los handlers.

La nueva consulta `/consultas/movimientos` recupera los asientos originales de varias versiones, con su origen y dos efectos firmados para conciliación. Usa SELECT parametrizado en `MovimientoPresupuestalRepository`, como los reportes existentes. No necesita nuevas tablas, vistas, índices ni migraciones. Los handlers se registran mediante el escaneo existente del módulo.

Los diagnósticos se usaron como referencias. La especificación implementada amplía su propuesta mínima: también consolida los cuatro reportes adicionales y agrega el historial.

## 2. Archivos

### Creados

- [Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/ConsolidacionPresupuestariaApiTests.cs](Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/ConsolidacionPresupuestariaApiTests.cs)
- [Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/ControlPresupuestario/ConsultaConsolidadaTests.cs](Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/ControlPresupuestario/ConsultaConsolidadaTests.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/ConsultaConsolidada.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/ConsultaConsolidada.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/MovimientoPresupuestal/ListarConsolidados/ListarMovimientosConsolidadosHandler.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/MovimientoPresupuestal/ListarConsolidados/ListarMovimientosConsolidadosHandler.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/MovimientoPresupuestal/ListarConsolidados/MovimientoConsolidadoResult.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/MovimientoPresupuestal/ListarConsolidados/MovimientoConsolidadoResult.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Presentation/Dto/ConsultaPresupuestaria/MovimientosConsolidadosRequest.cs](Cobranzas_Vittoria/ControlPresupuestario/Presentation/Dto/ConsultaPresupuestaria/MovimientosConsolidadosRequest.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Presentation/Dto/MovimientoPresupuestal/MovimientoConsolidadoResponse.cs](Cobranzas_Vittoria/ControlPresupuestario/Presentation/Dto/MovimientoPresupuestal/MovimientoConsolidadoResponse.cs)
- [IMPLEMENTACION_CONSOLIDACION_PRESUPUESTARIA.md](IMPLEMENTACION_CONSOLIDACION_PRESUPUESTARIA.md) (este reporte).

### Modificados

- [Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/ConsultaPresupuestariaQuery.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/ConsultaPresupuestariaQuery.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/GastosPorCentroCosto/GastosPorCentroCostoHandler.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/GastosPorCentroCosto/GastosPorCentroCostoHandler.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/GastosPorPartida/GastosPorPartidaHandler.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/GastosPorPartida/GastosPorPartidaHandler.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/PresupuestoVsComprometido/PresupuestoVsComprometidoHandler.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/PresupuestoVsComprometido/PresupuestoVsComprometidoHandler.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/PresupuestoVsEjecutado/PresupuestoVsEjecutadoHandler.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/PresupuestoVsEjecutado/PresupuestoVsEjecutadoHandler.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Resumen/ResumenHandler.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Resumen/ResumenHandler.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Saldo/SaldoHandler.cs](Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Saldo/SaldoHandler.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Domain/Persistence/IMovimientoPresupuestalRepository.cs](Cobranzas_Vittoria/ControlPresupuestario/Domain/Persistence/IMovimientoPresupuestalRepository.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Infrastructure/Persistence/Repository/MovimientoPresupuestalRepository.cs](Cobranzas_Vittoria/ControlPresupuestario/Infrastructure/Persistence/Repository/MovimientoPresupuestalRepository.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Presentation/Controller/ConsultaPresupuestariaController.cs](Cobranzas_Vittoria/ControlPresupuestario/Presentation/Controller/ConsultaPresupuestariaController.cs)
- [Cobranzas_Vittoria/ControlPresupuestario/Presentation/Dto/ConsultaPresupuestaria/ConsultaPresupuestariaRequest.cs](Cobranzas_Vittoria/ControlPresupuestario/Presentation/Dto/ConsultaPresupuestaria/ConsultaPresupuestariaRequest.cs)

No se modifican SQL versionado, procesos de escritura, DTO de respuesta existentes, `ConsultaPresupuestariaRepository`, `SaldoDetalleAsync()`, árbol, dashboard ni código Angular.

## 3. Contrato común de los seis reportes

Prefijo: `/api/control-presupuestario/consultas`. Método: GET. Autenticación: `Authorization: Bearer <token>`. Permiso: `control_presupuestario.reporte.ver`. Respuestas exitosas: HTTP 200, `application/json`, sin envoltorio `data`.

| Ruta | Respuesta HTTP 200 | Granularidad |
| --- | --- | --- |
| `/resumen` | `ResumenResponse` | Totales, grupos y partidas excedidas |
| `/saldo` | `SaldoPartidaResponse[]` | Detalle de la base vigente |
| `/presupuesto-vs-comprometido` | `PresupuestoVsComprometidoResponse[]` | Detalle de la base vigente |
| `/presupuesto-vs-ejecutado` | `PresupuestoVsEjecutadoResponse[]` | Detalle de la base vigente |
| `/gastos-por-partida` | `GastoPorPartidaResponse[]` | Partida/presupuesto/base vigente |
| `/gastos-por-centro-costo` | `GastoPorCentroCostoResponse[]` | Centro/presupuesto/base vigente |

### Parámetros de query string

Todos son opcionales. Los filtros presentes se combinan mediante AND.

| Campo | Tipo | Comportamiento |
| --- | --- | --- |
| `consolidado` | boolean | Omitido o `false`: semántica individual previa. `true`: activa la lectura consolidada. |
| `idCentroCosto` | entero | Limita los presupuestos al centro. |
| `idPresupuesto` | entero | Limita a un presupuesto. No anula otros filtros. |
| `idCatalogoPartida` | entero | Igualdad por ID, sin descendientes. En el nuevo modo se aplica antes de agrupar, también en gastos por centro. |
| `idMoneda` | entero | Igualdad por moneda del presupuesto; no convierte importes. |
| `incluirInactivos` | boolean nullable | Omitido o `false`: solo presupuestos activos. `true`: incluye activos e inactivos. |
| `idPresupuestoVersion` | entero | Disponible en modo individual. Incompatible con `consolidado=true`. |
| `estadoPresupuesto` | string | BORRADOR/APROBADO/HISTORICO/ANULADO; normalizado en modo individual. Cualquier valor explícito no vacío es incompatible con `consolidado=true`, incluso APROBADO. |

La validación de incompatibilidad ocurre antes de aplicar APROBADO por defecto. Estado ausente, vacío o compuesto solo por espacios se considera no indicado. Sin versión ni estado y sin modo consolidado se mantiene APROBADO como estado predeterminado. Una versión explícita sin estado conserva la consulta de esa versión, en cualquier estado.

**Moneda:** todos los reportes consolidados, incluso los que devuelven arrays, rechazan con HTTP 400 `MONEDAS_MIXTAS` si las filas seleccionadas contienen más de un `idMoneda`. Esto aplica a consultas globales y a centros con varias monedas. El cliente debe seleccionar `idMoneda` o un presupuesto; no se elige una moneda arbitrariamente ni se convierte o suma entre monedas. El filtro de inactivos se aplica antes de esa comprobación. Las consultas individuales mantienen su comportamiento anterior.

**Identidad:** `idPresupuestoVersion`, `numeroVersion`, `estado` e `idPresupuestoDetalle` describen la base APROBADO vigente. Los importes consolidados incluyen otros detalles de versiones HISTORICO. Los campos de identidad siguen presentes solo en los DTO que ya los tenían: gastos por partida no incluye detalle y gastos por centro no incluye partida/detalle.

No se añaden filtros de nivel, rama ni exclusiones a estos reportes. `/vigente`, `/dashboard` y `/arbol` mantienen sus contratos anteriores; el nuevo parámetro está tipado solo para los seis reportes indicados.

### Campos completos de respuesta

Los ejemplos siguientes enumeran todas las propiedades de cada DTO. Los IDs y contadores son números enteros, los importes/porcentajes son números JSON basados en `decimal` y `excedido` es booleano. Los textos descriptivos admiten `null` conforme a los DTO existentes.

En todas las filas de reporte, la cabecera es:

```typescript
interface CabeceraPresupuestaria {
  idCentroCosto: number;
  codigoCentroCosto: string | null;
  centroCosto: string | null;
  idPresupuesto: number;
  codigoPresupuesto: string | null;
  presupuesto: string | null;
  idMoneda: number;
  moneda: string | null;
  simboloMoneda: string | null;
  idPresupuestoVersion: number;
  numeroVersion: number;
  estado: string | null;
}
```

Los ejemplos usan IDs ilustrativos y los netos de la prueba de integración: partida 25 con presupuesto 1200, compromiso 375 y ejecución 530; partida 26 con presupuesto cero y ejecución histórica 25. No representan datos de producción. Los porcentajes de filas pueden contener más decimales que los mostrados por Angular; el formato numérico no debe interpretarse como un contrato de cantidad fija de decimales.

## 4. Peticiones y respuestas completas

### 4.1. Resumen

```http
GET /api/control-presupuestario/consultas/resumen?idCentroCosto=1&idPresupuesto=10&consolidado=true
Authorization: Bearer <token>
```

HTTP 200:

```json
{
  "totales": {
    "presupuestado": 1200,
    "comprometido": 375,
    "ejecutado": 555,
    "saldoDisponible": 270,
    "porcentajeEjecutado": 46.25,
    "centrosCosto": 1,
    "partidas": 2,
    "partidasExcedidas": 1
  },
  "porCentroCosto": [
    {
      "idCentroCosto": 1,
      "codigoCentroCosto": "CC-CONS",
      "centroCosto": "Centro consolidado",
      "idPresupuesto": 10,
      "codigoPresupuesto": "P-CONS",
      "presupuesto": "Presupuesto",
      "idMoneda": 1,
      "moneda": "PEN",
      "simboloMoneda": "S/",
      "idPresupuestoVersion": 103,
      "numeroVersion": 3,
      "estado": "APROBADO",
      "montoPresupuestado": 1200,
      "comprometido": 375,
      "ejecutado": 555,
      "saldoDisponible": 270,
      "porcentajeEjecutado": 46.25,
      "excedido": false
    }
  ],
  "partidasExcedidas": [
    {
      "idCentroCosto": 1,
      "codigoCentroCosto": "CC-CONS",
      "centroCosto": "Centro consolidado",
      "idPresupuesto": 10,
      "codigoPresupuesto": "P-CONS",
      "presupuesto": "Presupuesto",
      "idMoneda": 1,
      "moneda": "PEN",
      "simboloMoneda": "S/",
      "idPresupuestoVersion": 103,
      "numeroVersion": 3,
      "estado": "APROBADO",
      "idPresupuestoDetalle": 1032,
      "idCatalogoPartida": 26,
      "codigoPartida": "2",
      "partida": "Partida 2",
      "nivel": 1,
      "montoPresupuestado": 0,
      "comprometido": 0,
      "ejecutado": 25,
      "saldoDisponible": -25,
      "montoExcedido": 25,
      "excedido": true
    }
  ]
}
```

`totales` contiene `presupuestado`, `comprometido`, `ejecutado`, `saldoDisponible`, `porcentajeEjecutado`, `centrosCosto`, `partidas`, `partidasExcedidas`. `porCentroCosto` usa el contrato de gastos por centro; `partidasExcedidas` usa el contrato de saldo.

`partidas` cuenta filas de detalle seleccionadas; `partidasExcedidas` cuenta saldos negativos. Por compatibilidad, `centrosCosto` conserva el contador previo de grupos centro/presupuesto/versión, que puede superar la cantidad distinta de centros. No se cambia esa semántica como parte de esta corrección.

### 4.2. Saldo

```http
GET /api/control-presupuestario/consultas/saldo?idPresupuesto=10&idCatalogoPartida=25&consolidado=true
```

HTTP 200:

```json
[
  {
    "idCentroCosto": 1,
    "codigoCentroCosto": "CC-CONS",
    "centroCosto": "Centro consolidado",
    "idPresupuesto": 10,
    "codigoPresupuesto": "P-CONS",
    "presupuesto": "Presupuesto",
    "idMoneda": 1,
    "moneda": "PEN",
    "simboloMoneda": "S/",
    "idPresupuestoVersion": 103,
    "numeroVersion": 3,
    "estado": "APROBADO",
    "idPresupuestoDetalle": 1031,
    "idCatalogoPartida": 25,
    "codigoPartida": "1",
    "partida": "Partida 1",
    "nivel": 1,
    "montoPresupuestado": 1200,
    "comprometido": 375,
    "ejecutado": 530,
    "saldoDisponible": 295,
    "montoExcedido": 0,
    "excedido": false
  }
]
```

### 4.3. Presupuesto frente a comprometido

```http
GET /api/control-presupuestario/consultas/presupuesto-vs-comprometido?idPresupuesto=10&idCatalogoPartida=25&consolidado=true
```

HTTP 200:

```json
[
  {
    "idCentroCosto": 1,
    "codigoCentroCosto": "CC-CONS",
    "centroCosto": "Centro consolidado",
    "idPresupuesto": 10,
    "codigoPresupuesto": "P-CONS",
    "presupuesto": "Presupuesto",
    "idMoneda": 1,
    "moneda": "PEN",
    "simboloMoneda": "S/",
    "idPresupuestoVersion": 103,
    "numeroVersion": 3,
    "estado": "APROBADO",
    "idPresupuestoDetalle": 1031,
    "idCatalogoPartida": 25,
    "codigoPartida": "1",
    "partida": "Partida 1",
    "nivel": 1,
    "montoPresupuestado": 1200,
    "comprometido": 375,
    "diferencia": 825,
    "porcentajeComprometido": 31.25
  }
]
```

`diferencia` es presupuesto menos compromiso; no resta la ejecución. `porcentajeComprometido` es compromiso neto dividido entre presupuesto vigente, por cien.

### 4.4. Presupuesto frente a ejecutado

```http
GET /api/control-presupuestario/consultas/presupuesto-vs-ejecutado?idPresupuesto=10&idCatalogoPartida=25&consolidado=true
```

HTTP 200:

```json
[
  {
    "idCentroCosto": 1,
    "codigoCentroCosto": "CC-CONS",
    "centroCosto": "Centro consolidado",
    "idPresupuesto": 10,
    "codigoPresupuesto": "P-CONS",
    "presupuesto": "Presupuesto",
    "idMoneda": 1,
    "moneda": "PEN",
    "simboloMoneda": "S/",
    "idPresupuestoVersion": 103,
    "numeroVersion": 3,
    "estado": "APROBADO",
    "idPresupuestoDetalle": 1031,
    "idCatalogoPartida": 25,
    "codigoPartida": "1",
    "partida": "Partida 1",
    "nivel": 1,
    "montoPresupuestado": 1200,
    "ejecutado": 530,
    "diferencia": 670,
    "porcentajeEjecutado": 44.166666666666666666666666667
  }
]
```

`diferencia` es presupuesto menos ejecución; no resta el compromiso.

### 4.5. Gastos por partida

```http
GET /api/control-presupuestario/consultas/gastos-por-partida?idPresupuesto=10&idCatalogoPartida=25&consolidado=true
```

HTTP 200:

```json
[
  {
    "idCentroCosto": 1,
    "codigoCentroCosto": "CC-CONS",
    "centroCosto": "Centro consolidado",
    "idPresupuesto": 10,
    "codigoPresupuesto": "P-CONS",
    "presupuesto": "Presupuesto",
    "idMoneda": 1,
    "moneda": "PEN",
    "simboloMoneda": "S/",
    "idPresupuestoVersion": 103,
    "numeroVersion": 3,
    "estado": "APROBADO",
    "idCatalogoPartida": 25,
    "codigoPartida": "1",
    "partida": "Partida 1",
    "nivel": 1,
    "montoPresupuestado": 1200,
    "ejecutado": 530,
    "diferencia": 670,
    "porcentajeEjecutado": 44.166666666666666666666666667
  }
]
```

La granularidad conserva presupuesto y versión base, además de partida. No se agrupan entre presupuestos diferentes por compartir una partida. El contrato sigue sin `idPresupuestoDetalle`; el nuevo historial usa presupuesto/partida y puede abrirse desde esta fila.

### 4.6. Gastos por centro de costo

```http
GET /api/control-presupuestario/consultas/gastos-por-centro-costo?idCentroCosto=1&idPresupuesto=10&consolidado=true
```

HTTP 200:

```json
[
  {
    "idCentroCosto": 1,
    "codigoCentroCosto": "CC-CONS",
    "centroCosto": "Centro consolidado",
    "idPresupuesto": 10,
    "codigoPresupuesto": "P-CONS",
    "presupuesto": "Presupuesto",
    "idMoneda": 1,
    "moneda": "PEN",
    "simboloMoneda": "S/",
    "idPresupuestoVersion": 103,
    "numeroVersion": 3,
    "estado": "APROBADO",
    "montoPresupuestado": 1200,
    "comprometido": 375,
    "ejecutado": 555,
    "saldoDisponible": 270,
    "porcentajeEjecutado": 46.25,
    "excedido": false
  }
]
```

La agrupación continúa siendo `(idCentroCosto, idPresupuesto, idPresupuestoVersion)`. No se fusionan varios presupuestos del centro en una única fila. Los importes se suman primero y luego se calcula el porcentaje. En modo consolidado `idCatalogoPartida` restringe las filas antes de agrupar; el modo individual conserva la consulta anterior, que omite ese filtro en este reporte.

## 5. Historial consolidado

```http
GET /api/control-presupuestario/consultas/movimientos?idPresupuesto=10&idCatalogoPartida=25
Authorization: Bearer <token>
```

Ambos parámetros son enteros positivos obligatorios, validados mediante DataAnnotations y también en el caso de uso. No requiere `consolidado=true`: esta ruta representa siempre el historial consolidado. No admite selección de versión o estado. No tiene paginación ni filtro de fechas; devuelve todos los movimientos participantes en orden ascendente `(fecha, idMovimientoPresupuestal)`.

**Permisos:** requiere conjuntamente `control_presupuestario.reporte.ver` (controller) y `control_presupuestario.movimiento.ver` (acción). No se crea ningún permiso nuevo. El cliente debe verificar ambos permisos para habilitar el botón.

Primero se comprueba que exista el presupuesto y que la partida tenga una fila real en su base aprobada. Si falta, devuelve 404. Esto también rechaza una partida ajena, una partida solo de borrador, una partida histórica omitida de la base o un presupuesto sin aprobada; no publica como conciliable una partida que la vista vigente no incluye. Una partida válida sin movimientos devuelve `[]`.

La consulta permite inspeccionar un presupuesto inactivo por ID; es trazabilidad explícita, coherente con la consulta de reportes con `incluirInactivos=true`. No convierte esto en inclusión automática de inactivos en los reportes.

HTTP 200 devuelve `MovimientoConsolidadoResponse[]`. Ejemplo completo de contrato con dos movimientos de versiones distintas; es un extracto ilustrativo de actividad, no el listado íntegro que explica los importes de los ejemplos anteriores:

```json
[
  {
    "idMovimientoPresupuestal": 501,
    "idPresupuestoDetalle": 1011,
    "tipo": "LIBERACION",
    "nombreTipo": "Liberación",
    "origen": "PRUEBA",
    "idOrigen": 1,
    "claveEvento": "evento-501",
    "fecha": "2026-01-01T00:00:00",
    "monto": 100,
    "afectacion": "COMPROMISO",
    "direccion": "DECREMENTO",
    "observacion": null,
    "idPresupuesto": 10,
    "idPresupuestoVersion": 101,
    "numeroVersion": 1,
    "estadoPresupuesto": "HISTORICO",
    "idCatalogoPartida": 25,
    "codigoPartida": "1",
    "partida": "Partida 1",
    "codigoPresupuesto": "P-CONS",
    "idMoneda": 1,
    "moneda": "PEN",
    "simboloMoneda": "S/",
    "esNuevo": false,
    "idTipoMovimientoPresupuestal": 2,
    "efectoComprometido": -100,
    "efectoEjecutado": 0
  },
  {
    "idMovimientoPresupuestal": 509,
    "idPresupuestoDetalle": 1031,
    "tipo": "EJECUCION",
    "nombreTipo": "Ejecución",
    "origen": "PRUEBA",
    "idOrigen": 1,
    "claveEvento": "evento-509",
    "fecha": "2026-01-01T00:00:00",
    "monto": 200,
    "afectacion": "EJECUCION",
    "direccion": "INCREMENTO",
    "observacion": null,
    "idPresupuesto": 10,
    "idPresupuestoVersion": 103,
    "numeroVersion": 3,
    "estadoPresupuesto": "APROBADO",
    "idCatalogoPartida": 25,
    "codigoPartida": "1",
    "partida": "Partida 1",
    "codigoPresupuesto": "P-CONS",
    "idMoneda": 1,
    "moneda": "PEN",
    "simboloMoneda": "S/",
    "esNuevo": false,
    "idTipoMovimientoPresupuestal": 3,
    "efectoComprometido": 0,
    "efectoEjecutado": 200
  }
]
```

### Campos y conciliación

| Campos | Tipo y significado |
| --- | --- |
| `idMovimientoPresupuestal` | long; identidad única del asiento, usar como clave de fila Angular. |
| `idPresupuesto`, `idPresupuestoVersion`, `numeroVersion`, `estadoPresupuesto` | Identidad del presupuesto y versión **de origen**, no necesariamente la aprobada actual. |
| `idPresupuestoDetalle`, `idCatalogoPartida` | Detalle original y partida común entre versiones. |
| `idTipoMovimientoPresupuestal`, `tipo`, `nombreTipo` | ID del catálogo, código no nulo y nombre nullable. |
| `origen`, `idOrigen`, `claveEvento` | Procedencia original; textos nullable, ID entero. |
| `fecha` | DateTime ISO 8601 original, sin conversión de zona horaria ni desplazamiento añadido. |
| `monto` | decimal original positivo; no se cambia el signo para representar una liberación/decremento. |
| `afectacion`, `direccion`, `observacion` | Datos originales nullable, sin reescritura. |
| `codigoPartida`, `partida`, `codigoPresupuesto` | Textos descriptivos nullable. |
| `idMoneda`, `moneda`, `simboloMoneda` | ID y código/símbolo de la moneda del presupuesto; textos nullable. |
| `esNuevo` | boolean, `false` en esta lectura; se conserva el dato del contrato original. |
| `efectoComprometido`, `efectoEjecutado` | decimal firmado por movimiento. Sumar cada columna reconstruye su magnitud consolidada. |

La ruta individual conserva su DTO anterior sin estos tres campos nuevos (`idTipoMovimientoPresupuestal`, `efectoComprometido`, `efectoEjecutado`). El modelo interno ya contenía el ID de tipo, pero la respuesta HTTP individual no lo exponía.

| Tipo | Afectación / dirección | Efecto comprometido | Efecto ejecutado |
| --- | --- | --- | --- |
| COMPROMISO | Datos originales | +monto | 0 |
| LIBERACION | Datos originales | -monto | 0 |
| EJECUCION | Datos originales | 0 | +monto |
| AJUSTE | COMPROMISO / INCREMENTO | +monto | 0 |
| AJUSTE | COMPROMISO / DECREMENTO | -monto | 0 |
| AJUSTE | EJECUCION / INCREMENTO | 0 | +monto |
| AJUSTE | EJECUCION / DECREMENTO | 0 | -monto |

Se incluyen únicamente estados APROBADO/HISTORICO y tipos con efecto económico reconocido por las vistas. Los ajustes sin afectación/dirección válidas no se presentan como aportes al neto. Los pares compensatorios permanecen como dos filas originales aunque su suma sea cero. No se consulta el estado actual de compras/gastos: las vistas consolidadas y el historial leen el ledger y sus compensaciones.

```text
comprometido acumulado = SUM(efectoComprometido)
ejecutado acumulado    = SUM(efectoEjecutado)
saldo disponible      = presupuesto base aprobada - comprometido acumulado - ejecutado acumulado
```

En el ejemplo basado en la prueba principal, la partida 25 tiene 11 movimientos únicos de v1/v2/v3, compromiso neto 375 y ejecución neta 530. La partida 26 agrega ejecución histórica 25 con presupuesto actual cero. Resumen, centro y árbol totalizan presupuesto 1200, compromiso 375, ejecución 555 y saldo 270.

## 6. Reglas financieras y compatibilidad

- El presupuesto histórico nunca se suma: únicamente se toma `MontoPresupuestado` de la base APROBADO.
- Compromiso neto = compromisos - liberaciones + ajustes de compromiso con signo. Ejecución neta = ejecuciones + ajustes de ejecución con signo. No se resta automáticamente la ejecución del compromiso.
- Saldo = presupuesto - compromiso neto - ejecución neta. Un saldo negativo marca `excedido=true`; `montoExcedido` es su valor absoluto, en caso contrario cero.
- Diferencia del comparativo de compromiso = presupuesto - compromiso. Diferencia del comparativo de ejecución y gastos por partida = presupuesto - ejecución.
- Los porcentajes usan la magnitud neta dividida entre presupuesto, por cien. Con presupuesto cero se conserva la regla existente: porcentaje cero, incluso con ejecución histórica y saldo negativo. No se limita el porcentaje financiero al rango 0–100.
- Resumen redondea el porcentaje global a dos decimales con `AwayFromZero`; los porcentajes de filas/grupos utilizan `decimal` sin redondeo adicional en la aplicación. Angular debe formatearlos al presentar.
- Existen dos lecturas explícitas: sin modo se mantienen las vistas por versión y los defaults anteriores. No cambian los consumidores de saldo de Requerimientos ni el saldo usado en validaciones de ajustes.

Ejemplos de consultas individuales conservadas:

```http
GET /api/control-presupuestario/consultas/saldo?idCentroCosto=1&estadoPresupuesto=APROBADO
GET /api/control-presupuestario/consultas/saldo?idPresupuestoVersion=101
GET /api/control-presupuestario/consultas/resumen?idPresupuestoVersion=101
GET /api/control-presupuestario/consultas/gastos-por-partida?estadoPresupuesto=HISTORICO
GET /api/control-presupuestario/presupuestos/10/versiones/101/arbol
GET /api/control-presupuestario/presupuestos/10/versiones/101/partidas/1011/movimientos
```

La nueva lectura no modifica `vw_PresupuestoResumen`, `vw_Saldo`, `SaldoDetalleAsync()`, árboles por versión, árbol vigente, dashboard, evolución diaria ni `rubrosFiltrados`. No reasigna movimientos a la versión actual ni modifica registro/aprobación/compromiso/liberación/ejecución/ajuste.

## 7. Errores HTTP

| Estado | Código / origen | Condición |
| --- | --- | --- |
| 400 | `FILTROS_INCOMPATIBLES` | Consolidado + versión o estado explícito no vacío. |
| 400 | `MONEDAS_MIXTAS` | Las filas consolidadas seleccionadas abarcan varias monedas. |
| 400 | Validación HTTP de ASP.NET Core | Parámetros obligatorios ausentes/no positivos del historial o tipos de query inválidos. Respuesta ValidationProblemDetails estándar. |
| 400 | `IDENTIFICADOR_INVALIDO` | Defensa del caso de uso del historial cuando se invoca directamente con IDs no positivos. |
| 400 | `ESTADO_INVALIDO` | Estado desconocido en una consulta individual, como antes. |
| 404 | `PRESUPUESTO_NO_ENCONTRADO` | Historial de un presupuesto inexistente. |
| 404 | `PARTIDA_CONSOLIDADA_NO_ENCONTRADA` | Partida sin fila en la base aprobada de ese presupuesto. |
| 401 | Autenticación JWT | Token ausente o inválido. |
| 403 | Autorización | Falta permiso de reporte; en historial también se exige permiso de movimiento. |

Ejemplo de error del middleware presupuestario:

```http
GET /api/control-presupuestario/consultas/saldo?consolidado=true&estadoPresupuesto=APROBADO
```

```json
{
  "ok": false,
  "error": "FILTROS_INCOMPATIBLES",
  "message": "Consolidado no admite idPresupuestoVersion ni estadoPresupuesto explícitos."
}
```

Una consulta financiera sin filas devuelve arrays vacíos y/o totales cero con HTTP 200; no se transforma en error de existencia. La comprobación 404 de pertenencia es específica del nuevo historial.

## 8. Pruebas y validaciones

La suite seleccionada de Control Presupuestario ejecutó **184 pruebas, 184 aprobadas, cero fallidas y cero omitidas**: 19 casos unitarios nuevos y 10 de integración nuevos, más 155 regresiones existentes. El entorno usa Testcontainers con SQL Server `mcr.microsoft.com/mssql/server:2025-latest`, migraciones completas y el servidor HTTP de pruebas. No se utiliza el connection string de la aplicación para acceder a una base local.

Casos nuevos y evidencia:

| Caso | Cobertura |
| --- | --- |
| Aprobada única | Equivalencia de los seis reportes entre ambos modos, con movimientos actuales y sin históricos; partida válida sin movimientos devuelve `[]`. |
| Dos históricas y una aprobada | Presupuesto 1200 sin sumar 1000/1100 históricos; netos entre tres versiones. |
| Liberaciones, ambos ajustes y compensaciones | Ledger y efectos individuales; suma de 11 movimientos y conservación de importes positivos originales. |
| Presupuesto vigente cero | Histórico 25, saldo -25, exceso 25, porcentaje cero. |
| Sobreejecución | Presupuesto reducido a 400; saldo -505 en partida, porcentaje 132.5%; resumen 138.75%. |
| Compatibilidad | Sin parámetro, `false`, versión explícita, estado APROBADO de Requerimientos, saldo histórico, otros cuatro reportes y resumen individual. |
| Validación de filtros | Rechazo en los seis endpoints antes de los defaults; pruebas unitarias incluyen todos los estados y versión. |
| Monedas/global/inactivos | Rechazo de mezcla global y por centro, filtro de moneda, inclusión/exclusión de presupuesto inactivo y trazabilidad del inactivo. |
| Filtros y aislamiento | Centro, presupuesto, partida antes de agrupar y sin cruce de movimientos de otro presupuesto para la misma partida. |
| Historial | IDs únicos, datos originales, origen en v1/v2/v3, orden por fecha y desempate por ID; excluye BORRADOR/ANULADO. |
| Pertenencia y permisos | Presupuesto inexistente, partida ajena, ausencia de aprobada, IDs inválidos, 401 y 403. |
| Conciliación y regresión | Totales iguales al árbol vigente; árbol histórico individual; dashboard completo estable y exclusiones solo sobre `rubrosFiltrados`. |

Comando ejecutado:

```bash
dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-restore \
  --filter 'FullyQualifiedName~Tests.Unit.ControlPresupuestario|FullyQualifiedName~Tests.Integration.ControlPresupuestario' \
  --logger 'trx;LogFileName=consolidacion.trx' \
  --results-directory /tmp/consolidacion-resultados
```

Evidencia de la sesión: `/tmp/consolidacion-resultados/consolidacion.trx` y `/tmp/consolidacion-tests.log` (archivos temporales, no incluidos en Git). Los IDs de los ejemplos son ilustrativos; las pruebas usan IDs generados por SQL Server.

Validaciones adicionales completadas:

- `dotnet build backend.sln --no-restore -v minimal`: compilación de solución exitosa, cero errores y 19 advertencias en código preexistente fuera del cambio (nulabilidad y documentación XML). Registro: `/tmp/consolidacion-build-final.log`.
- `dotnet format whitespace backend.sln --no-restore --include <archivos C# del cambio>` y verificación posterior con `--verify-no-changes`: formato de los archivos modificados/nuevos correcto.
- `git diff --check`: correcto. Se verificaron además los JSON de ejemplo y los enlaces locales de este reporte.

No se ejecuta la suite completa de otros módulos. No se valida un despliegue, una base destino, datos reales de MC2, navegador ni integración Angular. La comprobación funcional realizada es backend HTTP/SQL sobre el contenedor desechable.

## 9. Integración Angular

1. Ampliar el filtro de los seis métodos de reporte con `consolidado?: boolean`. Mantener el valor predeterminado omitido; no establecerlo globalmente en el servicio compartido.
2. En el Panel, enviar `consolidado: true` tanto para resumen como para saldo y los otros cuatro reportes. Conservar centro/presupuesto/moneda y no agregar versión ni estado. El reporte frontend anterior proponía limitarlo a dos rutas; esta implementación habilita expresamente las seis.
3. Las seis tarjetas y excesos se alimentan del nuevo `/resumen`; tablas/CSV de cada pestaña conservan sus contratos y reciben cifras consolidadas. No sumar versiones ni recalcular el arrastre en Angular.
4. Si la consulta global o el centro mezcla monedas, solicitar selección de moneda/presupuesto y reenviar el filtro. No mostrar cifras anteriores como éxito ni sumar importes con distinto símbolo. La API no selecciona una moneda por defecto.
5. Presentar la versión de la fila financiera como **base aprobada vigente**. En el historial usar `numeroVersion` y `estadoPresupuesto` para una columna de versión de origen.
6. En las filas con `idPresupuesto` e `idCatalogoPartida`, consultar `/consultas/movimientos` para explicar el acumulado. Esto habilita también gastos por partida aunque no tenga ID de detalle. Gastos por centro es una agregación sin partida: requiere seleccionar una partida antes de abrir esta consulta; no inventar un ID.
7. Usar `idMovimientoPresupuestal` como clave de fila; mantener `monto` original y mostrar o sumar `efectoComprometido`/`efectoEjecutado` para conciliación. No sumar todos los `monto` ni restar ejecución de compromiso automáticamente. El JSON de historial es plano, no contiene un objeto anidado `movimiento`.
8. Conservar el historial individual en la gestión explícita de versiones y el árbol de versión. Su ruta y DTO no cambian. El árbol vigente y dashboard no requieren cambios de cálculo ni de peticiones en este trabajo.
9. Conservar la petición de Requerimientos `saldo({ idCentroCosto, estadoPresupuesto: 'APROBADO' })` sin nuevo modo.
10. El DTO de saldo continúa sin `porcentajeEjecutado`. La barra de avance identificada en el diagnóstico frontend requiere una decisión separada; esta implementación no introduce esa propiedad.

## 10. Límites y decisiones técnicas

- Se reutiliza la cobertura de la vista vigente: una partida económicamente relevante debe estar en la base aprobada, incluso con importe cero. No se reconstruyen snapshots corruptos ni se repara información cargada por fuera de los flujos normales de aprobación.
- Se mantiene la integridad esperada de una única versión aprobada por presupuesto. Esta corrección no elige por número máximo ni repara duplicados de estado cargados fuera del flujo normal.
- La moneda se valida sobre las filas efectivamente seleccionadas; un presupuesto sin detalles no aporta importes. La conciliación con árbol/dashboard exige el mismo conjunto de presupuestos activos, moneda y partidas, sin exclusiones/ramas distintas.
- No se introduce una transacción entre las distintas peticiones HTTP. Una aprobación o un nuevo movimiento concurrente puede cambiar los importes entre la carga de reportes y la apertura del historial; volver a consultar con el mismo alcance para conciliar.
- El historial no tiene paginación para que la respuesta permita reconstruir la totalidad del neto. Para presupuestos de gran volumen, evaluar una ampliación del contrato con totales y paginación sin sumar solo la página visible.
- Las fórmulas de efecto del historial reproducen la semántica vigente de `vw_PresupuestoResumen` y están verificadas contra el consolidado SQL. Si se cambian esas reglas en el futuro, deben actualizarse conjuntamente.
- Los IDs `long` se conservan como números JSON, igual que en el contrato individual. No hay cambio de transporte a string.
- No hay migración nueva que aplicar: desplegar el backend actualizado sobre un esquema con las migraciones vigentes. Las pruebas del contenedor acreditan ese esquema versionado; no confirman las vistas instaladas en otro entorno.
