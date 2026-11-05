# Filtro de exclusión de partidas del Dashboard Presupuestario

Fecha: 5 de noviembre de 2026.

## 1. Resumen

El backend acepta `partidasExcluidas` en `GET /api/control-presupuestario/consultas/dashboard` y devuelve `rubrosFiltrados`, una distribución independiente con la misma estructura de `TableroRubro` que `rubros`.

Las exclusiones se resuelven mediante las relaciones del catálogo jerárquico y se aplican a las partidas originales **antes** de agruparlas por `nivel` o `idPartidaPadre`. Solo cambia la nueva distribución para el donut. Se conservan los resultados de `encabezado`, `resumen`, `rubros`, `semanas`, `alCorte` y `desviacionPorRubro` para los mismos filtros anteriores.

No se modificaron frontend, procedimientos almacenados, vistas, migraciones, repositorios ni reglas de cálculo financiero. Tampoco se modificó la ejecución histórica utilizada por los otros reportes.

## 2. Archivos creados o modificados

Rutas relativas a la raíz del backend:

| Archivo | Cambio |
| --- | --- |
| `Cobranzas_Vittoria/ControlPresupuestario/Presentation/Controller/ConsultaPresupuestariaController.cs` | Recibe el filtro de exclusiones y lo transmite al handler. |
| `Cobranzas_Vittoria/ControlPresupuestario/Presentation/Dto/ConsultaPresupuestaria/ExclusionPartidasRequest.cs` | Nuevo DTO con validación de la colección mediante DataAnnotations (`IValidatableObject`). |
| `Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Dashboard/ObtenerDashboardQuery.cs` | Agrega `IReadOnlyCollection<int>? PartidasExcluidas = null`. |
| `Cobranzas_Vittoria/ControlPresupuestario/Application/ConsultaPresupuestaria/Dashboard/ObtenerDashboardHandler.cs` | Resuelve exclusiones, reutiliza una sola función de agrupación y construye la distribución independiente. |
| `Cobranzas_Vittoria/ControlPresupuestario/Domain/Service/ExclusionPartidas.cs` | Nuevo servicio puro que valida IDs y obtiene el conjunto de partidas excluidas con sus descendientes. |
| `Cobranzas_Vittoria/ControlPresupuestario/Domain/Service/TableroPresupuestario.cs` | Agrega `Tablero.RubrosFiltrados` y extrae `ConstruirDistribucion`, compartido con la distribución original. |
| `Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/ControlPresupuestario/ObtenerDashboardHandlerTests.cs` | Nuevas pruebas del handler, montos, jerarquía, invariantes y validaciones. |
| `Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Integration/ControlPresupuestario/ArbolYEstructuraApiTests.cs` | Nuevas pruebas HTTP de colección, agrupaciones, rama, porcentajes y errores. |
| `IMPLEMENTACION_FILTRO_PARTIDAS.md` | Este contrato y reporte de implementación. |

## 3. Contrato HTTP

### Endpoint y parámetros

```http
GET /api/control-presupuestario/consultas/dashboard
Authorization: Bearer <token>
```

Conserva el permiso `control_presupuestario.reporte.ver`.

| Query parameter | Tipo | Regla |
| --- | --- | --- |
| `idCentroCosto` | `int` | Obligatorio; positivo. |
| `idPresupuesto` | `int?` | Opcional; positivo. Conserva la selección de presupuestos activos con versión aprobada y la validación de moneda única. |
| `nivel` | `int?` | Opcional; desde 1. Usa el nivel absoluto del catálogo. Un nivel mayor que la profundidad disponible conserva las hojas de cada rama. |
| `idPartidaPadre` | `int?` | Opcional; positivo y existente. Limita **todo** el dashboard a esa rama. Sin `nivel`, agrupa por el nivel siguiente al de la partida seleccionada. |
| `partidasExcluidas` | colección de `int` | Nuevo y opcional. IDs positivos y existentes de `CatalogoPartida`; puede incluir hojas y padres. Afecta únicamente a `rubrosFiltrados`. |

La colección se envía repitiendo la clave. Los valores son `idCatalogoPartida`, **no** `idPresupuestoDetalle`, códigos de partida ni índices del gráfico.

```http
# Sin exclusiones: rubrosFiltrados equivale a rubros
GET /api/control-presupuestario/consultas/dashboard?idCentroCosto=1

# Excluir dos partidas, agrupando por raíces
GET /api/control-presupuestario/consultas/dashboard?idCentroCosto=1&nivel=1&partidasExcluidas=3&partidasExcluidas=7

# Seleccionar presupuesto y navegar dentro de una rama
GET /api/control-presupuestario/consultas/dashboard?idCentroCosto=1&idPresupuesto=10&idPartidaPadre=2&partidasExcluidas=3

# Se admiten redundancias entre padres e hijos y valores repetidos
GET /api/control-presupuestario/consultas/dashboard?idCentroCosto=1&partidasExcluidas=2&partidasExcluidas=3&partidasExcluidas=2
```

