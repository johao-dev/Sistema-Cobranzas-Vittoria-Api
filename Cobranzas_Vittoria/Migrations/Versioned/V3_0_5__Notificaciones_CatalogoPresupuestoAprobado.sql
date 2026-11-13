/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V3.0.5
Módulo      : Notificaciones
Descripción : Configura el evento PRESUPUESTO_APROBADO, su regla y plantilla.
              Cada nueva versión aprobada debe informarse al Coordinador.
              Se utiliza el rol global; aún no existe asignación por proyecto.
===============================================================================
*/

SET XACT_ABORT ON;

-- Resolver por nombre evita depender del ID del rol en cada entorno.
DECLARE @IdRolCoordinador INT = (
    SELECT IdRol FROM seguridad.Rol WHERE Nombre = N'Coordinador'
);
DECLARE @IdTipoDestinatario INT = (
    SELECT IdTipoDestinatario FROM Notificaciones.TipoDestinatario WHERE Nombre = N'ROL'
);

IF @IdRolCoordinador IS NULL
    THROW 50000, N'No se encontró el rol Coordinador para configurar PRESUPUESTO_APROBADO.', 1;

IF @IdTipoDestinatario IS NULL
    THROW 50000, N'No se encontró el tipo de destinatario ROL para configurar PRESUPUESTO_APROBADO.', 1;

BEGIN TRANSACTION;

INSERT INTO Notificaciones.CodigoEventoNotificacion (Codigo)
VALUES (N'PRESUPUESTO_APROBADO');

DECLARE @IdCodigoEvento INT = CONVERT(INT, SCOPE_IDENTITY());

INSERT INTO Notificaciones.ReglaNotificacion
(
    IdCodigoEventoNotificacion,
    Nombre,
    Activo
)
VALUES (@IdCodigoEvento, N'Notificación de nueva versión de presupuesto aprobada', 1);

DECLARE @IdReglaNotificacion INT = CONVERT(INT, SCOPE_IDENTITY());

-- Únicamente Coordinador en TO, sin CC ni BCC.
INSERT INTO Notificaciones.ReglaDestinatario
(
    IdReglaNotificacion,
    IdTipoDestinatario,
    Valor,
    TipoEnvio
)
VALUES (@IdReglaNotificacion, @IdTipoDestinatario, CONVERT(NVARCHAR(150), @IdRolCoordinador), N'TO');

-- Variables que deberá proporcionar el emisor al integrar este evento.
INSERT INTO Notificaciones.PlantillaCorreo
(
    IdCodigoEventoNotificacion,
    Asunto,
    CuerpoHtml,
    Activo
)
VALUES
(
    @IdCodigoEvento,
    N'Presupuesto aprobado: {{CodigoPresupuesto}} - versión {{NumeroVersion}}',
    N'<!DOCTYPE html>
    <html lang="es">
    <head>
        <meta charset="UTF-8">
    </head>
    <body>
        <h2>Nueva versión de presupuesto aprobada</h2>

        <p>Se ha aprobado una nueva versión del presupuesto en el ERP.</p>

        <table>
            <tbody>
                <tr>
                    <td><strong>Presupuesto:</strong></td>
                    <td>{{CodigoPresupuesto}} - {{NombrePresupuesto}}</td>
                </tr>
                <tr>
                    <td><strong>Versión:</strong></td>
                    <td>{{NumeroVersion}}</td>
                </tr>
                <tr>
                    <td><strong>Proyecto:</strong></td>
                    <td>{{Proyecto}}</td>
                </tr>
                <tr>
                    <td><strong>Fecha de aprobación:</strong></td>
                    <td>{{Fecha}}</td>
                </tr>
            </tbody>
        </table>

        <hr>

        <small>Mensaje automático generado por el ERP de Vittoria Inmobiliaria.</small>
    </body>
    </html>',
    1
);

COMMIT TRANSACTION;
GO
