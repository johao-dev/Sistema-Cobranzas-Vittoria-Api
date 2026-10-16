namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Habilitar;

/// <summary> Handler para habilitar un permiso en el sistema de seguridad. </summary>
public class EnablePermisoHandler
{
    /*
        - Validar el identificador y obtener el permiso, incluyendo inactivos.
        - Si no existe, propagar el error de negocio siguiendo el contrato actual del módulo.
        - Si ya tiene el estado solicitado, terminar sin persistir ni modificar auditoría.
        - Ejecutar permiso.Activar() o permiso.Desactivar().
        - Establecer la auditoría con IUsuarioActualService.
        - Persistir el estado y registrar el resultado.
    */
}