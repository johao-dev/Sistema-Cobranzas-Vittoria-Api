# Implementación de Application — Notificaciones

Fecha: 10 de noviembre de 2026.

## 1. Resultado y alcance

Se implementaron los tres casos de uso de Application: generación por evento, procesamiento de pendientes y recuperación de intentos interrumpidos. La orquestación utiliza las entidades, fábricas, validaciones y excepciones existentes del dominio. Los contratos externos no dependen de Dapper, SQL Server, MailKit ni MimeKit. No se añadieron paquetes NuGet.

Se revisaron `Notificaciones/Domain`, las migraciones V3.0.0, V3.0.1 y V3.0.2, handlers de Compras y Seguridad, pruebas NUnit con stubs manuales y el manejo de conexiones/transacciones del ERP. Las migraciones se leyeron como especificación; no se ejecutaron ni se modificaron.

El resultado está validado como orquestación unitaria. No hay implementaciones productivas de persistencia, resolución de destinatarios, renderizado ni transporte, ni registro de esos adaptadores en DI. Los dobles de prueba existen exclusivamente en el proyecto de pruebas.

## 2. Archivos completados, creados y modificados

### Application

Rutas relativas a `Cobranzas_Vittoria/Notificaciones/Application/`:

| Archivo | Cambio y responsabilidad |
| --- | --- |
| [ProcesarEvento/ProcesarEventoCommand.cs](../Cobranzas_Vittoria/Notificaciones/Application/ProcesarEvento/ProcesarEventoCommand.cs) | Completado: identidad de ocurrencia, código funcional, referencia y variables. |
| [ProcesarEvento/ProcesarEventoHandler.cs](../Cobranzas_Vittoria/Notificaciones/Application/ProcesarEvento/ProcesarEventoHandler.cs) | Completado: consulta, habilitación, resolución, renderizado, creación y registro idempotente. |
| [ProcesarEvento/ProcesarEventoResult.cs](../Cobranzas_Vittoria/Notificaciones/Application/ProcesarEvento/ProcesarEventoResult.cs) | Completado: estado, ID persistido y motivo de omisión. Se conservó el nombre del archivo previamente creado. |
| [ProcesarNotificacionesPendientes/ProcesarNotificacionesPendientesHandler.cs](../Cobranzas_Vittoria/Notificaciones/Application/ProcesarNotificacionesPendientes/ProcesarNotificacionesPendientesHandler.cs) | Completado: reclamación, envío y guardado condicionado por intento. |
| [RecuperarNotificacionesInterrumpidas/RecuperarNotificacionesInterrumpidasHandler.cs](../Cobranzas_Vittoria/Notificaciones/Application/RecuperarNotificacionesInterrumpidas/RecuperarNotificacionesInterrumpidasHandler.cs) | El archivo vacío `ProcesarNotificacionesInterrumpidasHandler.cs` de esta carpeta se renombró según el caso de uso solicitado y se completó. |
| [Services/IDestinatarioResolver.cs](../Cobranzas_Vittoria/Notificaciones/Application/Services/IDestinatarioResolver.cs) | Completado: contrato de resolución por instrucción ROL o USUARIO. |
| [Services/IPlantillaRenderer.cs](../Cobranzas_Vittoria/Notificaciones/Application/Services/IPlantillaRenderer.cs) | Completado: contrato de renderizado de asunto y HTML con obligaciones de seguridad. |
| [Services/IEmailSender.cs](../Cobranzas_Vittoria/Notificaciones/Application/Services/IEmailSender.cs) | Completado: contrato de aceptación de mensajes por el proveedor. |
| [Services/DireccionCorreo.cs](../Cobranzas_Vittoria/Notificaciones/Application/Services/DireccionCorreo.cs) | Creado: dirección y nombre, sin identidad ni estado de persistencia. |
| [Services/ContenidoCorreoRenderizado.cs](../Cobranzas_Vittoria/Notificaciones/Application/Services/ContenidoCorreoRenderizado.cs) | Creado: asunto y HTML definitivos. |
| [Services/SolicitudEnvioCorreo.cs](../Cobranzas_Vittoria/Notificaciones/Application/Services/SolicitudEnvioCorreo.cs) | Creado: contenido y colecciones TO, CC y BCC independientes del proveedor. |
| [Options/NotificacionProcesamientoOptions.cs](../Cobranzas_Vittoria/Notificaciones/Application/Options/NotificacionProcesamientoOptions.cs) | Completado: valores iniciales y validaciones con DataAnnotations. |
| [ResultadoProcesamientoLote.cs](../Cobranzas_Vittoria/Notificaciones/Application/ResultadoProcesamientoLote.cs) | Creado: cantidades evaluadas, guardadas y conflictos de concurrencia. |

