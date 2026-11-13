/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V3.0.6
Módulo      : Notificaciones
Descripción : Agrega el motivo de cambio a la plantilla de PRESUPUESTO_APROBADO.
===============================================================================
*/

SET XACT_ABORT ON;

UPDATE pc
SET CuerpoHtml = N'<!DOCTYPE html>
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
                <tr>
                    <td><strong>Motivo de cambio:</strong></td>
                    <td>{{MotivoCambio}}</td>
                </tr>
            </tbody>
        </table>

        <hr>

        <small>Mensaje automático generado por el ERP de Vittoria Inmobiliaria.</small>
    </body>
    </html>'
FROM Notificaciones.PlantillaCorreo pc
INNER JOIN Notificaciones.CodigoEventoNotificacion cen
    ON cen.IdCodigoEvento = pc.IdCodigoEventoNotificacion
WHERE cen.Codigo = N'PRESUPUESTO_APROBADO';

IF @@ROWCOUNT = 0
    THROW 50000, N'No se encontró la plantilla de PRESUPUESTO_APROBADO para agregar el motivo de cambio.', 1;
GO
