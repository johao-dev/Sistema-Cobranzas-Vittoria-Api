namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;

/// <summary>Registro validado y referencias de facturas para persistir en una sola transacción.</summary>
public sealed record GastoDirectoImportado(int Fila, int IdCentroCosto, RegistroGastoDirecto Registro,
    IReadOnlyList<string> Facturas);
