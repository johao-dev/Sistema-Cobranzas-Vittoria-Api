# Implementación de Infrastructure — Notificaciones

Fecha: 10 de noviembre de 2026.

## 1. Resultado y alcance

El módulo cuenta con repositorios SQL Server/Dapper, resolución de destinatarios mediante Strategy, renderizado HTML restringido, transporte MailKit con TLS, worker secuencial y composición DI integrada en `Program.cs`.

Se verificaron **219 pruebas: 182 del módulo sin SQL Server, 32 con SQL Server aislado y 5 existentes de arranque/Swagger de la API**, sin fallos ni omisiones. La primera agrupación incluye pruebas de protocolo SMTP sobre un servidor local con TLS, además de Domain, Application y worker. La compilación de la API y del proyecto de pruebas terminó sin errores.

El worker está **deshabilitado de forma predeterminada**. No se configuraron credenciales corporativas ni se enviaron mensajes a usuarios reales. La validación no acredita conectividad con cPanel, recepción en buzones, comportamiento del alojamiento productivo ni aplicación de migraciones en una base objetivo.

La solicitud de implementación prevalece sobre las partes anteriores de [la propuesta técnica](propuesta-infrastructure-notificaciones.md): cada módulo posee sus transacciones, el lote es de 3 y la integración futura será por eventos en memoria después de confirmar Compras. No se implementó un contexto transaccional compartido.

## 2. Archivos creados y modificados

Las rutas siguientes parten de `Cobranzas_Vittoria/`, salvo donde se indique `docs/`.

### Archivos creados

| Archivo | Responsabilidad |
| --- | --- |
| `Notificaciones/NotificacionesModule.cs` | Composición explícita, opciones y validación al iniciar. |
| `Notificaciones/Application/Services/DestinatarioResolver.cs` | Fachada que selecciona una estrategia por tipo. |
| `Notificaciones/Domain/Exception/PlantillaCorreoInvalidaException.cs` | Error de configuración de estructura, contexto, asunto o enlace. |
| `Notificaciones/Infrastructure/Persistence/Repository/NotificacionRepository.cs` | Registro, lectura, reclamación, recuperación y guardado condicionado. |
| `Notificaciones/Infrastructure/Persistence/Repository/ConfiguracionNotificacionRepository.cs` | Lectura consistente y guardado transaccional de reglas/instrucciones y plantillas. |
| `Notificaciones/Infrastructure/Persistence/Entity/NotificacionRegistro.cs` | Proyección de cabecera y destinatarios, conversión de textos SQL y restauración UTC. |
| `Notificaciones/Infrastructure/Destinatarios/DestinatarioRolResolver.cs` | Consulta de usuarios activos asociados al rol. |
| `Notificaciones/Infrastructure/Destinatarios/DestinatarioUsuarioResolver.cs` | Consulta de un usuario activo específico. |
| `Notificaciones/Infrastructure/Plantillas/PlantillaRenderer.cs` | Análisis del árbol HTML y sustitución según el contexto permitido. |
| `Notificaciones/Infrastructure/Email/SmtpOptions.cs` | Configuración, modos TLS y límites temporales. |
| `Notificaciones/Infrastructure/Email/SmtpEmailSender.cs` | Un intento SMTP, clasificación de fallos y cierre seguro. |
| `Notificaciones/Infrastructure/BackgroundJobs/NotificacionesWorkerOptions.cs` | Habilitación explícita y frecuencia. |
| `Notificaciones/Infrastructure/BackgroundJobs/NotificacionesWorker.cs` | Recuperación y procesamiento secuencial con scopes. |
| `Cobranzas_Vittoria.Tests/NotificacionesSql/NotificacionesPersistenceTests.cs` | Persistencia, concurrencia, configuración, estrategias SQL y flujo completo aislado. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Infrastructure/DestinatarioResolverTests.cs` | Selección, orden, duplicados, estrategia ausente y cancelación. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Infrastructure/PlantillaRendererTests.cs` | Variables, codificación, enlaces y contextos rechazados. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Infrastructure/ServidorSmtpPrueba.cs` | Servidor SMTP de prueba en loopback, con TLS y respuestas controladas. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Infrastructure/SmtpEmailSenderTests.cs` | Protocolo, STARTTLS/TLS implícito, rechazos, cancelación, timeout y BCC. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Infrastructure/NotificacionesWorkerTests.cs` | DI, inicio seguro, ciclos, recuperación, suspensión y reinicio. |
| `docs/reporte-infrastructure-notificaciones.md` | Este reporte. |
| `docs/evidencias/notificaciones-validacion.json` | Contadores y tiempos extraídos de los TRX de las tres ejecuciones aprobadas. |

### Archivos modificados

| Archivo | Cambio |
| --- | --- |
| `Cobranzas_Vittoria.csproj` | Referencias a MailKit y HtmlAgilityPack. |
| `Program.cs` | Invocación de `AddNotificaciones`. |
| `appsettings.json` | Worker deshabilitado y política inicial explícita. |
| `Notificaciones/Application/Options/NotificacionProcesamientoOptions.cs` | Tamaño de lote predeterminado: 3. |
| `Notificaciones/Application/Services/IDestinatarioResolver.cs` | Propiedad `Tipo` para cada estrategia. |
| `Notificaciones/Application/ProcesarEvento/ProcesarEventoHandler.cs` | Consumo de la fachada y eliminación de la comprobación particular Rol/Usuario. |
| `Notificaciones/Application/ProcesarNotificacionesPendientes/ProcesarNotificacionesPendientesHandler.cs` | Propagación de fallos operativos sin agotar el lote; logs por intento y duración. |
| `Notificaciones/Domain/Exception/EnvioCorreoException.cs` | Indicador `RequiereIntervencion`, conservando los usos anteriores del constructor. |
| `Notificaciones/Domain/Persistence/INotificacionRepository.cs` | Documentación ajustada a la propiedad local de conexión/transacción. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/DoblesNotificaciones.cs` | Dobles adaptados al contrato Strategy. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/ProcesarEventoHandlerTests.cs` | Construcción del handler mediante fachada con las dos estrategias. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/NotificacionProcesamientoOptionsTests.cs` | Expectativa de lote 3. |
| `Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/ProcesarNotificacionesPendientesHandlerTests.cs` | Lote 3 y aserción del log de conflicto compatible con logs de resultado. |
| `Cobranzas_Vittoria.Tests/Setup/CustomWebApplicationFactory.cs` | Deshabilitación explícita del worker en las pruebas existentes de API, aunque el entorno externo lo habilite. |

