/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V3.0.3
Módulo      : Notificaciones
Descripción : Agrega el rol Comprador (ID 7) como destinatario principal
              del evento REQUERIMIENTO_CREADO.
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
    N'7',
    N'TO'
FROM Notificaciones.ReglaNotificacion rn
INNER JOIN Notificaciones.CodigoEventoNotificacion cen
    ON cen.IdCodigoEvento = rn.IdCodigoEventoNotificacion
INNER JOIN Notificaciones.TipoDestinatario td
    ON td.Nombre = N'ROL'
WHERE cen.Codigo = N'REQUERIMIENTO_CREADO'
  AND NOT EXISTS (
      SELECT 1
      FROM Notificaciones.ReglaDestinatario rd
      WHERE rd.IdReglaNotificacion = rn.IdReglaNotificacion
        AND rd.IdTipoDestinatario = td.IdTipoDestinatario
        AND rd.Valor = N'7'
        AND rd.TipoEnvio = N'TO'
  );
GO
