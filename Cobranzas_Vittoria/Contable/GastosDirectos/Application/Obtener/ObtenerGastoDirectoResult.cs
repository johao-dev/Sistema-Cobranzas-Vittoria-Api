using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Obtener;

public sealed record ObtenerGastoDirectoResult(GastoDirecto Gasto, IReadOnlyList<GastoDirectoDocumento> Documentos);
