**Control Presupuestario: decisiones clave de integración**  
   
 **Estado:** propuesta para aprobación funcional y técnica  
   
    
   
  **Alcance:** define la semántica para la futura implementación de procedimientos almacenados, aplicación y API. No modifica el modelo ni implementa movimientos.  
   
 **Principios que no cambian**  
- PresupuestoVersion es un snapshot oficial; solo BORRADOR puede editarse.  
- Un requerimiento y las operaciones que se originen de él conservan el IdPresupuestoDetalle de la versión bajo la que nacieron.  
- MovimientoPresupuestal es un ledger inmutable: no se corrige mediante UPDATE o DELETE.  
- Monto es positivo y el tipo de movimiento expresa su significado económico.  
- AJUSTE no altera el saldo hasta que exista una semántica aprobada para él.  
 ![](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAnEAAAACCAYAAAA3pIp+AAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAANElEQVR4nO3OQQmAABRAsSeYxKQ/jMEMIR7FCt5E2BJsmZmt2gMA4C+Otbqr8+sJAACvXQ85TAYU+XLD1wAAAABJRU5ErkJggg==)  
 **1. Arrastre de movimientos entre versiones**  
 **Decisión propuesta**  
   
 El arrastre será **lógico, solo para reporting de control vigente**. No se copiarán, migrarán ni reasignarán movimientos ni operaciones históricas al aprobar una nueva versión.  
   
 Un movimiento seguirá relacionado con el PresupuestoDetalle de la versión donde nació su operación. Para medir la disponibilidad actual de la versión aprobada, el reporting deberá considerar los movimientos de los detalles homólogos de versiones anteriores, identificados por:  
   
 IdPresupuesto + IdCatalogoPartida  
   
    
   
 IdPresupuestoDetalle no es estable entre snapshots y, por tanto, no sirve para el arrastre entre versiones.  
 **Dos lecturas que deben coexistir**  
   
 | | | | |  
   
 |-|-|-|-|  
   
 | **Lectura** |  **Finalidad** |  **Presupuesto mostrado** |  **Movimientos incluidos** |  
   
 | Histórica por versión | Auditoría y trazabilidad | El monto de esa versión | Solo los movimientos de sus propios detalles |  
   
 | Control vigente | Decidir disponibilidad actual | El monto de la versión APROBADO | Movimientos de la misma partida y presupuesto, aunque procedan de detalles de versiones anteriores |  
   
    
   
 Ejemplo: V1 aprobada contiene una OC pendiente contra Concreto. Se aprueba V2 y el requerimiento histórico sigue en V1. Si una compra se acepta después, su ejecución permanece trazable a V1, pero debe descontar la disponibilidad de Concreto en V2 dentro de la vista de control vigente.  
 **Reglas de aprobación necesarias**  
   
 Al aprobar V2, el sistema deberá validar que toda partida de versiones previas con impacto aún vigente pueda representarse en V2. Como mínimo:  