## 3. Dependencias y migraciones

| Dependencia directa nueva | Versión | Uso |
| --- | --- | --- |
| MailKit | 4.17.0 | Cliente SMTP; restaura también MimeKit 4.17.0. |
| HtmlAgilityPack | 1.13.1 | Distingue nodos de texto, elementos, comentarios y atributos. Evita implementar un analizador HTML propio. |

La restauración seleccionó ensamblados `lib/net8.0` de ambas dependencias. Se verificaron las versiones resueltas mediante `dotnet list ... package --include-transitive` y la compatibilidad práctica mediante compilación y pruebas en .NET 8. MailKit incorpora dependencias criptográficas transitivas, entre ellas BouncyCastle.Cryptography 2.6.2 y System.Security.Cryptography.Pkcs 8.0.1. No se añadió un motor de plantillas, proveedor de mensajería ni framework de jobs.

No hicieron falta migraciones nuevas, procedimientos ni tipos tabla: las operaciones utilizan SQL parametrizado y las tablas/índices existentes. **V3.0.0–V3.0.2 permanecen intactas**. Las pruebas SQL aplicaron esas migraciones originales y el DDL real de Seguridad en bases desechables.

## 4. Persistencia y concurrencia

### Registro idempotente

`RegistrarSiNoExisteAsync` admite una entidad nueva pendiente, sin intentos y con fecha UTC. Abre su propia transacción `SERIALIZABLE`, consulta `IdEvento` con `UPDLOCK, HOLDLOCK` e inserta cabecera y destinatarios antes de confirmar.

El bloqueo de rango protege el caso en que todavía no existe el GUID. La restricción única `UQ_Notificacion_IdEvento` sigue presente como garantía SQL. Si la ocurrencia existe, se devuelve su identificador con `Creada = false`, sin modificar nada.

No se capturan indiscriminadamente errores 2601/2627 ni se convierten fallos de integridad en duplicados. El duplicado normal se resuelve mediante la lectura protegida; los demás errores se propagan. La disposición de una transacción no confirmada revierte también cualquier destinatario insertado antes del fallo. Se comprobó rollback por una inserción inválida y propagación de la violación de FK.