### Domain y pruebas

| Archivo | Cambio |
| --- | --- |
| [Domain/Model/Notificacion.cs](../Cobranzas_Vittoria/Notificaciones/Domain/Model/Notificacion.cs) | Parámetro opcional `permitirReintento` en `RegistrarFallo`; permite finalizar fallos permanentes conservando intentos e invariantes. |
| [Domain/Persistence/INotificacionRepository.cs](../Cobranzas_Vittoria/Notificaciones/Domain/Persistence/INotificacionRepository.cs) | Consulta de procesamientos vencidos y aclaración de participación en transacciones del emisor. |
| [ProcesarEventoHandlerTests.cs](../Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/ProcesarEventoHandlerTests.cs) | Creado: generación, omisiones, errores, destinatarios, idempotencia, otros eventos y cancelación. |
| [ProcesarNotificacionesPendientesHandlerTests.cs](../Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/ProcesarNotificacionesPendientesHandlerTests.cs) | Creado: envíos, reintentos, agotamiento, fallos permanentes, concurrencia y errores no recuperables. |
| [RecuperarNotificacionesInterrumpidasHandlerTests.cs](../Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/RecuperarNotificacionesInterrumpidasHandlerTests.cs) | Creado: timeout, recuperación, reenvío programado y resultados obsoletos. |
| [NotificacionProcesamientoOptionsTests.cs](../Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/NotificacionProcesamientoOptionsTests.cs) | Creado: valores iniciales y rechazo de configuraciones inválidas antes de operar. |
| [DoblesNotificaciones.cs](../Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/Notificaciones/Application/DoblesNotificaciones.cs) | Creado: stubs manuales, datos y reloj controlado. El repositorio de pruebas utiliza copias para simular resultados obsoletos sin modificar por referencia la fila almacenada. |
| [FalloPermanenteTests.cs](../Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/Notificaciones/Domain/FalloPermanenteTests.cs) | Creado: transición permanente, invariantes y ausencia de cambios parciales. |
| [Setup/GlobalSetupFixture.cs](../Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Setup/GlobalSetupFixture.cs) | Namespace acotado a `Cobranzas_Vittoria.Tests.Integration`: NUnit deja de iniciar SQL Server para las pruebas unitarias. Las referencias existentes desde integración compilan. |
| [Unit/Importacion/CsvFileParserTests.cs](../Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Unit/Importacion/CsvFileParserTests.cs) | Registro explícito de Windows-1252 en su preparación: estas pruebas dependían de la inicialización incidental de la API. No se modificó el parser productivo. |
| [Este reporte](reporte-application-notificaciones.md) | Creado: decisiones, contratos, validación y pendientes. |

Se conservaron los demás cambios previos del workspace, incluido `docs/reporte-dominio-notificaciones.md` y la eliminación previa del `.gitKeep` de Application.

## 3. Generación por evento

`ProcesarEventoHandler.HandleAsync(command, cancellationToken)` consulta la configuración completa sin filtrar componentes desactivados en persistencia.

