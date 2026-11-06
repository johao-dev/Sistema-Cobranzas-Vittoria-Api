/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V3.0.1
Módulo      : Notificaciones
Descripción : Creación de tablas, relaciones, restricciones e índices para el
              módulo de Notificaciones del ERP.
Autor       : Johao Bravo
===============================================================================
*/

-- ============================================================================
-- 1. CATALOGO DE EVENTOS DE NOTIFICACION
-- ============================================================================
/*
Define los eventos de negocio que pueden generar notificaciones automáticas.

Los códigos son identificadores funcionales reconocidos por el backend.

Ejemplos:
REQUERIMIENTO_CREADO   -> Se registra un nuevo requerimiento.
REQUERIMIENTO_APROBADO -> Se aprueba un requerimiento.
PRESUPUESTO_APROBADO   -> Se aprueba una versión de presupuesto.

Cada evento puede tener una regla de notificación y una plantilla de correo.
===============================================================================
*/
CREATE TABLE Notificaciones.CodigoEventoNotificacion (
    IdCodigoEvento INT IDENTITY(1,1) NOT NULL,
    Codigo NVARCHAR(100) NOT NULL,

    CONSTRAINT PK_CodigoEventoNotificacion
        PRIMARY KEY (IdCodigoEvento),

    CONSTRAINT UQ_CodigoEventoNotificacion_Codigo
        UNIQUE (Codigo)
);
GO


-- ============================================================================
-- 2. REGLAS DE NOTIFICACION
-- ============================================================================
/*
Define qué eventos de negocio tienen habilitado el envío de notificaciones.

Activo:
1 -> La regla está habilitada.
0 -> La regla está deshabilitada.

Se permite una única regla por evento de negocio.

Desactivar una regla impide generar nuevas notificaciones para ese evento,
pero no cancela las notificaciones que ya fueron registradas.
===============================================================================
*/
CREATE TABLE Notificaciones.ReglaNotificacion (
    IdReglaNotificacion INT IDENTITY(1,1) NOT NULL,
    IdCodigoEventoNotificacion INT NOT NULL,
    Nombre NVARCHAR(150) NOT NULL,
    Activo BIT NOT NULL
        CONSTRAINT DF_ReglaNotificacion_Activo DEFAULT (1),

    CONSTRAINT PK_ReglaNotificacion
        PRIMARY KEY (IdReglaNotificacion),

    CONSTRAINT FK_ReglaNotificacion_CodigoEvento
        FOREIGN KEY (IdCodigoEventoNotificacion)
        REFERENCES Notificaciones.CodigoEventoNotificacion (IdCodigoEvento),

    CONSTRAINT UQ_ReglaNotificacion_CodigoEvento
        UNIQUE (IdCodigoEventoNotificacion)
);
GO


-- ============================================================================
-- 3. TIPOS DE DESTINATARIO
-- ============================================================================
/*
Catálogo que define las estrategias utilizadas para resolver los
destinatarios de una notificación.

Valores iniciales sugeridos:

ROL                    -> Usuarios asociados a un rol.
USUARIO                -> Usuario específico.
SOLICITANTE            -> Usuario que originó el proceso.
RESPONSABLE_PROYECTO   -> Responsable del proyecto.
COORDINADOR_PROYECTO   -> Coordinador asignado al proyecto.
COMPRADOR_ASIGNADO     -> Comprador responsable del proceso.
PROVEEDOR              -> Proveedor asociado al documento.

El backend interpreta cada tipo y resuelve sus direcciones de correo.
===============================================================================
*/
CREATE TABLE Notificaciones.TipoDestinatario (
    IdTipoDestinatario INT IDENTITY(1,1) NOT NULL,
    Nombre NVARCHAR(50) NOT NULL,

    CONSTRAINT PK_TipoDestinatario
        PRIMARY KEY (IdTipoDestinatario),

    CONSTRAINT UQ_TipoDestinatario_Nombre
        UNIQUE (Nombre)
);
GO