### Reclamación

Una CTE `TOP (@Cantidad)`, ordenada por creación e ID, selecciona pendientes disponibles y ejecuta `UPDATE ... OUTPUT` dentro de una transacción `READ COMMITTED`:

- Cambia a `PROCESANDO` e incrementa `Intentos` una sola vez.
- Establece `FechaInicioProcesamiento` y limpia `FechaProximoIntento`.
- Captura los IDs actualizados y reconstruye sus cabeceras y destinatarios originales.
- Confirma y dispone los recursos antes de retornar a Application.

Se usan `UPDLOCK, READPAST, READCOMMITTEDLOCK`. El último hint fuerza lectura con bloqueos incluso con RCSI activado. No se combina con `ROWLOCK`, que pertenece al mismo grupo de granularidad; tampoco se usa `NOLOCK`. La elección sigue las restricciones documentadas de [table hints de SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17).

Las pruebas ejecutaron reclamaciones concurrentes con RCSI `OFF` y `ON`, sin duplicar filas ni incrementos. `READPAST` puede omitir candidatas bloqueadas por el recorrido concurrente; un lote puede quedar incompleto y el siguiente ciclo recupera pendientes restantes. No garantiza omitir bloqueos de página ni elimina toda posibilidad de espera o interbloqueo SQL.

### Resultado y recuperación

`GuardarResultadoAsync` valida la correspondencia entre entidad e intento esperado y solo admite `PENDIENTE`, `ENVIADA` o `ERROR`. El `UPDATE` exige ID, estado persistido `PROCESANDO` e intento esperado. Dos guardados concurrentes dejan un solo ganador; un resultado anterior no sobrescribe un intento posterior.

Solo se guardan estado, fechas del resultado y último error. Nunca se actualizan contenido, destinatarios, fecha de creación ni contador de intentos desde el resultado. Se probó incluso con una entidad reconstruida que contenía otro asunto, otro HTML y una colección vacía.

`ObtenerProcesamientosVencidosAsync` lee un lote con inicio estrictamente anterior al límite; no reclama ni modifica registros. Application aplica `RegistrarFallo` y guarda con el intento observado. Las consultas de cabecera y destinatarios usan una transacción de lectura consistente y terminan antes de retornar.

### Configuración y reconstrucción

La lectura de evento, regla, instrucciones, catálogo y plantilla se realiza en una transacción `SERIALIZABLE`, sin filtrar componentes desactivados. Distingue evento inexistente, componentes ausentes y componentes deshabilitados. Una prueba mantuvo una actualización de regla y plantilla sin confirmar y comprobó que la lectura esperaba y devolvía una configuración coherente.

Los guardados de reglas/instrucciones y de plantillas tienen transacciones propias, protegen la unicidad por evento y comprueban la correspondencia de identidades. Las instrucciones persistidas se conservan/actualizan por su ID y pertenencia; se eliminan las retiradas y se insertan las nuevas. Se rechazan instrucciones ajenas y tipos cuyo ID no corresponda al nombre del catálogo. Para obtener las identidades asignadas a altas se vuelve a consultar la configuración, dado que los contratos de guardado retornan `Task`.

Cada operación es atómica. Dos llamadas separadas para guardar regla y plantilla siguen siendo dos operaciones independientes; el contrato no incorpora una edición administrativa conjunta.

La reconstrucción utiliza las fábricas `Reconstruir`, conversiones explícitas de estados y participaciones (`TO`, `CC`, `BCC`), y nombres `ROL`/`USUARIO` del catálogo. No se convierten IDs SQL directamente al enum. `datetime2` se marca como UTC mediante `DateTime.SpecifyKind`, sin desplazar datos por la zona local. Se conserva la precisión de segundos definida por el esquema.

## 5. Resolución de destinatarios

`DestinatarioResolver`, en Application, recibe `IEnumerable<IDestinatarioResolver>` y construye un registro por `Tipo`. Rechaza tipos duplicados al construirse, lanza `TipoDestinatarioNoSoportadoException` si falta una estrategia y propaga el token. No usa reflexión ni `IServiceProvider` para seleccionar.

`ProcesarEventoHandler` conoce únicamente la fachada. Conserva su validación de direcciones mediante el dominio y su deduplicación por correo/participación. La fachada no implementa la interfaz de estrategias ni forma parte de su colección DI.

