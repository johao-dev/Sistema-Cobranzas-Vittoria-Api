namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

public sealed record Moneda(int IdMoneda, string Codigo, string Nombre, string? Simbolo, bool Activo);
