using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Common;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Documentos.Subir;

public sealed record SubirDocumentosCommand(int IdGastoDirecto, string? TipoDocumento, IReadOnlyList<ArchivoAdjunto> Archivos);