| Situación | Resultado |
| --- | --- |
| Evento no existente en el catálogo | `Omitida / EventoSinSuscripcion`. |
| Evento existente sin regla | `ConfiguracionEventoIncompletaException`. |
| Regla desactivada | `Omitida / ReglaDesactivada`, incluso si falta plantilla. |
| Regla activa sin plantilla | `ConfiguracionEventoIncompletaException`. |
| Plantilla desactivada | `Omitida / PlantillaDesactivada`. |
| Regla y plantilla activas sin instrucciones | `ConfiguracionEventoIncompletaException`. |
| Resolución sin ninguna dirección válida | `NotificacionSinDestinatariosException`. |
| Tipo de destinatario no soportado | `TipoDestinatarioNoSoportadoException`. |
| Variable requerida no resuelta por el renderer | Se propaga `VariablePlantillaFaltanteException` sin registrar. |
| Registro nuevo | `Creada` y el ID devuelto por persistencia. |
| Mismo `IdEvento` ya registrado | `YaRegistrada` y el ID existente, sin modificar la instantánea persistida. |

Cada instrucción se resuelve por separado; su `TipoEnvio` se conserva. Los IDs numéricos del catálogo de tipos no se confunden con los valores del enum. Se soportan varios usuarios por rol y el contrato del resolvedor permite omitir usuarios sin correo.

Las direcciones se validan mediante `NotificacionDestinatario.Crear`. Se descarta exclusivamente la validación `NOTIFICACION_DESTINATARIO_CORREO_INVALIDO`; otros errores, como un nombre demasiado largo, se propagan. No se replica el algoritmo de validación del dominio. Después de normalizar, se eliminan duplicados de dirección y participación ignorando mayúsculas. Una dirección en TO y CC conserva ambas participaciones, según el dominio actual; no se introdujo una precedencia entre TO, CC y BCC.

El emisor suministra el GUID de la ocurrencia y debe reutilizarlo al reentregarla. Application no genera otro GUID ni implementa una caché, consulta previa o mecanismo adicional de idempotencia. La unicidad y las carreras corresponden a `RegistrarSiNoExisteAsync`.

El contrato actual detecta duplicados al registrar, después de resolver y renderizar. Por tanto, una reentrega vuelve a evaluar la configuración vigente y puede omitirse o fallar si esta cambió. Eso no modifica una notificación existente. No se añadió una consulta por evento para alterar este contrato.

## 4. Contratos externos y contenido seguro

- `IDestinatarioResolver.ResolverAsync`: transforma una instrucción ROL o USUARIO en direcciones concretas. No se incorporan estrategias por proyecto ni comprador.
- `IPlantillaRenderer.RenderizarAsync`: recibe el evento, la entidad `PlantillaCorreo` y un diccionario de valores sin codificar. Devuelve asunto y HTML definitivos. El emisor formatea fechas y montos con la cultura deseada; las claves son sensibles a mayúsculas.
- `IEmailSender.EnviarAsync`: recibe asunto, HTML y listas TO/CC/BCC. Una tarea completada satisfactoriamente significa que el proveedor confirmó aceptación; no acredita entrega final al buzón.

El contrato del renderer exige `VariablePlantillaFaltanteException` para variables requeridas ausentes o nulas, codificación según contexto HTML, atributos entre comillas, validación de enlaces con esquemas http/https y rechazo de contextos no soportados. No admite HTML crudo en variables ni saltos CR/LF en el asunto. La codificación HTML por sí sola no impide un `href` con esquema `javascript:`. Estas obligaciones deberán implementarse y probarse en Infrastructure: las pruebas de Application solo comprueban delegación y propagación de errores.

La configuración SMTP, remitente `notificaciones@vittoriainmobiliaria.com`, credenciales y librerías corresponden al futuro adaptador. No hay secretos ni configuración de transporte en Application.

## 5. Pendientes, errores y reintentos

El handler reclama un lote mediante `ReclamarPendientesAsync`. El repositorio ya cambia atómicamente a `PROCESANDO`, registra el inicio e incrementa `Intentos`; Application no llama nuevamente a `IniciarProcesamiento`.