Para enviar una lista vacía desde HTTP, **omitir** todas las claves `partidasExcluidas`. No enviar `partidasExcluidas=`, `[]`, JSON ni una cadena separada por comas: no representan una colección de enteros con este contrato. Dentro de la aplicación se aceptan tanto `null` como una colección vacía.

### Respuesta 200

La respuesta sigue siendo el objeto del dashboard directamente, sin envolverlo en `data`. Se agrega el miembro obligatorio `rubrosFiltrados`, siempre un arreglo, incluido cuando no hay partidas o no se enviaron exclusiones.

Cada elemento de `rubros` y `rubrosFiltrados` tiene este contrato JSON:

```typescript
interface TableroRubro {
  idCatalogoPartida: number;
  codigo: string;
  nombre: string;
  presupuestado: number;
  comprometido: number;
  ejecutado: number;
  porcentajeDelTotal: number;
}
```

Los tres importes y el porcentaje provienen de `decimal` en .NET y se serializan como números JSON. `idCatalogoPartida` identifica el rubro después de la agrupación; puede ser un ancestro, aunque la exclusión se aplicó previamente sobre las partidas originales.

Ejemplo ilustrativo: dos raíces, con gasto de 150 y 30 respectivamente; la primera contiene una hoja excluida de gasto 60. Extracto de la respuesta para `nivel=1&partidasExcluidas=3`:

```json
{
  "resumen": {
    "presupuestado": 1900,
    "comprometido": 190,
    "ejecutado": 180,
    "desviacion": -1720,
    "desviacionPorcentaje": -90.53,
    "porcentajeEjecutado": 9.47
  },
  "rubros": [
    {
      "idCatalogoPartida": 1,
      "codigo": "1",
      "nombre": "Directos",
      "presupuestado": 1200,
      "comprometido": 120,
      "ejecutado": 150,
      "porcentajeDelTotal": 83.33
    },
    {
      "idCatalogoPartida": 6,
      "codigo": "2",
      "nombre": "Indirectos",
      "presupuestado": 700,
      "comprometido": 70,
      "ejecutado": 30,
      "porcentajeDelTotal": 16.67
    }
  ],
  "rubrosFiltrados": [
    {
      "idCatalogoPartida": 1,
      "codigo": "1",
      "nombre": "Directos",
      "presupuestado": 900,
      "comprometido": 90,
      "ejecutado": 90,
      "porcentajeDelTotal": 75
    },
    {
      "idCatalogoPartida": 6,
      "codigo": "2",
      "nombre": "Indirectos",
      "presupuestado": 700,
      "comprometido": 70,
      "ejecutado": 30,
      "porcentajeDelTotal": 25
    }
  ]
}
```

Este extracto omite `encabezado`, `semanas`, `alCorte` y `desviacionPorRubro` por brevedad; el endpoint sigue devolviéndolos con su estructura y valores originales. En este ejemplo, el total filtrado es 120; el ejecutado del resumen permanece en 180. Si se excluyen todas las partidas, `rubrosFiltrados` es `[]` y los demás miembros siguen conservándose.

### Validaciones y errores

| Caso | HTTP | Formato |
| --- | --- | --- |
| Algún ID de exclusión es cero o negativo | 400 | `ValidationProblemDetails` de `[ApiController]`, con `errors` asociado a `PartidasExcluidas`. |
| Valor no entero, vacío, separado por comas o fuera del rango de `int` | 400 | Error de model binding, en el mismo formato de validación HTTP. |
| ID positivo que no existe en el catálogo | 404 | Middleware del módulo: `error = "PARTIDA_NO_ENCONTRADA"`. Se rechaza la solicitud completa. |
| ID existente, pero sin importes en el presupuesto o fuera de la rama actual | 200 | Exclusión válida; no modifica la distribución si no tiene partidas incluidas a las que afectar. |

