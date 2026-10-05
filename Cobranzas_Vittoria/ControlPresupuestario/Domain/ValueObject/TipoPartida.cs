namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

public sealed record TipoPartida(int IdTipoPartida, string Codigo, string Nombre, string? Descripcion, bool Activo);