El mensaje se construye exclusivamente con asunto, HTML y destinatarios almacenados. No vuelve a consultar reglas, usuarios ni plantillas. Después de esperar la aceptación del proveedor, se ejecuta `MarcarComoEnviada`. Solo se capturan excepciones `EnvioCorreoException` lanzadas por el transporte; las transiciones y el guardado están fuera de ese bloque.

| Situación | Política |
| --- | --- |
| `EsTransitorio == true` | `RegistrarFallo`: pendiente con espera mientras queden intentos; error al agotar el máximo. |
| `EsTransitorio == null` | La misma política acotada de reintento. Un fallo desconocido no se considera automáticamente permanente. |
| `EsTransitorio == false` | `RegistrarFallo(..., permitirReintento: false)`: error definitivo desde el intento actual. |
| Fallo individual de transporte manejado | Guarda el resultado y continúa con el resto del lote. |
| Error de programación, configuración o persistencia | Se propaga; no se convierte en fallo SMTP ni éxito. |
| Cancelación | Se propaga. Los intentos reclamados sin resultado quedan disponibles para recuperación por timeout. |
| `GuardarResultadoAsync == false` | Se informa conflicto en el resultado del lote y en logs; no cuenta como guardado ni se repite el envío inmediatamente. |

El guardado recibe el número de intento leído antes del envío. El repositorio solo debe actualizar una fila todavía en `PROCESANDO` con ese número, sin sobrescribir el contador. Se mantiene así la protección de intentos posteriores.

Los diagnósticos persistidos de transporte son textos controlados por Application. Los logs incluyen ID de notificación, intento y clasificación, sin registrar el mensaje o la causa del proveedor, direcciones, variables ni cuerpo del correo. El error original puede contener secretos y no se vuelca a los logs del handler.

`ResultadoProcesamientoLote.Guardadas` cuenta resultados persistidos, incluidos reintentos o errores definitivos; no es un contador de correos aceptados. `ConflictosConcurrencia` distingue resultados que no se pudieron aplicar.

## 6. Recuperación y ajustes mínimos de Domain

Se añadió `INotificacionRepository.ObtenerProcesamientosVencidosAsync(cantidad, fechaLimiteUtc, cancellationToken)`. Es una lectura acotada de instantáneas en `PROCESANDO` con inicio estrictamente anterior al límite; no reclama de nuevo, no incrementa intentos y no mantiene transacciones o bloqueos abiertos al regresar.

El handler calcula `fechaLimiteUtc = ahoraUtc - timeout`. En el límite exacto todavía no se ha superado el timeout. Por cada intento vencido aplica el método existente `RegistrarFallo` con un diagnóstico de resultado incierto y guarda mediante `GuardarResultadoAsync` con el intento observado. No fue necesaria una nueva transición de recuperación en la entidad. Si quedan intentos, el próximo envío se habilita después del intervalo configurado; con intentos agotados termina en `ERROR`.

La comparación de estado e intento evita sobrescribir tanto un envío que se guardó mientras se leía la recuperación como un intento posterior. También protege contra la respuesta tardía de un proveedor después de recuperar el intento: ese resultado ya no encuentra su fila en `PROCESANDO` con el intento correspondiente.

La única ampliación del comportamiento de la entidad fue `RegistrarFallo(..., bool permitirReintento = true)`. El dominio anterior solo terminaba en error por agotamiento; no podía expresar un rechazo permanente en el primer intento. El parámetro permite esa transición sin manipular `Intentos`, inventar un máximo ni reconstruir arbitrariamente el estado. Las llamadas existentes de cuatro argumentos conservan el comportamiento anterior. Se mantienen la exigencia de estado `PROCESANDO` y las validaciones de error, máximo e intervalo antes de mutar la entidad. Se agregaron seis pruebas de dominio.

