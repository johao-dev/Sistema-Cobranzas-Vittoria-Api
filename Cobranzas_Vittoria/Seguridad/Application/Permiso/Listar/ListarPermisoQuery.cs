namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Listar;

/// <summary>
/// Query para listar permisos activos o inactivos.
/// </summary>
public sealed record ListarPermisoQuery(bool Activo = true);