- Si una partida tiene compromiso pendiente o puede recibir una ejecución futura de una operación histórica, V2 debe conservar la misma IdCatalogoPartida.  
- No se debe aprobar una versión que elimine una partida con obligaciones abiertas, porque la vista vigente no tendría dónde mostrar su consumo.  
- Una partida puede mantenerse con monto cero si se desea evidenciar que el consumo posterior generará una desviación; eliminarla por completo no es equivalente.  
 **Consecuencia para las vistas**  
   
 La vista actual basada únicamente en MovimientoPresupuestal.IdPresupuestoDetalle es válida como vista histórica. No debe asumirse que representa por sí sola el saldo vigente cuando existen varias versiones aprobadas históricas.  
 ![](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAnEAAAACCAYAAAA3pIp+AAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAALUlEQVR4nO3OQQ0AIAwEsAMnOJ0TtOFkGngRklZBR1WtJDsAAPzizNcDAADuNcK0AyWbyd+DAAAAAElFTkSuQmCC)  
 **2. Liberación explícita al ejecutar compromisos**  
 **Decisión propuesta**  
   
 Una EJECUCION **no libera implícitamente** un COMPROMISO. Cuando una compra consume una OC comprometida, se registran dos movimientos positivos y distintos:  
   
 LIBERACION  = importe de la OC que deja de estar pendiente  
   
  EJECUCION   = importe efectivamente consumido  
   
    
   
 Por tanto, las fórmulas de control propuestas son:  
   
 MontoComprometido = MAX(TotalCompromisos - TotalLiberaciones, 0)  
   
  SaldoDisponible   = MontoPresupuestado - MontoComprometido - MontoEjecutado  
   
    
   
 AJUSTE continúa expuesto por separado y no interviene en estas fórmulas.  
 **Motivo**  
   
 El módulo admite ejecuciones directas de GastoProyecto y GastoAdministrativo, que no tienen una OC previa. Si toda ejecución redujera automáticamente el compromiso agregado, un gasto directo reduciría por error el compromiso pendiente de una OC distinta que use la misma partida.  
   
 Ejemplo:  
   
 Presupuesto de partida: 200  
   
  OC comprometida:        100  
   
  Gasto directo:           30  
   
    
   
 Con la fórmula antigua (Compromiso - Liberación - Ejecución), el compromiso pendiente sería 70 y el saldo 100. El resultado correcto es compromiso pendiente 100, ejecución 30 y saldo 70.  
 **Matriz de movimientos**  
   
 | | |  
   
 |-|-|  
   
 | **Evento operativo** |  **Movimiento presupuestal** |  
   
 | OC aprobada por 100 | COMPROMISO 100 |  
   
 | Compra aceptada por 30, originada en esa OC | LIBERACION 30 + EJECUCION 30 |  
   
 | OC reducida en 20 antes de compra | LIBERACION 20 |  
   
 | OC anulada | LIBERACION por el compromiso pendiente |  
   
 | Gasto directo activo por 30 | EJECUCION 30 |  
   
    
   
 Esta decisión conserva visibles tanto la obligación pendiente como el gasto efectivamente ejecutado, sin doble consumo ni liberaciones implícitas incorrectas.  
 **Reversos de ejecución: bloqueo que requiere decisión posterior**  
   
 El catálogo actual tiene COMPROMISO, LIBERACION, EJECUCION y AJUSTE. LIBERACION debe conservar el significado de liberar un compromiso, por lo que no debe utilizarse para anular una ejecución directa. A su vez, AJUSTE todavía no tiene dirección económica definida.  
   
 Por eso, antes de habilitar la edición, anulación o desactivación de gastos integrados presupuestariamente, debe aprobarse una semántica explícita de reverso de ejecución. La alternativa más clara es incorporar un tipo positivo REVERSO_EJECUCION y descontarlo de MontoEjecutado; otra alternativa requiere definir formalmente dirección y reglas de AJUSTE.  
   
 Hasta esa decisión, no es seguro automatizar movimientos para gastos que luego pueden editarse, desactivarse o anularse.  
 ![](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAnEAAAACCAYAAAA3pIp+AAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAANUlEQVR4nO3OMQ2AABAAsSNhQgNSEPcTKpnRgQU2QtIq6DIze3UGAMBf3Gu1VcfXEwAAXrseaI0EMPwDEBYAAAAASUVORK5CYII=)  
 **3. Moneda base y tipo de cambio**  
 **Decisión propuesta**  
   
 La moneda base del módulo será **PEN (soles)**. Todos los importes de:  
