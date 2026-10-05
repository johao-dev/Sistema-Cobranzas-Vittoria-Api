using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Crear;

public sealed class CrearGastoDirectoHandler
{
    private readonly IGastoDirectoRepository _repository;
    private readonly ILogger<CrearGastoDirectoHandler> _logger;

    public CrearGastoDirectoHandler(IGastoDirectoRepository repository, ILogger<CrearGastoDirectoHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(CrearGastoDirectoCommand c)
    {
        var id = await _repository.CrearAsync(RegistroGastoDirecto.Crear(c.IdPresupuestoDetalle, c.IdProveedor, c.IdMoneda, c.Fecha, c.Concepto,
            c.Descripcion, c.Monto, c.Seccion, c.IdMonedaOriginal, c.MontoOriginal, c.TipoCambio, c.FechaTipoCambio));
        _logger.LogInformation("Gasto directo registrado: IdGastoDirecto={Id}, Seccion={Seccion}", id, c.Seccion);
        return id;
    }
}