Ejemplo representativo de validación HTTP (el `traceId` se omite y la clave de `errors` puede incluir un prefijo del parámetro):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PartidasExcluidas": [
      "Las partidas excluidas deben tener identificadores positivos."
    ]
  }
}
```

Ejemplo de partida inexistente:

```json
{
  "ok": false,
  "error": "PARTIDA_NO_ENCONTRADA",
  "message": "La partida 999999 no existe."
}
```

La resolución de dominio también valida los IDs positivos para llamadas al handler que no pasen por HTTP, utilizando `ValidacionPresupuestariaException` y el código existente `IDENTIFICADOR_INVALIDO`. Las validaciones anteriores de centro, presupuesto, nivel, rama y monedas se conservan.

## 4. Reglas de negocio y decisiones técnicas

1. **Jerarquía real:** la relación `IdPartidaPadre` del catálogo determina descendientes; no se deducen por prefijos del código. Se utiliza el catálogo completo, incluidas partidas inactivas que puedan formar parte de datos históricos.
2. **Hoja y padre:** excluir una hoja elimina solo esa hoja; excluir un padre elimina el propio nodo y todos sus descendientes, a cualquier profundidad.
3. **Sin duplicidades:** un `HashSet<int>` mantiene la unión de exclusiones. Seleccionar padre e hijo o repetir un ID no resta montos varias veces. El recorrido es iterativo y visita cada descendiente una vez.
4. **Orden de procesamiento:** selección original de centro/presupuesto y rama → exclusión de partidas originales para el donut → agrupación por las reglas existentes → sumatoria de importes → porcentajes sobre el ejecutado neto incluido.
5. **Agrupación compartida:** `AgruparRubros` se utiliza para ambas distribuciones; conserva la selección de ancestros, el comportamiento de ramas menos profundas y el respaldo de código/nombre que ya existía para filas sin entrada de catálogo.
6. **Finanzas originales:** los importes provienen de las mismas filas de `VigenteAsync`. La feature únicamente selecciona filas y suma los importes ya calculados; no cambia el origen, cálculo ni significado de presupuestado, comprometido o ejecutado. La ejecución diaria se consulta y agrupa con el flujo original.
7. **Porcentajes:** `ejecutadoDelRubro * 100 / sumaEjecutadoIncluido`, redondeado a dos decimales con `MidpointRounding.AwayFromZero`. Si el total neto es cero, todos los porcentajes son cero. No se divide por el ejecutado del resumen original.
8. **Importes con signo:** se conservan ceros y negativos, sin recortes ni valores absolutos. Con importes mixtos puede haber porcentajes negativos o mayores a 100; con total negativo la fórmula conserva ese denominador. Por redondeo, la suma de porcentajes puede diferir de 100 por centésimas.
9. **Grupos vacíos y grupos cero:** un grupo sin filas incluidas desaparece. Si conserva partidas incluidas cuyo neto es cero, permanece con sus importes correspondientes y el porcentaje calculado por la misma fórmula.
10. **Orden y cantidad:** ambas distribuciones se ordenan por ejecutado descendente y después por código ordinal. El backend devuelve todos los rubros resultantes; no genera el grupo «Otros».
11. **Sin exclusiones:** `rubrosFiltrados` utiliza los mismos valores de `rubros`. No vuelve a calcular curvas ni otros resultados. El cambio del contrato es aditivo.
12. **Extensión futura:** `ExclusionPartidas.Resolver` recibe únicamente catálogo e IDs y devuelve un conjunto reutilizable, sin conocer DTOs HTTP, repositorios ni importes. Otros reportes podrán consumir ese conjunto explícitamente; en esta iteración solo lo consume la distribución del donut.

## 5. Pruebas y verificación

Las pruebas nuevas cubren:

- Ausencia de exclusiones y colección vacía.
- Exclusión de hoja, padre, ancestro de la rama y padre e hijo simultáneos, incluyendo IDs repetidos.
- Agrupaciones en niveles 1, 2, 3 y superiores a la profundidad del catálogo; ramas de distinta profundidad.
- Navegación con `idPartidaPadre`, con nivel automático y explícito.
- Exclusión de todas las raíces o de todas las hojas; distribución vacía.
- Presupuestos aprobados ausentes, partidas existentes sin montos y exclusiones fuera de la rama.
- Montos negativos, cero, total neto cero y total neto negativo; sumas de los tres importes y porcentajes.
- Comparación completa de la respuesta, salvo `rubrosFiltrados`, para comprobar invariabilidad de todos los demás resultados; las pruebas unitarias incluyen deliberadamente fuentes de vigente e histórico distintas.
- Model binding HTTP de parámetros repetidos, valores inválidos y respuesta 404 con el código del módulo.

Validación realizada con SDK .NET 8.0.132 y runtime 8.0.32:

```bash
dotnet build backend.sln --no-restore --nologo -m:1 -v quiet

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-build --no-restore \
  --filter 'FullyQualifiedName~Tests.Unit|FullyQualifiedName~Tests.Integration.ControlPresupuestario' \
  --logger 'trx;LogFileName=filtro-partidas-validacion.trx' \
  --results-directory /tmp/filtro-partidas-test-results --nologo -v minimal