- PresupuestoDetalle.MontoPresupuestado;  
- MovimientoPresupuestal.Monto;  
- vistas de presupuesto, comprometido, ejecutado y saldo;  
   
 se interpretan y comparan en PEN.  
   
 No se mezclarán importes nominales en USD y PEN dentro del ledger.  
 **Regla de conversión**  
   
 Cuando una operación de origen esté expresada en otra moneda, su movimiento se registra por su equivalente en PEN a la fecha económica del evento. El cálculo se redondea a dos decimales antes de insertarse en el ledger.  
   
 | | |  
   
 |-|-|  
   
 | **Origen** |  **Importe presupuestal en PEN** |  
   
 | Compra | El total de compra, que hoy el flujo trata como PEN |  
   
 | GastoProyecto | MontoSoles, ya persistido por el módulo contable |  
   
 | GastoAdministrativo en PEN | Monto |  
   
 | GastoAdministrativo en USD u otra moneda | Monto × tipo de cambio aplicado al evento |  
   
    
 **Trazabilidad del tipo de cambio**  
   
 El importe convertido no debe recalcularse al consultar reportes: el ledger debe conservar el valor PEN que se decidió al ocurrir el evento.  
   
 Para un gasto administrativo no PEN, el sistema también debe poder auditar de dónde salió ese valor. Hoy GastoAdministrativo persiste Moneda y Monto, pero no una fecha ni tasa de conversión; por ello no permite reproducir de manera confiable el importe PEN histórico.  
   
 La integración de gastos administrativos en moneda distinta de PEN debe quedar deshabilitada hasta aprobar una forma estructurada de persistir, como mínimo, la moneda origen, importe origen, tipo de cambio aplicado y fecha de tipo de cambio. No se debe usar el tipo fijo actualmente presente en el controlador presupuestario heredado como regla del nuevo ledger.  
 ![](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAnEAAAACCAYAAAA3pIp+AAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAANElEQVR4nO3OQQmAUBBAwSfIj+HZmJvAlAaxgjcRZhLMNjNHdQUAwF/ce7Wq8+sJAACvrQctfwNKYUQ0YAAAAABJRU5ErkJggg==)  
 **4. Momento exacto del impacto presupuestario**  
 **Decisión propuesta**  
   
 El impacto se produce únicamente cuando la operación se vuelve económicamente efectiva según su flujo. Crear una entidad, adjuntar documentos o modificar datos de borrador no debe generar por sí mismo un movimiento.  
   
 | | | | |  
   
 |-|-|-|-|  
   
 | **Operación** |  **Momento de impacto** |  **Movimiento** |  **Condición** |  
   
 | Requerimiento | No tiene impacto monetario | Ninguno | Solo selecciona la partida presupuestaria |  
   
 | Creación de OC | No tiene impacto | Ninguno | Aún puede modificarse |  
   
 | Aprobación de OC | Al entrar en el estado aprobado definido para el flujo | COMPROMISO por el total aprobado | Partida válida de una versión aprobada vigente |  
   
 | Reducción de OC aprobada | Al confirmar la reducción | LIBERACION por la reducción | No puede liberar más que el compromiso pendiente |  
   
 | Anulación de OC | Al confirmar la anulación | LIBERACION por el pendiente | No revierte ejecuciones ya realizadas |  
   
 | Registro de compra | No tiene impacto todavía | Ninguno | La compra queda pendiente de aceptación |  
   
 | Aceptación de compra | Dentro de la misma transacción que marca la compra aceptada | LIBERACION + EJECUCION por el importe aceptado | Debe provenir de una OC con presupuesto |  
   
 | Alta de gasto directo activo | Al persistirse como gasto activo integrado | EJECUCION | Importe PEN y detalle presupuestal válidos |  
   
 | Edición/anulación de gasto o compra aceptada | Pendiente de semántica de reverso aprobada | No automatizar aún | Requiere movimiento compensatorio explícito |  
   
    
 **Relación con el flujo actual**  
   
 Actualmente la creación de una compra la deja con Aceptada = 0, y la aceptación posterior es la operación que además registra el ingreso a Kardex. Por consistencia operativa y para evitar ejecutar compras no validadas, la aceptación es el punto recomendado para registrar la ejecución presupuestaria.  
   
 El estado que representa una OC aprobada debe normalizarse antes de la integración. El constraint histórico y el procedimiento de actualización de estado manejan conjuntos de estados distintos; no se debe asumir que cualquier estado denominado "Aceptada" o "Aprobada" es ya el punto financiero definitivo.  
 **Atomicidad obligatoria**  
   
 El cambio de la entidad operativa y su movimiento presupuestal deben completarse en la misma transacción de SQL Server. No es aceptable crear o aceptar una compra y, en una segunda llamada independiente, intentar registrar el movimiento: un error entre ambas llamadas dejaría el ERP y el ledger en estados distintos.  
 ![](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAnEAAAACCAYAAAA3pIp+AAAABmJLR0QA/wD/AP+gvaeTAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAANUlEQVR4nO3OQQmAABRAsSd40BA2MOQvYEx7WMGbCFuCLTNzVFcAAPzFvVZbdX49AQDgtf0BSrYDUhfMN7UAAAAASUVORK5CYII=)  
   
 **5. Idempotencia: por qué **Origen + IdOrigen + Tipo ** no basta**  
 **Qué identifica cada dato**  
   
 Origen  + IdOrigen  = entidad de negocio que produjo movimientos  
   
  Tipo                 = naturaleza económica de uno de esos movimientos  
   
    
   
 Esa combinación identifica una entidad y una clase de efecto, pero **no identifica un evento irrepetible**. Una misma entidad puede producir varios eventos legítimos del mismo tipo durante su vida.  
 **Casos legítimos que colisionarían**  
   
 | | |  
   
 |-|-|  
   
 | **Caso** |  **Movimientos válidos con la misma combinación** |  
   
 | OC #125 reducida dos veces | ORDEN_COMPRA / 125 / LIBERACION por 20 y luego por 10 |  
   
 | OC #125 con ejecuciones parciales | Varias liberaciones asociadas a compras aceptadas contra la misma OC |  
   
 | Gasto directo editable | La alta y un incremento posterior pueden requerir dos eventos de ejecución o un reverso y una nueva ejecución |  
   
 | OC anulada tras una reducción | Ya existe una liberación por reducción y corresponde otra por el saldo pendiente |  
   
    
   
 Declarar UNIQUE (Origen, IdOrigen, IdTipoMovimientoPresupuestal) impediría esos eventos válidos. Eliminar toda unicidad, por otro lado, permitiría que un reintento de red o de aplicación duplique un movimiento económico.  
 **Decisión propuesta**  
   
 La idempotencia debe basarse en una **clave de evento**, no en la identidad de la entidad origen. Cada movimiento necesita una clave estable, generada para el evento exacto que lo produce.  
   
 Ejemplos conceptuales:  
   
 OC:125:APROBACION:historial-901  
   
  OC:125:REDUCCION:historial-914:LIBERACION  
   
  COMPRA:90:ACEPTACION:LIBERACION  
   
  COMPRA:90:ACEPTACION:EJECUCION  
   
  GASTO_PROYECTO:42:ALTA:1:EJECUCION  
   
    
   
 Si un mismo evento genera dos movimientos, como la aceptación de una compra, cada fila necesita una secuencia o sufijo distinto. La garantía futura debe ser única por esa clave de movimiento, no por la entidad de origen.  
   
 El comportamiento esperado del registro de movimientos será:  
- Misma clave y mismo contenido: devolver el resultado existente; es un reintento seguro.  
- Misma clave con monto, detalle o tipo distintos: rechazar como conflicto.  
- Nueva clave: registrar el nuevo movimiento.  
   
 Origen + IdOrigen debe conservarse para trazabilidad e índices de consulta; no debe utilizarse como la garantía de idempotencia.  
 **Criterio para continuar**  
   
 Los procedimientos de versiones pueden diseñarse una vez estabilizadas las migraciones. La implementación de sp_RegistrarMovimiento y la integración con operaciones debe esperar la aprobación de estas decisiones, especialmente la semántica de reverso de ejecución y la persistencia de tipo de cambio para gastos no PEN.  
