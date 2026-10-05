using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Actualizar;

public sealed class ActualizarGastoDirectoHandler
{
    private readonly IGastoDirectoRepository _repository;

    public ActualizarGastoDirectoHandler(IGastoDirectoRepository repository) => _repository = repository;

    public Task<int> HandleAsync(ActualizarGastoDirectoCommand c)
    {
        GastoDirectoValidator.ValidarId(c.IdGastoDirecto);
        return _repository.ActualizarAsync(c.IdGastoDirecto, RegistroGastoDirecto.Crear(c.IdPresupuestoDetalle, c.IdProveedor, c.IdMoneda, c.Fecha, c.Concepto,
            c.Descripcion, c.Monto, c.Seccion, c.IdMonedaOriginal, c.MontoOriginal, c.TipoCambio, c.FechaTipoCambio));
    }
}
