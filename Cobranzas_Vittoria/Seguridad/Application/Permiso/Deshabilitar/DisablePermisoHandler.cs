namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Deshabilitar;

/// <summary> Handler para deshabilitar un permiso en el sistema de seguridad. </summary>
public class DisablePermisoHandler
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