La recuperación no acredita que el correo falló. El proveedor pudo aceptarlo antes de una interrupción o timeout. Un reintento puede duplicar la entrega; la idempotencia de registro por `IdEvento` y el control de concurrencia en SQL no garantizan entrega única por SMTP.

## 7. Transacciones e integración posterior

El ERP utiliza conexiones locales de `IDbConnectionFactory`; algunas operaciones comparten explícitamente `IDbConnection`/`IDbTransaction`, como Importación. No se encontró una unidad de trabajo común entre módulos que se pudiera reutilizar directamente.

Application no abre, confirma, revierte ni dispone transacciones. Se aclaró el contrato de `RegistrarSiNoExisteAsync`: el futuro adaptador debe poder recibir en su ámbito la conexión/transacción del módulo emisor y conservar su propiedad, registrando cabecera y destinatarios en ella. Sin transacción externa deberá garantizar su propia atomicidad. No se añadieron tipos SQL al comando ni una nueva abstracción de unidad de trabajo.

Esta preparación permite inyectar un repositorio vinculado al contexto transaccional del emisor, pero aún no existe ese adaptador ni la integración con la creación de Requerimientos. La atomicidad de negocio y notificación deberá verificarse con rollback y pruebas contra SQL Server cuando se implemente Infrastructure. La composición futura no debe usar una conexión independiente con commit propio para registrar una notificación de una operación de negocio no confirmada.

La reclamación, recuperación y guardado del procesamiento se ejecutarán en ámbitos sin transacción de negocio abierta. Las operaciones de repositorio deben terminar sus transacciones antes de que el handler comunique con el proveedor.

## 8. Opciones y reloj

| Propiedad | Valor inicial | Validación |
| --- | ---: | --- |
| `TamanoLote` | 10 | Entero positivo. |
| `MaximoIntentos` | 3 | Entero positivo. |
| `IntervaloReintentoMinutos` | 5 | Entero positivo. |
| `TimeoutProcesamientoMinutos` | 10 | Entero positivo. |

Los handlers reciben `IOptions<NotificacionProcesamientoOptions>`, validan sus valores al construirse y copian los parámetros a campos propios. No dependen de mutaciones posteriores de esa instancia de opciones. Las opciones inválidas fallan antes de reclamar o recuperar trabajo. El futuro registro DI podrá complementar esto con validación al iniciar el proceso.

Se inyecta `TimeProvider` de .NET 8; en producción deberá registrarse `TimeProvider.System`. Todas las fechas enviadas a persistencia son UTC. Las pruebas utilizan un reloj controlado, sin esperas reales.

No se añadió la frecuencia de 30 segundos a estas opciones. Su configuración corresponde al futuro proceso alojado. El adaptador SMTP y el worker deberán coordinar sus tiempos máximos con el timeout de procesamiento: el inicio se registra al reclamar el lote, de modo que también cuenta el tiempo de espera de las últimas notificaciones de un lote secuencial.

## 9. Compilación y pruebas ejecutadas

Entorno: SDK .NET `8.0.132`, destino `net8.0` y dependencias previamente restauradas.

| Comprobación | Resultado |
| --- | --- |
| Compilación de API y proyecto de pruebas | Correcta, sin errores. La primera compilación emitió 38 advertencias en archivos ajenos a Notificaciones; la última, al recompilar el proyecto de pruebas, emitió 19 advertencias existentes. No se emitieron advertencias en los nuevos archivos de Notificaciones. |
| Unitarias de Application / ProcesarEvento | 20 aprobadas. |
| Unitarias de Application / ProcesarNotificacionesPendientes | 17 aprobadas. |
| Unitarias de Application / RecuperarNotificacionesInterrumpidas | 14 aprobadas. |
| Unitarias de Application / opciones | 9 aprobadas. |
| Nuevas unitarias de Domain / fallo permanente | 6 aprobadas. |
| Unitarias originales de Domain | 58 aprobadas. |
| Total Notificaciones | **124 aprobadas, 0 fallidas**; 66 casos nuevos. |
| Suite unitaria completa del backend, tras ajustar el setup de CSV | **733 aprobadas, 0 fallidas, 0 omitidas**, incluidas las 124 de Notificaciones. |

