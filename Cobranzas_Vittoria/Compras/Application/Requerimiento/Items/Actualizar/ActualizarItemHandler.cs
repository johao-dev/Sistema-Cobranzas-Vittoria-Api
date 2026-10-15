namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Actualizar;

// Reglas de negocio pendientes:
// - Verificar que el requerimiento exista y esté en estado EnviadoAlmacen.
// - Verificar que el ítem exista y pertenezca al requerimiento indicado.
// - Rechazar una solicitud PATCH que no incluya ningún campo a modificar.
// - Validar que Cantidad sea mayor que cero cuando se proporcione.
// - Definir si IdMaterial se puede modificar; si se permite, impedir materiales duplicados.
// - Definir si null significa no modificar o limpiar IdPresupuestoDetalle y Observacion.
// - Delegar la actualización parcial al repositorio solo después de superar las validaciones.