Las estrategias consultan directamente proyecciones mínimas de `seguridad.Usuario` y `seguridad.UsuarioRol`, usando el mecanismo de conexiones del ERP y parámetros Dapper. La consulta por rol utiliza `EXISTS`, evitando duplicar usuarios por asignaciones repetidas.

Se excluyen usuarios inactivos y correos nulos, vacíos o compuestos de espacios. No se filtra por proyecto, comprador ni estado del rol: la política acordada filtra el estado del usuario. Las estrategias no asignan TO/CC/BCC. Los roles 2 y 6 permanecen en la configuración SQL original, sin condiciones especiales en código.

## 6. Renderizado

Se procesa el árbol HTML con HtmlAgilityPack. Una expresión regular reconoce solamente la gramática de los marcadores; no decide los contextos ni reemplaza indiscriminadamente todo el documento.

| Contexto | Política implementada |
| --- | --- |
| Asunto | Variables sin codificación HTML y rechazo de CR/LF resultantes. |
| Texto HTML | Inserción con `WebUtility.HtmlEncode`, sin interpretar el valor como HTML. |
| Enlace | Únicamente `href` de `a`, cuyo valor dinámico completo sea un marcador entre comillas. |
| URL | HTTP/HTTPS absoluta, host presente, sin controles ni credenciales embebidas; valor codificado para el atributo. |
| Otros atributos dinámicos | Rechazados, incluso `title`, `class` y `style`. |
| Elementos y atributos estáticos | Lista explícita de estructura de correo; scripts, estilos, eventos, iframes y atributos desconocidos se rechazan. |
| Comentarios | Sin marcadores. |
| Variables ausentes o nulas | `VariablePlantillaFaltanteException`; los nombres son sensibles a mayúsculas incluso si el diccionario recibido no lo es. |
| HTML inválido o marcador descartado por el parser | `PlantillaCorreoInvalidaException`; se verifica que ningún marcador original haya quedado sin procesar. |

No existe una lista fija de variables por evento. Se probaron la plantilla SQL original de `REQUERIMIENTO_CREADO` y plantillas de otros eventos. Los valores llegan ya formateados por el emisor. Una plantilla que necesite CSS o atributos adicionales requerirá ampliar explícitamente la política y sus pruebas.

## 7. SMTP y configuración requerida

El adaptador crea un cliente por envío. Admite `StartTls` obligatorio o `SslOnConnect`; no hay modo de transporte en claro ni fallback oportunista. El constructor de producción mantiene la validación normal de certificados de MailKit. La confianza privada de los certificados locales se inyecta únicamente desde los tests mediante un constructor interno.

El remitente predeterminado es `notificaciones@vittoriainmobiliaria.com`. El dominio de alojamiento de la API no se usa para derivar el servidor ni el remitente. El host, usuario y contraseña quedan sin configurar hasta que se aporten datos reales.

Configuración disponible bajo `Notificaciones:Smtp`:

| Propiedad | Valor predeterminado / requisito |
| --- | --- |
| `Host` | Vacío; aportar el hostname real de cPanel. |
| `Puerto` | 587; confirmar y sustituir según la configuración real. |
| `ModoTls` | `StartTls`; elegir `SslOnConnect` si corresponde al puerto configurado por cPanel. |
| `Usuario` | Vacío; aportar el usuario SMTP real de la cuenta. |
| `Contrasena` | Vacía; secreto externo al repositorio. |
| `Remitente` | `notificaciones@vittoriainmobiliaria.com`. |
| `NombreVisible` | `Notificaciones Vittoria`. |
| `TimeoutOperacionSegundos` | 30. |
| `TimeoutTotalSegundos` | 60 para conexión, autenticación y envío completos. |
| `TimeoutCierreSegundos` | 5 adicionales. |

Usar secretos del entorno o User Secrets en desarrollo. Las variables de entorno correspondientes son `Notificaciones__Smtp__Host`, `Notificaciones__Smtp__Puerto`, `Notificaciones__Smtp__ModoTls`, `Notificaciones__Smtp__Usuario` y `Notificaciones__Smtp__Contrasena`, entre otras. No guardar contraseñas en `appsettings*.json`, scripts, historial compartido ni logs.