La compilación inicial de la solución con ejecución paralela terminó con código 1 sin un diagnóstico de compilación; se ejecutó satisfactoriamente con un solo nodo y los servidores de compilación deshabilitados. La primera ejecución de VSTest fue bloqueada por el sandbox al abrir su socket local. Las ejecuciones posteriores se realizaron con autorización fuera del sandbox y no requirieron SMTP, Docker ni base de datos.

La primera ejecución ampliada de unitarias detectó tres casos CSV que no podían construir datos Windows-1252 sin el arranque incidental de la API. Se corrigió únicamente su preparación de pruebas, se recompiló y la ejecución completa posterior aprobó los 733 casos.

Comandos reproducibles desde la raíz del backend:

```bash
dotnet build backend.sln --no-restore --disable-build-servers -m:1 -v minimal

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-build --no-restore \
  --filter FullyQualifiedName~Cobranzas_Vittoria.Tests.Unit.Notificaciones \
  --logger 'console;verbosity=normal' \
  --logger 'trx;LogFileName=notificaciones-application.trx' \
  --results-directory /tmp/vittoria-notificaciones-application

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-build --no-restore \
  --filter FullyQualifiedName~Cobranzas_Vittoria.Tests.Unit \
  --logger 'console;verbosity=minimal' \
  --logger 'trx;LogFileName=backend-unitarias.trx' \
  --results-directory /tmp/vittoria-notificaciones-application
```

Evidencia temporal: `/tmp/notificaciones-application-build.log`, `/tmp/notificaciones-application-solution-build.log`, `/tmp/notificaciones-application-tests.log`, `/tmp/notificaciones-backend-unitarias.log` y los dos archivos TRX del directorio indicado. Los conteos incluyen variantes `[TestCase]`; no representan porcentajes de cobertura.

## 10. Dependencias pendientes y límites de validación

- Implementar ambos repositorios, la lectura consistente de configuración, la reclamación atómica y las actualizaciones condicionadas por estado/intento. Probar carreras entre workers y recuperadores contra SQL Server.
- Implementar el vínculo del repositorio con la transacción del emisor y probar rollback conjunto, registro de destinatarios e idempotencia bajo concurrencia real.
- Implementar resolución ROL/USUARIO con las relaciones y correos reales del ERP. La semilla inicial usa rol 2 como TO y rol 6 como CC; no se contrastaron usuarios ni asignaciones en una base objetivo.
- Implementar el renderer seguro y probar variables ausentes/nulas, codificación de texto y atributos, rechazo de esquemas de URL peligrosos y encabezados. No se validó seguridad de un renderer concreto porque todavía no existe.
- Implementar SMTP corporativo, configuración segura de credenciales, tiempos de transporte y traducción de errores a `EnvioCorreoException`. La aceptación parcial de destinatarios, si el proveedor la permite, deberá tener una política explícita en el adaptador; no se garantiza entrega única ni entrega al buzón.
- Registrar adaptadores, handlers, opciones y reloj en DI cuando existan las implementaciones necesarias. No se simula un módulo operativo con implementaciones ficticias.
- Implementar posteriormente el worker, su frecuencia de 30 segundos e integración de eventos con Requerimientos. No se añadieron servicios alojados, controladores, frontend, SQL ni nuevas migraciones.
- El middleware HTTP existente no se modificó. Una futura exposición HTTP del módulo deberá definir la traducción de sus excepciones; actualmente la orquestación las propaga.

Las pruebas de concurrencia realizadas usan dobles y tareas controladas: validan la coordinación del handler con el contrato, no las garantías físicas de un repositorio SQL. No se ejecutaron pruebas de integración, migraciones en una base objetivo, envíos reales, pruebas de navegador ni despliegue.