git diff --check
```

| Verificación | Resultado |
| --- | --- |
| Compilación de la solución, API y proyecto de pruebas | Correcta, 0 errores; advertencias existentes en código ajeno a esta feature. |
| Todas las pruebas unitarias | **590 correctas**. |
| Integración de ControlPresupuestario | **95 correctas**. |
| Total de la ejecución final | **685 correctas, 0 fallidas, 0 omitidas**. |
| Pruebas nuevas incluidas en ese total | **24 unitarias del handler y 2 de integración HTTP**, con múltiples escenarios por prueba. |
| `git diff --check` | Sin errores de formato. |
| Ejemplos JSON de este documento | Los tres bloques se validaron con un parser JSON; bloques Markdown de código balanceados. |

Las pruebas se ejecutaron con el fixture real del proyecto: Testcontainers, SQL Server `2025-latest`, migraciones y host ASP.NET Core de prueba. El fixture apunta al contenedor desechable; no utiliza la base de datos local de desarrollo. Evidencia TRX de esta sesión: `/tmp/filtro-partidas-test-results/filtro-partidas-validacion.trx`.

La verificación cubre HTTP, repositorios y SQL dentro del entorno de integración, además del dominio y el handler. No se ejecutaron las integraciones de otros módulos ni pruebas en Angular, navegador o un entorno desplegado.

## 6. Integración posterior con Angular

1. Agregar `rubrosFiltrados: TableroRubro[]` al modelo de respuesta. Alimentar exclusivamente el donut con ese miembro. Las tarjetas, curva y desviaciones siguen utilizando los miembros originales.
2. Mantener el estado de selección como una colección de `idCatalogoPartida`. Para construir un selector jerárquico ya existe `GET /api/control-presupuestario/partidas`, que devuelve `idCatalogoPartida`, `idPartidaPadre`, `nivel`, `esHoja`, `codigo`, `nombre` y `activo`, entre otros campos. Ese endpoint requiere `control_presupuestario.partida.ver`, un permiso diferente del de reportes; contemplarlo en la disponibilidad del selector.
3. Si el selector debe limitarse a las partidas con presencia en el centro/presupuesto, existe `GET /api/control-presupuestario/consultas/arbol?idCentroCosto=1&idPresupuesto=10`, con el permiso de reportes. No utilizar solamente los rubros del nivel visual para inferir todo el árbol.
4. El backend ya expande padres y elimina redundancias; Angular puede enviar directamente la selección. Conservar los IDs seleccionados cuando cambie `nivel` o se navegue por una rama permite mantener la intención del filtro: un ID fuera de la rama es válido y no produce efecto en ella.
5. Construir parámetros repetidos con `HttpParams.append`; recordar que `HttpParams` es inmutable. Con selección vacía omitir el parámetro. Ejemplo orientativo, sin cambios en frontend en esta entrega:

```typescript
let params = new HttpParams().set('idCentroCosto', idCentroCosto);
if (idPresupuesto != null) params = params.set('idPresupuesto', idPresupuesto);
if (nivel != null) params = params.set('nivel', nivel);
if (idPartidaPadre != null) params = params.set('idPartidaPadre', idPartidaPadre);

for (const id of partidasExcluidas) {
  params = params.append('partidasExcluidas', id);
}

return this.http.get<Tablero>(
  '/api/control-presupuestario/consultas/dashboard', { params }
);
```

6. Elegir los cinco principales rubros y agrupar «Otros» **después** de recibir `rubrosFiltrados`. El total del donut es la suma de sus `ejecutado`, no `resumen.ejecutado`. Si se calcula el porcentaje de «Otros», usar el total filtrado y contemplar el redondeo.
7. Mostrar un estado vacío cuando `rubrosFiltrados` sea `[]`. Si hay rubros cuyo total neto es cero, sus porcentajes son cero. La elección visual para importes negativos o porcentajes fuera del rango 0–100 corresponde al frontend; la API conserva los valores financieros con signo y no los adapta a las limitaciones del gráfico.
8. Procesar errores 400 tanto desde `errors` de la validación HTTP como desde el formato del middleware (`error`/`message`) utilizado por el módulo; los 404 de partidas inexistentes usan este último formato.
9. Incorporar las exclusiones a cualquier clave de caché de la consulta. Si se cambia la selección rápidamente, descartar respuestas anteriores para que el donut represente la última petición.
