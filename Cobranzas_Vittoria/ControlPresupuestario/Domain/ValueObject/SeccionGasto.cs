namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

/// <summary>
/// Sección de gasto directo: cada pantalla de Operaciones → Gastos del proyecto.
/// CodigosTipoCentroCosto lista, separados por coma, los tipos de centro de costo que admite.
/// </summary>
public sealed record SeccionGasto(int IdSeccionGasto, string Codigo, string Nombre, string? Descripcion, int Orden,
    bool Activo, string? CodigosTipoCentroCosto);
