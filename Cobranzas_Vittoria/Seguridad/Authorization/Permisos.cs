namespace Cobranzas_Vittoria.Seguridad.Authorization;

// Sigue la convención: recurso.acción
public static class Permisos
{
    public static class Requerimientos
    {
        public const string Ver = "requerimientos.ver";
        public const string Crear = "requerimientos.crear";
        public const string EditarBorrador = "requerimientos.editar_borrador";
        public const string Enviar = "requerimientos.enviar";
        public const string ProcesarStock = "requerimientos.procesar_stock";
        public const string Aprobar = "requerimientos.aprobar";
        public const string Rechazar = "requerimientos.rechazar";
        public const string EnviarCompras = "requerimientos.enviar_compras";
        public const string VerDepuracionAlmacen = "requerimientos.ver_depuracion_almacen";
    }
}
