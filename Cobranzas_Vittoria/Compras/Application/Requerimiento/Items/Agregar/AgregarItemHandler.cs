namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Agregar;

// Reglas de negocio pendientes:
// - Verificar que el requerimiento exista y esté en estado EnviadoAlmacen.
// - Validar que IdMaterial sea válido y que Cantidad sea mayor que cero.
// - Validar IdPresupuestoDetalle cuando se proporcione.
// - Impedir que el material ya exista entre los ítems del requerimiento.
// - Delegar la inserción al repositorio solo después de superar las validaciones.