Antes de activar el módulo, obtener los datos reales de la cuenta en cPanel, verificar resolución DNS y conectividad desde el servidor del ERP, cadena de confianza y nombre del certificado, autenticación y autorización del remitente. Los valores predeterminados de puerto y modo no representan una verificación de la configuración corporativa.

### Resultados de transporte

| Situación | Comportamiento |
| --- | --- |
| `SendAsync` confirma aceptación | Éxito; Application marca `ENVIADA`. No acredita recepción final. |
| Respuesta SMTP 4xx | Transitoria. |
| Respuesta SMTP 5xx de mensaje/destinatario | Permanente para ese mensaje. |
| Autenticación 454 | Transitoria, sin suspensión operativa. |
| Credenciales rechazadas, certificado/TLS inválido o remitente rechazado | Error operativo; se suspende el worker. |
| Desconexión/timeout durante el envío sin confirmación | Clasificación desconocida. |
| Fallo de conexión previo al envío | Transitorio, salvo fallo operativo identificado. |
| Cancelación del consumidor | Se propaga sin convertirla en rechazo. |
| Fallo de `QUIT` después de aceptación | Se registra de forma segura; no convierte el envío en fallo. |

Se usan los códigos y la fase SMTP. Para autenticación, MailKit conserva el código en una `SmtpCommandException` interna; no se inspeccionan palabras del mensaje. El adaptador no implementa reintentos.

TO y CC forman sus encabezados normales; BCC participa exclusivamente en el sobre SMTP. Se deduplican direcciones del sobre sin retirar participaciones de los encabezados visibles.

