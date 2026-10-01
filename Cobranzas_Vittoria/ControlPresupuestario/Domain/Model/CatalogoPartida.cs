using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>
/// Partida del catálogo reusable, con jerarquía padre/hija. Solo las partidas sin hijas
/// reciben montos y pueden pertenecer a una sección de gasto directo.
/// </summary>
public sealed class CatalogoPartida
{
    public int IdCatalogoPartida { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public int? IdPartidaPadre { get; private set; }
    public string? CodigoPartidaPadre { get; private set; }
    public string? NombrePartidaPadre { get; private set; }
    public int Nivel { get; private set; }
    public int IdTipoPartida { get; private set; }
    public string? CodigoTipoPartida { get; private set; }
    public string? NombreTipoPartida { get; private set; }
    public bool Activo { get; private set; }
    public bool EsHoja { get; private set; }
    public int? IdSeccionGasto { get; private set; }
    public string? CodigoSeccionGasto { get; private set; }
    public string? NombreSeccionGasto { get; private set; }
    public DateTime? FechaCreacion { get; private set; }
    public DateTime? FechaActualizacion { get; private set; }

    private CatalogoPartida() { }

    public static CatalogoPartida Crear(string codigo, string nombre, int idTipoPartida, int? idPartidaPadre,
        string? descripcion, int? idSeccionGasto)
        => new()
        {
            Codigo = Reglas.Requerido(codigo, "Codigo", 50),
            Nombre = Reglas.Requerido(nombre, "Nombre", 200),
            IdTipoPartida = Reglas.Id(idTipoPartida, "IdTipoPartida"),
            IdPartidaPadre = Reglas.IdOpcional(idPartidaPadre, "IdPartidaPadre"),
            Descripcion = Reglas.Opcional(descripcion, "Descripcion", 500),
            IdSeccionGasto = Reglas.IdOpcional(idSeccionGasto, "IdSeccionGasto"),
            Activo = true,
            EsHoja = true
        };

    public static CatalogoPartida Reconstruir(int idCatalogoPartida, string codigo, string nombre, string? descripcion,
        int? idPartidaPadre, string? codigoPartidaPadre, string? nombrePartidaPadre, int nivel, int idTipoPartida,
        string? codigoTipoPartida, string? nombreTipoPartida, bool activo, bool esHoja, int? idSeccionGasto,
        string? codigoSeccionGasto, string? nombreSeccionGasto, DateTime? fechaCreacion, DateTime? fechaActualizacion)
        => new()
        {
            IdCatalogoPartida = idCatalogoPartida,
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            IdPartidaPadre = idPartidaPadre,
            CodigoPartidaPadre = codigoPartidaPadre,
            NombrePartidaPadre = nombrePartidaPadre,
            Nivel = nivel,
            IdTipoPartida = idTipoPartida,
            CodigoTipoPartida = codigoTipoPartida,
            NombreTipoPartida = nombreTipoPartida,
            Activo = activo,
            EsHoja = esHoja,
            IdSeccionGasto = idSeccionGasto,
            CodigoSeccionGasto = codigoSeccionGasto,
            NombreSeccionGasto = nombreSeccionGasto,
            FechaCreacion = fechaCreacion,
            FechaActualizacion = fechaActualizacion
        };

    public void Actualizar(string nombre, int idTipoPartida, bool activo, int? idPartidaPadre, string? descripcion,
        int? idSeccionGasto)
    {
        if (idPartidaPadre == IdCatalogoPartida)
            throw new ValidacionPresupuestariaException("JERARQUIA_INVALIDA", "Una partida no puede ser su propio padre.");
        Nombre = Reglas.Requerido(nombre, "Nombre", 200);
        IdTipoPartida = Reglas.Id(idTipoPartida, "IdTipoPartida");
        Activo = activo;
        IdPartidaPadre = Reglas.IdOpcional(idPartidaPadre, "IdPartidaPadre");
        Descripcion = Reglas.Opcional(descripcion, "Descripcion", 500);
        IdSeccionGasto = Reglas.IdOpcional(idSeccionGasto, "IdSeccionGasto");
    }
}
