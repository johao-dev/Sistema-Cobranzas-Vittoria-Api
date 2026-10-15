namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Eliminar;

// Reglas de negocio pendientes:
// - Verificar que el requerimiento exista y esté en estado EnviadoAlmacen.
// - Verificar que el ítem exista y pertenezca al requerimiento indicado.
// - Impedir la eliminación si dejaría el requerimiento sin ítems.
// - Delegar la eliminación al repositorio solo después de superar las validaciones.