using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.Importar;

public sealed record ImportarPresupuestoDetalleCommand(int IdPresupuesto, int IdPresupuestoVersion, ArchivoTabular Archivo,
    bool QuitarAusentes);
