/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V3.0.4
Módulo      : Notificaciones
Descripción : Corrige el tipo de envío del rol Comprador (ID 7) a CC
              para el evento REQUERIMIENTO_CREADO.
===============================================================================
*/

UPDATE rd
SET TipoEnvio = N'CC'
FROM Notificaciones.ReglaDestinatario rd
INNER JOIN Notificaciones.ReglaNotificacion rn
    ON rn.IdReglaNotificacion = rd.IdReglaNotificacion
INNER JOIN Notificaciones.CodigoEventoNotificacion cen
    ON cen.IdCodigoEvento = rn.IdCodigoEventoNotificacion
INNER JOIN Notificaciones.TipoDestinatario td
    ON td.IdTipoDestinatario = rd.IdTipoDestinatario
WHERE cen.Codigo = N'REQUERIMIENTO_CREADO'
  AND td.Nombre = N'ROL'
  AND rd.Valor = N'7'
  AND rd.TipoEnvio = N'TO';
GO
