namespace Cobranzas_Vittoria.Application.Inventario.Dtos;

/// <summary>
/// DTO de entrada para crear o actualizar una entrada manual de Kardex.
/// Mapea 1-a-1 con los parametros del SP <c>almacen.usp_KardexEntrada_Registrar</c>
/// y <c>almacen.usp_KardexEntrada_Actualizar</c>.
///
/// <para>
/// <b>IdKardexEntrada</b>: es <c>int?</c> y se ignora en el registro
/// (lo genera la identidad). En la actualizacion el controller valida
/// que <c>idRuta == IdKardexEntrada</c>; si no coinciden, lanza
/// <c>IdRutaInconsistenteException</c> ANTES de llegar al service.
/// </para>
///
/// <para>
/// <b>Cantidad</b>: el SP exige <c>&gt;= 0</c>. Una entrada con cantidad
/// 0 es valida (caso de ajuste / anulacion) y no genera error de negocio.
/// </para>
///
/// <para>
/// <b>Por que es una clase mutable y no un record</b>:
/// el binding de ASP.NET Core requiere propiedades con <c>set</c> publico
/// (los <c>init</c> de los records funcionan, pero las clases con <c>set</c>
/// son el patron establecido en los DTOs legacy del proyecto y son mas
/// flexibles para partial-binding cuando hay campos opcionales en JSON).
/// </para>
/// </summary>
public sealed class KardexEntradaCreateDto
{
    /// <summary>PK. Null en POST (lo asigna la BD); obligatorio en PUT (validado contra la ruta).</summary>
    public int? IdKardexEntrada { get; set; }

    /// <summary>FK a maestra.Especialidad (REQUERIDO).</summary>
    public int IdEspecialidad { get; set; }

    /// <summary>FK a maestra.Material (REQUERIDO).</summary>
    public int IdMaterial { get; set; }

    /// <summary>FK a maestra.Proveedor (OPCIONAL).</summary>
    public int? IdProveedor { get; set; }

    /// <summary>FK a maestra.Proyecto (OPCIONAL). Deriva del front aunque el payload original no lo exija.</summary>
    public int? IdProyecto { get; set; }

    /// <summary>Numero de documento soporte, ej: "F001-12345" (OPCIONAL, max 50 chars).</summary>
    public string? NumeroDocumento { get; set; }

    /// <summary>Fecha del movimiento (REQUERIDO).</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Cantidad ingresada (REQUERIDO, &gt;= 0).</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Observacion libre (OPCIONAL, max 250 chars).</summary>
    public string? Observacion { get; set; }
}
