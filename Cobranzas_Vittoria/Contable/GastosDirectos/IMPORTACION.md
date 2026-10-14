# Importación masiva de gastos directos

`POST /api/contable/gastos-directos/importar` recibe `multipart/form-data` con un campo
`archivo` de tipo XLSX. Exige `gasto_directo.operar`, igual que el alta manual.
`GET /api/contable/gastos-directos/plantilla` devuelve la plantilla original adjunta,
incluida como recurso del assembly para que también esté disponible al publicar la API.
Ambas operaciones están documentadas en Swagger.

## Contrato y flujo

Se procesa solamente `Carga masiva`, con los 16 encabezados exactos en fila 5 y
registros desde fila 6. Se aceptan de 1 a 200 registros no vacíos y archivos de hasta
10 MB. Las columnas auxiliares no se importan, no validan datos y no generan registros.
Las otras hojas, incluidos sus ejemplos e IDs informativos, no se procesan.

1. `FileValidator` y `FileParserResolver` verifican archivo, extensión, MIME y firma.
2. La nueva lectura contractual de `ExcelFileParser` conserva las filas físicas,
   recorta textos y lee resultados almacenados de fórmulas, sin ejecutarlas.
   La lectura usada por las otras importaciones mantiene su comportamiento.
3. El caso de uso comprueba obligatoriedad, fechas, importes, longitudes y el bloque USD.
   Recupera catálogos una vez y partidas disponibles una vez por centro de costo.
4. Categorías se resuelven exclusivamente por los siete códigos permitidos y su
   catálogo activo. Centros admiten código o nombre; partidas admiten código o nombre
   dentro del centro; proveedores admiten RUC o razón social. Se rechaza la ambigüedad.
5. La moneda contable procede de la partida disponible del presupuesto aprobado.
   El bloque de referencia requiere los cuatro campos, USD activo y diferente de la
   moneda presupuestaria. El monto contable no se convierte ni se recalcula.
6. `GastoDirectoRepository.CrearLoteAsync` abre una transacción `SERIALIZABLE` y
   revalida todas las relaciones usando los mismos procedimientos de consulta del
   formulario. Después crea gastos con `contable.usp_GastoDirecto_Crear` y referencias
   con `contable.usp_GastoDirectoDocumento_Registrar`, en esa conexión y transacción.
   Cualquier rechazo o cancelación revierte gastos y documentos del lote completo.

Fechas nativas de Excel se convierten a su fecha calendario; fechas de texto requieren
`dd/MM/yyyy`. Los importes se procesan como `decimal`. Valores numéricos OOXML se
normalizan a los 15 dígitos significativos de Excel con aritmética decimal, evitando
que una representación como `3.7599999999999998` rechace el valor `3.76`. Textos
numéricos se conservan exactamente, usan punto decimal sin separador de miles y deben
respetar `decimal(18,2)` para montos y `decimal(18,6)` para tipos de cambio.

## Facturas y auditoría

La aclaración del usuario establece que `Factura(s)` contiene números opcionales,
sin PDF. Varias referencias se separan con `;`, por ejemplo `F001-245; INV-10025`.
La plantilla y las importaciones existentes no definen un separador para múltiples
números de factura; esta convención usa el separador de sus plantillas CSV y queda
documentada en el endpoint. Se rechazan referencias repetidas en la misma fila.

Se reutiliza `contable.GastoDirectoDocumento`: `TipoDocumento=Factura`, número como
etiqueta `NombreArchivo`, `RutaArchivo=NULL` y `Extension=NULL`. Las consultas
existentes devuelven además `numeroFactura` y `tieneArchivo`. Una referencia no tiene
PDF descargable; su descarga devuelve 404 con un mensaje específico. Los PDF existentes
conservan su ruta y pueden descargarse mediante el flujo habitual.

La migración `V2_7_3` permite esas referencias en la tabla existente, conservando la
restricción de ruta para documentos físicos, y agrega `UsuarioCreacion` al gasto.
Esto es necesario porque el esquema anterior exigía un archivo físico y carecía de
usuario de auditoría. No se crean tablas ni relaciones alternativas. La identidad
procede de `IUsuarioActualService`; un campo `usuario` enviado por formulario se ignora.
El estado y las fechas los establece el SP manual: `REGISTRADO`, creación UTC y
actualización inicial nula. Registrar no confirma el gasto ni genera ejecución
presupuestaria; la confirmación posterior conserva las reglas económicas del ERP.

Los IDs de categorías se obtienen por código, según la decisión del usuario. Esto
admite tanto la instalación limpia con IDs 1–7 como el catálogo con IDs 26–32, sin
modificar ni renumerar registros. Los IDs de moneda tampoco se fijan en código.