-- ============================================================================
-- 4. DESTINATARIOS POR REGLA
-- ============================================================================
/*
Configura los destinatarios que deben recibir una notificación cuando
se produce el evento asociado a una regla.

TipoEnvio:
TO  -> Destinatario principal.
CC  -> Destinatario en copia.
BCC -> Destinatario en copia oculta.

Valor:
Contiene el identificador requerido para resolver un destinatario fijo.

Ejemplos:
ROL     -> Identificador del rol.
USUARIO -> Identificador del usuario.

Para destinatarios dinámicos, como SOLICITANTE o COMPRADOR_ASIGNADO,
Valor puede ser NULL.

Los destinatarios se resuelven al generar cada notificación.
===============================================================================
*/
CREATE TABLE Notificaciones.ReglaDestinatario (
    IdReglaDestinatario INT IDENTITY(1,1) NOT NULL,
    IdReglaNotificacion INT NOT NULL,
    IdTipoDestinatario INT NOT NULL,
    Valor NVARCHAR(150) NULL,
    TipoEnvio NVARCHAR(3) NOT NULL,

    CONSTRAINT PK_ReglaDestinatario
        PRIMARY KEY (IdReglaDestinatario),

    CONSTRAINT FK_ReglaDestinatario_ReglaNotificacion
        FOREIGN KEY (IdReglaNotificacion)
        REFERENCES Notificaciones.ReglaNotificacion (IdReglaNotificacion),

    CONSTRAINT FK_ReglaDestinatario_TipoDestinatario
        FOREIGN KEY (IdTipoDestinatario)
        REFERENCES Notificaciones.TipoDestinatario (IdTipoDestinatario),

    CONSTRAINT CK_ReglaDestinatario_TipoEnvio
        CHECK (TipoEnvio IN ('TO', 'CC', 'BCC'))
);
GO

CREATE INDEX IX_ReglaDestinatario_IdReglaNotificacion
    ON Notificaciones.ReglaDestinatario (IdReglaNotificacion);
GO


-- ============================================================================
-- 5. PLANTILLAS DE CORREO
-- ============================================================================
/*
Almacena las plantillas utilizadas para construir el contenido de los
correos electrónicos generados por el sistema.

Asunto     -> Asunto del correo con variables dinámicas.
CuerpoHtml -> Contenido HTML del mensaje.

Activo:
1 -> Plantilla habilitada.
0 -> Plantilla deshabilitada.

Se permite una única plantilla por evento de negocio.

Ejemplos de variables:
{{NumeroRequerimiento}}
{{Proyecto}}
{{Solicitante}}
{{Fecha}}
{{Url}}

Las variables son reemplazadas por el backend antes de registrar
la notificación, conservando el contenido generado.
===============================================================================
*/
CREATE TABLE Notificaciones.PlantillaCorreo (
    IdPlantilla INT IDENTITY(1,1) NOT NULL,
    IdCodigoEventoNotificacion INT NOT NULL,
    Asunto NVARCHAR(250) NOT NULL,
    CuerpoHtml NVARCHAR(MAX) NOT NULL,
    Activo BIT NOT NULL
        CONSTRAINT DF_PlantillaCorreo_Activo DEFAULT (1),

    CONSTRAINT PK_PlantillaCorreo
        PRIMARY KEY (IdPlantilla),

    CONSTRAINT FK_PlantillaCorreo_CodigoEvento
        FOREIGN KEY (IdCodigoEventoNotificacion)
        REFERENCES Notificaciones.CodigoEventoNotificacion (IdCodigoEvento),

    CONSTRAINT UQ_PlantillaCorreo_CodigoEvento
        UNIQUE (IdCodigoEventoNotificacion)
);
GO


