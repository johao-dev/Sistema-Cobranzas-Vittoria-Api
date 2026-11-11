# Ejecución de la suite completa

Fecha: 11 de noviembre de 2026, zona America/Lima.

Se ejecutó toda la suite dos veces consecutivas, sin filtros. **Ambas ejecuciones aprobaron los 1.568 casos, sin fallos, omisiones ni errores del runner registrados en los TRX.** Cada ejecución duró aproximadamente dos minutos y creó sus propios contenedores de prueba.

| Ejecución | Total | Aprobados | Fallidos | Omitidos |
| --- | ---: | ---: | ---: | ---: |
| Completa, con compilación | 1.568 | 1.568 | 0 | 0 |
| Repetición completa | 1.568 | 1.568 | 0 | 0 |

Entorno comprobado: SDK .NET 8.0.132 y Docker Server 29.9.3. Se utilizaron las dependencias restauradas existentes. Los resultados corresponden al código actual del workspace, incluida la integración de eventos Compras/Notificaciones.

## Errores de Docker y los 16 fallos informados

En la comprobación inicial, acceder a `/var/run/docker.sock` desde el sandbox produjo `permission denied`. Se verificó Docker y se ejecutaron ambas suites fuera del sandbox con autorización. Ese bloqueo local del agente no demuestra cuál fue la causa de los errores de las ejecuciones anteriores del usuario.

Se comprobó en el código y en el TRX que `NotificacionesPersistenceTests(False)` contiene exactamente 16 casos, y `NotificacionesPersistenceTests(True)` otros 16. Cada variante inicia un contenedor SQL en su `OneTimeSetUp` compartido, en `Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/NotificacionesSql/NotificacionesPersistenceTests.cs`.

**Hipótesis pendiente de confirmar:** un único fallo de Docker durante ese arranque puede hacer fallar los 16 casos dependientes. El número de fallos informado es compatible con esta estructura; no prueba que ésa fuera la causa. Se necesita el mensaje y stack del arranque fallido para confirmarlo.

No se reprodujeron los dos errores de la tercera ejecución informada. Las dos ejecuciones nuevas no aportan un stack de fallo porque todos sus casos terminaron correctamente. Tampoco certifican la ausencia de fallos intermitentes futuros.

El log detallado sí incluye excepciones SQL emitidas por pruebas que provocan errores deliberadamente. Esos mensajes no equivalen por sí mismos a pruebas fallidas: se contrastaron con el resultado individual y los contadores finales del TRX.

Los TRX también contienen entradas `RunInfo` de tipo `Warning` con salida capturada de los fixtures y migraciones. No hay entradas `RunInfo` de tipo `Error` o `Failed`. El resumen JSON conserva metadatos de esas entradas; su contenido completo permanece en los TRX para evitar duplicar logs extensos en el repositorio.

## Evidencia

- [Primera ejecución: TRX](/tmp/vittoria-suite-completa/suite-completa.trx).
- [Primera ejecución: log detallado](/tmp/vittoria-suite-completa.log).
- [Segunda ejecución: TRX](/tmp/vittoria-suite-completa/suite-completa-repeticion.trx).
- [Segunda ejecución: log detallado](/tmp/vittoria-suite-completa-repeticion.log).
- [Resumen extraído de ambos TRX](evidencias/suite-completa-validacion.json).

Los archivos completos de `/tmp` son evidencia local temporal. Se conserva en el repositorio únicamente este reporte y el resumen JSON de las ejecuciones; esta revisión no cambió el código de producción ni las pruebas.

## Comandos utilizados

Desde la raíz del backend:

```bash
dotnet --version
docker info --format '{{.ServerVersion}}'

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-restore \
  --logger 'console;verbosity=detailed' \
  --logger 'trx;LogFileName=suite-completa.trx' \
  --results-directory /tmp/vittoria-suite-completa \
  > /tmp/vittoria-suite-completa.log 2>&1

dotnet test Cobranzas_Vittoria/Cobranzas_Vittoria.Tests/Cobranzas_Vittoria.Tests.csproj \
  --no-build --no-restore \
  --logger 'console;verbosity=detailed' \
  --logger 'trx;LogFileName=suite-completa-repeticion.trx' \
  --results-directory /tmp/vittoria-suite-completa \
  > /tmp/vittoria-suite-completa-repeticion.log 2>&1
```

Para investigar una próxima ejecución fallida, conservar su TRX y el log completo antes de repetirla. `console;verbosity=detailed` y el TRX permiten recuperar el mensaje y stack que un resumen final puede omitir. Si el problema ocurre en `OneTimeSetUp` o `OneTimeTearDown`, revisar también las entradas del runner y la excepción original de Docker, además de los casos marcados como fallidos.

## Actualización posterior del diagnóstico

Tras recibir los nombres de las dos pruebas fallidas, se reprodujeron y corrigieron los interbloqueos SQL de Gastos Directos. También se reprodujeron cierres del motor SQL en el arranque de contenedores. La validación final del código corregido pasó los 1.570 casos, incluidos dos nuevos casos de regresión. Los resultados anteriores de este reporte corresponden a las ejecuciones previas y se conservan como evidencia histórica. Véase el [reporte de corrección y diagnóstico completo](reporte-correccion-concurrencia-gastos-directos.md).
