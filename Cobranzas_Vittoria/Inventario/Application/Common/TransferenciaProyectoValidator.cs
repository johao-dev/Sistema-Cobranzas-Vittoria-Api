using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Inventario.Application.Excepciones;
using Cobranzas_Vittoria.Inventario.Domain.Model;

namespace Cobranzas_Vittoria.Inventario.Application.Common;

public sealed class TransferenciaProyectoValidator
{
    public void Validar(RegistroTransferenciaProyecto registro)
    {
        ArgumentNullException.ThrowIfNull(registro);
        List<DetalleErrorValidacion> errores = new();

        if (registro.IdProyectoOrigen <= 0 || registro.IdProyectoDestino <= 0)
        {
            errores.Add(new(null, "idProyecto", CodigosErrorInventario.Validacion.CampoRequerido, "Los proyectos deben ser mayores a cero."));
        }

        if (registro.IdProyectoOrigen == registro.IdProyectoDestino)
        {
            errores.Add(new(null, "idProyectoDestino", CodigosErrorInventario.Validacion.ProyectosIguales, "El proyecto origen y destino deben ser distintos."));
        }

        if (registro.Fecha == default || registro.Observacion is null || registro.Observacion.Length > 500)
        {
            errores.Add(new(null, "fecha/observacion", CodigosErrorInventario.Validacion.CampoRequerido, "Indique fecha y observación de hasta 500 caracteres."));
        }

        if (registro.Items is null || registro.Items.Count == 0)
        {
            errores.Add(new(null, "items", CodigosErrorInventario.Validacion.CampoRequerido, "Se requiere al menos un material."));
        }
        else if (registro.Items.Any(item => item is null || item.IdMaterial <= 0 || item.Cantidad <= 0 ||
                     item.Cantidad > 9999999999999999.99m || decimal.Round(item.Cantidad, 2) != item.Cantidad))
        {
            errores.Add(new(null, "items", CodigosErrorInventario.Validacion.CantidadInvalida, "Indique material y cantidad positiva con hasta dos decimales."));
        }

        if (errores.Count > 0)
        {
            throw new ValidacionNegocioInventarioException(errores);
        }
    }
}