-- ============================================================================
-- 6. NOTIFICACIONES GENERADAS
-- ============================================================================
/*
Registra las notificaciones generadas como consecuencia de eventos
de negocio y controla su procesamiento asíncrono.

IDENTIFICACIÓN:
IdEvento     -> Identificador único de la ocurrencia del evento.
IdReferencia -> Identificador del registro de negocio relacionado.

El IdEvento permite evitar registros duplicados cuando se intenta
procesar nuevamente una misma ocurrencia de negocio.

CONTENIDO:
AsuntoGenerado     -> Asunto definitivo del correo.
CuerpoHtmlGenerado -> Contenido HTML definitivo del correo.

Se conserva el contenido renderizado para que los cambios posteriores
en las plantillas no alteren las notificaciones ya generadas.

ESTADOS:
PENDIENTE  -> Esperando procesamiento o próximo reintento.
PROCESANDO -> El worker está procesando la notificación.
ENVIADA    -> El proveedor de correo aceptó el mensaje.
ERROR      -> Fallo definitivo o intentos agotados.

FECHAS:
FechaCreacion            -> Registro de la notificación.
FechaInicioProcesamiento -> Inicio del intento de procesamiento actual.
FechaProximoIntento      -> Fecha habilitada para reintentar el envío.
FechaEnvio               -> Fecha de envío satisfactorio.

CONTROL DE REINTENTOS:
Intentos    -> Cantidad de intentos de procesamiento iniciados.
UltimoError -> Descripción del último error detectado.

Política inicial sugerida:
- Máximo 3 intentos.
- Intervalo de 5 minutos entre intentos.
- Recuperación de procesamientos atascados mediante timeout.

Todas las fechas se almacenan en UTC.

La unicidad de IdEvento evita duplicar registros, pero no garantiza
que un proveedor de correo nunca reciba dos intentos de envío.
===============================================================================
*/
CREATE TABLE Notificaciones.Notificacion (
    IdNotificacion BIGINT IDENTITY(1,1) NOT NULL,

    -- Identificación e idempotencia
    IdEvento UNIQUEIDENTIFIER NOT NULL,
    IdCodigoEventoNotificacion INT NOT NULL,
    IdReferencia INT NOT NULL,

    -- Contenido generado
    AsuntoGenerado NVARCHAR(250) NOT NULL,
    CuerpoHtmlGenerado NVARCHAR(MAX) NOT NULL,

    -- Estado de procesamiento
    Estado NVARCHAR(15) NOT NULL
        CONSTRAINT DF_Notificacion_Estado DEFAULT ('PENDIENTE'),

    -- Fechas del ciclo de vida
    FechaCreacion DATETIME2(0) NOT NULL
        CONSTRAINT DF_Notificacion_FechaCreacion
        DEFAULT (SYSUTCDATETIME()),

    FechaInicioProcesamiento DATETIME2(0) NULL,
    FechaProximoIntento DATETIME2(0) NULL,
    FechaEnvio DATETIME2(0) NULL,

    -- Control de reintentos
    Intentos INT NOT NULL
        CONSTRAINT DF_Notificacion_Intentos DEFAULT (0),

    UltimoError NVARCHAR(MAX) NULL,

    CONSTRAINT PK_Notificacion
        PRIMARY KEY (IdNotificacion),

    CONSTRAINT UQ_Notificacion_IdEvento
        UNIQUE (IdEvento),

    CONSTRAINT FK_Notificacion_CodigoEvento
        FOREIGN KEY (IdCodigoEventoNotificacion)
        REFERENCES Notificaciones.CodigoEventoNotificacion (IdCodigoEvento),

    CONSTRAINT CK_Notificacion_Estado
        CHECK (Estado IN (
            'PENDIENTE',
            'PROCESANDO',
            'ENVIADA',
            'ERROR'
        )),

    CONSTRAINT CK_Notificacion_Intentos
        CHECK (Intentos >= 0)
);
GO

-- Consulta de notificaciones pendientes y reintentos programados.
CREATE INDEX IX_Notificacion_Estado_FechaProximoIntento
    ON Notificaciones.Notificacion (Estado, FechaProximoIntento);
GO

-- Recuperación de notificaciones cuyo procesamiento quedó interrumpido.
CREATE INDEX IX_Notificacion_Estado_FechaInicioProcesamiento
    ON Notificaciones.Notificacion (Estado, FechaInicioProcesamiento);
GO

-- Consulta de notificaciones asociadas a registros de negocio.
CREATE INDEX IX_Notificacion_CodigoEvento_Referencia
    ON Notificaciones.Notificacion (
        IdCodigoEventoNotificacion,
        IdReferencia
    );
GO


-- ============================================================================
-- 7. DESTINATARIOS DE NOTIFICACIONES
-- ============================================================================
/*
Almacena los destinatarios concretos que fueron resueltos al generar
cada notificación.

Correo    -> Dirección de correo del destinatario.
Nombre    -> Nombre del destinatario al momento de la generación.
TipoEnvio -> Tipo de participación en el correo.

TipoEnvio:
TO  -> Destinatario principal.
CC  -> Destinatario en copia.
BCC -> Destinatario en copia oculta.

Los destinatarios se conservan independientemente de los cambios
posteriores en roles, usuarios, responsables o reglas de notificación.

Esto permite mantener trazabilidad sobre los destinatarios originales.
===============================================================================
*/
CREATE TABLE Notificaciones.NotificacionDestinatario (
    IdNotificacionDestinatario BIGINT IDENTITY(1,1) NOT NULL,
    IdNotificacion BIGINT NOT NULL,
    Correo NVARCHAR(254) NOT NULL,
    Nombre NVARCHAR(150) NULL,
    TipoEnvio NVARCHAR(3) NOT NULL,

    CONSTRAINT PK_NotificacionDestinatario
        PRIMARY KEY (IdNotificacionDestinatario),

    CONSTRAINT FK_NotificacionDestinatario_Notificacion
        FOREIGN KEY (IdNotificacion)
        REFERENCES Notificaciones.Notificacion (IdNotificacion),

    CONSTRAINT CK_NotificacionDestinatario_TipoEnvio
        CHECK (TipoEnvio IN ('TO', 'CC', 'BCC'))
);
GO

CREATE INDEX IX_NotificacionDestinatario_IdNotificacion
    ON Notificaciones.NotificacionDestinatario (IdNotificacion);
GO
