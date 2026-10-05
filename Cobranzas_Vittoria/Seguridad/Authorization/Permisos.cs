namespace Cobranzas_Vittoria.Seguridad.Authorization;

// Sigue la convención: recurso.acción
public static class Permisos
{
    public static class Requerimientos
    {
        public const string Ver = "requerimientos.ver";
        public const string Crear = "requerimientos.crear";
        public const string EditarBorrador = "requerimientos.editar_borrador";
        public const string EditarCantidadesAlmacen = "requerimientos.editar_cantidades_almacen";
        public const string Enviar = "requerimientos.enviar";
        public const string ProcesarStock = "requerimientos.procesar_stock";
        public const string Aprobar = "requerimientos.aprobar";
        public const string Rechazar = "requerimientos.rechazar";
        public const string EnviarCompras = "requerimientos.enviar_compras";
        public const string VerDepuracionAlmacen = "requerimientos.ver_depuracion_almacen";
    }

    public static class GastoDirecto
    {
        public const string Ver = "gasto_directo.ver";

        // Operar es registrar, editar, confirmar, anular y adjuntar documentos.
        public const string Operar = "gasto_directo.operar";
    }

    // Permisos por capacidad de negocio del contrato de API de Control Presupuestario.
    public static class ControlPresupuestario
    {
        public static class CentroCosto
        {
            public const string Ver = "control_presupuestario.centro_costo.ver";
            public const string Crear = "control_presupuestario.centro_costo.crear";
            public const string Actualizar = "control_presupuestario.centro_costo.actualizar";
        }

        public static class Partida
        {
            public const string Ver = "control_presupuestario.partida.ver";
            public const string Crear = "control_presupuestario.partida.crear";
            public const string Actualizar = "control_presupuestario.partida.actualizar";
        }

        public static class Presupuesto
        {
            public const string Ver = "control_presupuestario.presupuesto.ver";
            public const string Crear = "control_presupuestario.presupuesto.crear";
            public const string Actualizar = "control_presupuestario.presupuesto.actualizar";
            public const string EditarDetalle = "control_presupuestario.presupuesto.editar_detalle";

            // Ajuste manual del ledger: acción de negocio acordada fuera del contrato base (D2).
            public const string RegistrarAjuste = "control_presupuestario.presupuesto.registrar_ajuste";
        }

        public static class Version
        {
            public const string Crear = "control_presupuestario.version.crear";
            public const string Aprobar = "control_presupuestario.version.aprobar";
            public const string Anular = "control_presupuestario.version.anular";
        }

        public static class Movimiento
        {
            public const string Ver = "control_presupuestario.movimiento.ver";
        }

        public static class Reporte
        {
            public const string Ver = "control_presupuestario.reporte.ver";
        }
    }
}
