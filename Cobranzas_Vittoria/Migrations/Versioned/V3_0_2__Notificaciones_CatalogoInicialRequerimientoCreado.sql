/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V3.0.2
Módulo      : Notificaciones
Descripción : Registro inicial de catálogos, regla y plantilla
              para el evento REQUERIMIENTO_CREADO.
Autor       : Johao Bravo
===============================================================================
*/

-- ============================================================================
-- 1. CODIGOS DE EVENTO
-- ============================================================================
/*
REQUERIMIENTO_CREADO:
Se genera cuando un requerimiento se registra correctamente
en el ERP.
===============================================================================
*/
INSERT INTO Notificaciones.CodigoEventoNotificacion (Codigo)
VALUES ('REQUERIMIENTO_CREADO');
GO


-- ============================================================================
-- 2. TIPOS DE DESTINATARIO
-- ============================================================================
/*
ROL:
Resuelve usuarios asociados a un rol específico.

USUARIO:
Resuelve un usuario específico.

Estos tipos deberán disponer de resolutores en el backend.
===============================================================================
*/
INSERT INTO Notificaciones.TipoDestinatario (Nombre)
VALUES
    ('ROL'),
    ('USUARIO');
GO


-- ============================================================================
-- 3. REGLA DE NOTIFICACION
-- ============================================================================
/*
Habilita el envío automático de notificaciones
cuando se registra un nuevo requerimiento.
===============================================================================
*/
INSERT INTO Notificaciones.ReglaNotificacion
(
    IdCodigoEventoNotificacion,
    Nombre,
    Activo
)
SELECT
    IdCodigoEvento,
    'Notificación de requerimiento creado',
    1
FROM Notificaciones.CodigoEventoNotificacion
WHERE Codigo = 'REQUERIMIENTO_CREADO';
GO


/*
===============================================================================
4. DESTINATARIOS DEL EVENTO REQUERIMIENTO_CREADO
-------------------------------------------------------------------------------
Configura los destinatarios de la notificación cuando se registra
un nuevo requerimiento.

ROL 2 -> Almacenero (TO).
ROL 6 -> Coordinador de proyecto (CC).

Los destinatarios se resuelven utilizando los usuarios que tengan
asignados los roles correspondientes.

Si existen varios usuarios con el mismo rol, todos recibirán la notificación.
===============================================================================
*/
INSERT INTO Notificaciones.ReglaDestinatario
(
    IdReglaNotificacion,
    IdTipoDestinatario,
    Valor,
    TipoEnvio
)
SELECT
    rn.IdReglaNotificacion,
    td.IdTipoDestinatario,
    destinatarios.Valor,
    destinatarios.TipoEnvio
FROM Notificaciones.ReglaNotificacion rn
INNER JOIN Notificaciones.CodigoEventoNotificacion cen
    ON cen.IdCodigoEvento = rn.IdCodigoEventoNotificacion
CROSS JOIN (
    VALUES
        (N'2', N'TO'),
        (N'6', N'CC')
) AS destinatarios(Valor, TipoEnvio)
INNER JOIN Notificaciones.TipoDestinatario td
    ON td.Nombre = N'ROL'
WHERE cen.Codigo = N'REQUERIMIENTO_CREADO';
GO


-- ============================================================================
-- 5. PLANTILLA DE CORREO
-- ============================================================================
/*
Define el asunto y cuerpo HTML del correo.

Las variables se reemplazarán por valores reales antes de registrar
la notificación.
===============================================================================
*/
INSERT INTO Notificaciones.PlantillaCorreo
(
    IdCodigoEventoNotificacion,
    Asunto,
    CuerpoHtml,
    Activo
)
SELECT
    IdCodigoEvento,
    N'Nuevo requerimiento: {{NumeroRequerimiento}}',
    N'
    <!DOCTYPE html>
    <html lang="es">
    <head>
        <meta charset="UTF-8">
    </head>
    <body>
        <h2>Nuevo requerimiento registrado</h2>

        <p>
            Se ha registrado un nuevo requerimiento
            de materiales en el ERP.
        </p>

        <p>
            <strong>Requerimiento:</strong>
            {{NumeroRequerimiento}}
        </p>

        <p>
            <strong>Proyecto:</strong>
            {{Proyecto}}
        </p>

        <p>
            <strong>Solicitante:</strong>
            {{Solicitante}}
        </p>

        <p>
            <strong>Fecha:</strong>
            {{Fecha}}
        </p>

        <p>
            Acceda al sistema para revisar
            el requerimiento:
        </p>

        <p>
            <a href="{{Url}}">
                Ver requerimiento
            </a>
        </p>

        <hr>

        <small>
            Mensaje automático generado por
            el ERP de Vittoria Inmobiliaria.
        </small>
    </body>
    </html>',
    1
FROM Notificaciones.CodigoEventoNotificacion
WHERE Codigo = 'REQUERIMIENTO_CREADO';
GO