## Respuestas

Éxito HTTP 200: `modulo`, `formato`, `filasInsertadas`, `totalFilas`, `filasValidas`,
`filasInvalidas`, `ok`, `resultado=IMPORTADO` y `errores=[]`.

Rechazo de datos HTTP 422: conserva `ok=false`, `error=DATOS_INVALIDOS`, `message`
y `errores` del middleware compartido, y añade el resumen y `resultado=RECHAZADO`.
Cada error tiene `fila` física del Excel, `campo`, `codigoError` y `mensaje`.
`filasInsertadas` es cero incluso si falló una asociación después de insertar otras
filas dentro de la transacción. Las importaciones anteriores mantienen su contrato.
Errores de estructura/archivo conservan HTTP 400 y exceso de tamaño HTTP 413.

## Archivos

Nuevos:

- `Application/Importar/PlantillaGastosDirectos.cs`
- `Application/Importar/ImportarGastosDirectosHandler.cs`
- `Application/Importar/ResultadoImportacionGastosDirectos.cs`
- `Application/Importar/ObtenerPlantillaGastosDirectosHandler.cs`
- `Domain/Model/GastoDirectoImportado.cs`
- `Infrastructure/Plantillas/Plantilla_Carga_Masiva_Gastos_Directos.xlsx`
- `../../Migrations/Versioned/V2_7_3__Contable_GastoDirecto_Importacion.sql`
- `../../Cobranzas_Vittoria.Tests/Integration/Contable/GastosDirectosImportacionTests.cs`
- `../../Cobranzas_Vittoria.Tests/Unit/Importacion/GastosDirectosExcelTests.cs`
- Este documento.

Modificados dentro de `Contable/GastosDirectos`:

- `Presentation/Controller/GastoDirectoController.cs`
- `Domain/Persistence/IGastoDirectoRepository.cs`
- `Domain/Model/GastoDirecto.cs`
- `Infrastructure/Persistence/Repository/GastoDirectoRepository.cs`
- `Infrastructure/Persistence/Entity/GastoDirectoEntities.cs`
- `Infrastructure/Persistence/Mapper/GastoDirectoMapper.cs`
- `Application/Documentos/Descargar/DescargarDocumentoHandler.cs`

Modificados en el proyecto:

- `Application/Importacion/Parsers/ExcelFileParser.cs`
- `Application/Importacion/Excepciones/DatosInvalidosException.cs`
- `Middleware/ApiExceptionMiddleware.cs`
- `Migrations/Repeatable/2_StoredProcedures/R__Contable_GastoDirecto_SPs.sql`
- `Swagger/SwaggerConfiguration.cs`
- `Swagger/ErrorResponsesOperationFilter.cs`
- `Cobranzas_Vittoria.csproj`
- `Cobranzas_Vittoria.Tests/Integration/Swagger/SwaggerJsonGenerationTests.cs`

## Verificación

Las pruebas usan NUnit, la plantilla real y el SQL Server aislado de Testcontainers.
Cubren los 21 escenarios mínimos del requerimiento, incluyendo siete categorías,
catálogos activos, límites de filas, fórmulas, culturas, fechas, USD, facturas, permisos,
auditoría y rollback al fallar la asociación de la segunda factura. También prueban
Swagger y la desactivación de una relación entre validación y persistencia.

Comandos desde la raíz del repositorio:

```bash
dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj --no-restore --filter 'FullyQualifiedName~GastosDirectosImportacionTests|FullyQualifiedName~GastosDirectosExcelTests|FullyQualifiedName~Swagger'
dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj --no-restore --filter 'FullyQualifiedName~Importacion|FullyQualifiedName~GastosDirectosControllerTests|FullyQualifiedName~ControlPresupuestario|FullyQualifiedName~Swagger|FullyQualifiedName~ApiExceptionMiddlewareTests|FullyQualifiedName~MigracionGastosHistoricosTests'
```

La ejecución de regresión pasó 440 pruebas, sin fallos ni omitidas. Incluye las
importaciones anteriores y los flujos de presupuesto, alta manual, documentos y
migraciones históricas. La compilación inicial del backend terminó sin errores;
el proyecto conserva advertencias preexistentes.

La verificación final focalizada pasó 81 pruebas, sin fallos ni omitidas, sobre
importación de gastos directos, lectura de la plantilla y Swagger. Ambos conjuntos
se solapan; las cifras no representan una suma de pruebas distintas. Los resultados
TRX están en `Cobranzas_Vittoria.Tests/TestResults/gastos-directos.trx` y
`Cobranzas_Vittoria.Tests/TestResults/importaciones-regresion.trx`.
