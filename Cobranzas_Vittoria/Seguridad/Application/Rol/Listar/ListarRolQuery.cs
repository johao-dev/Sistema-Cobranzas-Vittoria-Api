namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Listar;

/// <summary>
/// Query para listar roles activos o inactivos.
/// </summary>
public sealed record ListarRolQuery(bool? Activo = true);
