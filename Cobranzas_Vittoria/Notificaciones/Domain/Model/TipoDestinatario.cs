namespace Cobranzas_Vittoria.Notificaciones.Domain.Model;

/// <summary>Estrategias conocidas por el dominio; sus valores no representan los IDs del catálogo SQL.</summary>
/// <remarks>Persistencia debe traducir los nombres del catálogo, como ROL, a estos valores.</remarks>
public enum TipoDestinatario
{
    Rol,
    Usuario,
}