La política inicial aborta ante cualquier destinatario rechazado antes de transmitir DATA. Las pruebas comprobaron el caso de RCPT aceptado seguido de RCPT rechazado, con y sin PIPELINING: no se transmitió el cuerpo. No se implementa entrega parcial deliberada ni persistencia por destinatario. Estas decisiones se apoyan en el [contrato de MailKit para rechazos](https://mimekit.net/docs/html/M_MailKit_Net_Smtp_SmtpClient_OnRecipientNotAccepted.htm) y se verificaron contra la versión incorporada.

## 8. Worker, tiempos y composición

El worker es un `BackgroundService` dentro de la API. Cada ciclo habilitado y no suspendido crea un scope, ejecuta recuperación, procesa pendientes secuencialmente, registra resultados y espera 30 segundos después de terminar. No se acumulan ciclos vencidos ni se inicia un segundo ciclo mientras el anterior continúa.

Política inicial: lote 3, máximo 3 intentos, reintento 5 minutos y recuperación después de 10 minutos. El inicio del procesamiento se fija al reclamar todo el lote.

Al habilitar el worker, la validación de arranque exige:

```text
(TimeoutTotalSegundos + TimeoutCierreSegundos + 30) × TamanoLote + 60
    < TimeoutProcesamientoMinutos × 60
```

Los 30 segundos por mensaje reservan tiempo para guardar SQL y los 60 finales son margen adicional. Con los valores predeterminados, el presupuesto validado es 345 segundos, menor que los 600 de recuperación. SMTP queda acotado a 195 segundos para tres envíos incluyendo cierres. Es un control de coherencia de configuración, no una garantía ante una pausa del proceso o una indisponibilidad indefinida de infraestructura.

Las opciones se fijan por proceso mediante `IOptions`; aplicar cambios operativos requiere reiniciar la API. Ante `RequiereIntervencion`, el handler no marca todos los mensajes como errores permanentes y propaga el fallo. El worker activa una suspensión en memoria y deja de recuperar/reclamar trabajo. Los intentos ya reclamados permanecen en `PROCESANDO` para su recuperación posterior.

Para reanudar: corregir la causa, actualizar los secretos/configuración y reiniciar la API. Los registros cuyo inicio haya vencido se recuperan con la política existente y, si corresponde, esperan su intervalo de reintento. Reiniciar no reinicia contadores ni reactiva automáticamente mensajes `ERROR`.

| Servicio | Ciclo de vida |
| --- | --- |
| Repositorios, estrategias, fachada, handlers y sender | Scoped. |
| Renderer | Singleton sin estado por renderizado. |
| `TimeProvider.System` | Singleton mediante `TryAddSingleton`, sustituible en pruebas. |
| Worker | Singleton alojado que crea scopes. |

`ValidateOnStart` comprueba las opciones y, si se habilita procesamiento, la configuración SMTP completa y el presupuesto del lote. Con el worker deshabilitado se permite iniciar sin host ni secretos SMTP. El sender valida también su configuración antes de un envío directo. La fábrica de pruebas de API fuerza el worker deshabilitado; las pruebas de composición lo habilitan únicamente con un sender de prueba.

Los logs incluyen ID, intento, duración, resultado, guardados, conflictos, recuperados y clasificación segura de transporte. No se serializan las excepciones originales del proveedor ni se registran direcciones, variables, cuerpos o credenciales. `Guardadas` incluye reprogramaciones y fallos persistidos; no equivale a mensajes aceptados.

## 9. Validación ejecutada

Entorno: SDK .NET 8.0.132, NUnit y herramientas existentes. Las pruebas de SQL utilizaron `mcr.microsoft.com/mssql/server:2025-latest` mediante Testcontainers y bases creadas para el módulo. El contenedor de la prueba existente de API aplicó las migraciones completas a otra base desechable.

| Validación | Resultado real |
| --- | --- |
| Restauración del proyecto de pruebas y API | Correcta; MailKit 4.17.0 y HtmlAgilityPack 1.13.1 resueltos. |
| Compilación del proyecto de pruebas y API | 0 errores; 39 advertencias en código preexistente, ninguna en los archivos del módulo de notificaciones. |
| Domain/Application/Infrastructure sin SQL Server | 182 aprobadas; 0 fallos; 0 omitidas. Incluye SMTP real en loopback con TLS y worker. |
| Persistencia SQL, RCSI OFF y ON | 32 aprobadas; 0 fallos; 0 omitidas. |
| Suite existente `SwaggerJsonGenerationTests` | 5 aprobadas; 0 fallos; 0 omitidas. Verifica arranque de API y documentos Swagger. |
| `git diff --check` | Sin errores. |
| `appsettings.json` | JSON válido. |

Los [contadores conservados](evidencias/notificaciones-validacion.json) se extrajeron de los TRX. Los archivos completos de esta sesión están en `/tmp/notificaciones-resultados/`: `notificaciones-unit.trx`, `notificaciones-sql.trx` y `notificaciones-arranque-api.trx`. Son evidencia local temporal; se pueden regenerar con estos comandos desde la raíz del backend:

```bash
dotnet restore Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj
dotnet build Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj --no-restore

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-build --no-restore --filter 'FullyQualifiedName~Tests.Unit.Notificaciones' \
  --logger 'trx;LogFileName=notificaciones-unit.trx' --results-directory /tmp/notificaciones-resultados

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-build --no-restore --filter 'FullyQualifiedName~Tests.NotificacionesSql' \
  --logger 'trx;LogFileName=notificaciones-sql.trx' --results-directory /tmp/notificaciones-resultados

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-build --no-restore --filter 'FullyQualifiedName~Tests.Integration.Swagger.SwaggerJsonGenerationTests' \
  --logger 'trx;LogFileName=notificaciones-arranque-api.trx' --results-directory /tmp/notificaciones-resultados
```

La restauración necesitó red fuera del sandbox y VSTest necesitó permiso para abrir sockets locales. Docker también requirió acceso fuera del sandbox. Las ejecuciones finales anteriores se completaron con esos permisos. No se atribuyeron esos bloqueos del entorno al código.

Durante el desarrollo las pruebas detectaron la excepción específica `SslHandshakeException`, la cancelación derivada `TaskCanceledException`, la agregación de errores de opciones y el comportamiento de lotes incompletos con `READPAST`; se corrigió el tratamiento o la expectativa correspondiente antes de las ejecuciones finales aprobadas.

### Cobertura funcional comprobada

- Cabecera/destinatarios atómicos, rollback, idempotencia y carreras por `IdEvento`.
- Reclamación concurrente, contador único, lotes acotados, reintentos futuros y recuperación estricta sin modificación de registros.
- Resultados concurrentes, estados permitidos, rechazo de intentos anteriores e inmutabilidad de contenido/destinatarios.
- Configuración ausente/deshabilitada, altas/actualizaciones, IDs de catálogo, pertenencia de instrucciones y lectura durante modificaciones concurrentes.
- Usuarios por rol y usuario, múltiples usuarios, asignaciones repetidas, inactivos y correos nulos/vacíos.
- Selección Strategy, duplicados, ausencia de implementación, DI y cancelación.
- Plantilla SQL inicial, otros eventos, codificación HTML, URL, atributos, comentarios, scripts y CR/LF.
- SMTP con TLS implícito y STARTTLS, 4xx/5xx, autenticación, certificado rechazado, timeout, cancelación, resultado incierto y cierre tras aceptación.
- TO/CC/BCC y negociación RCPT parcial con y sin PIPELINING.
- Worker deshabilitado, ejecución periódica, lote 3, recuperación antes de envío, ausencia de superposición, cancelación, suspensión y recuperación tras reinicio simulado.
- Flujo Application → SQL → SMTP local → `ENVIADA`, en ambos modos de aislamiento.

No se ejecutó la totalidad de las pruebas de integración de todos los demás módulos. La regresión externa comprobada corresponde al arranque/Swagger; Domain y Application de Notificaciones se ejecutaron completos.

## 10. Validación manual y operación pendiente

1. Ejecutar los comandos anteriores con Docker disponible. Las pruebas SQL y SMTP son aisladas y no requieren credenciales corporativas.
2. Revisar que las migraciones V3.0.0–V3.0.2 estén aplicadas en la base destinada a validación. No confundir los contenedores de pruebas con esa base.
3. Mantener `Notificaciones:Worker:Habilitado=false` mientras se preparan host, puerto, TLS y secretos reales. Confirmar la cuenta remitente y destinatarios autorizados para la prueba.
4. Revisar la configuración persistida del evento y sus usuarios activos. Los roles 2 y 6 pueden resolver más de un usuario; la prueba corporativa requiere controlar previamente quién recibirá el mensaje.
5. En una herramienta de diagnóstico o prueba del mismo proceso, invocar `ProcesarEventoHandler.HandleAsync` con GUID estable, código de evento, referencia y variables. Se puede repetir el GUID para comprobar que no se crea otra notificación. No se añadió un endpoint para ejecutar esta operación.
6. Con el worker aún deshabilitado, comprobar la instantánea `PENDIENTE`, asunto/HTML codificados y destinatarios en SQL. Preferir primero la prueba completa automatizada con SMTP local.
7. Para la prueba corporativa autorizada, habilitar `Notificaciones__Worker__Habilitado=true`, reiniciar la API y observar reclamación, aceptación y persistencia de `ENVIADA`. Comprobar recepción final y confidencialidad de BCC por separado.
8. Verificar en un entorno controlado los logs de fallos y conflictos. Ante un fallo operativo, confirmar suspensión, corregir la causa y reiniciar; respetar los 10 minutos de recuperación y los 5 de reintento cuando correspondan.
9. Asegurar que el alojamiento mantiene activo el proceso de API aunque no reciba tráfico HTTP; una API detenida o suspendida no ejecuta el worker.

## 11. Límites y siguiente integración

SQL garantiza idempotencia del registro y control del intento persistido. SMTP no proporciona entrega exactamente una vez: si el proveedor acepta un correo y la API se interrumpe antes de guardar el resultado, la recuperación puede producir un duplicado. La misma incertidumbre existe si se pierde la respuesta de aceptación. No hay garantía de entrega final al buzón a partir del estado `ENVIADA`.

Los tiempos se validaron con la política inicial, sin prueba de carga productiva ni benchmark de grandes colas. Los bloqueos SQL pueden causar esperas; los errores del ciclo se registran y se vuelven a evaluar en un ciclo posterior. La suspensión operativa es local al proceso y se reanuda por reinicio; no hay panel ni endpoint administrativo nuevo.

El paso siguiente sigue pendiente: definir `RequerimientoCreadoEvent`, publicador compartido en memoria y suscriptor de Notificaciones. Compras debe confirmar primero su propia operación y después publicar. El suscriptor invocará `ProcesarEventoHandler`; un fallo de generación no debe revertir un requerimiento ya confirmado.

No se modificaron `CrearRequerimientoHandler` ni el repositorio de Requerimientos. No se añadieron endpoints administrativos, Angular, notificaciones internas, transacciones intermodulares, Outbox, brokers, Hangfire ni paralelismo. Antes de persistir la notificación, los futuros eventos en memoria pueden perderse si el proceso se interrumpe; esa limitación aceptada permanece pendiente de la integración futura.
