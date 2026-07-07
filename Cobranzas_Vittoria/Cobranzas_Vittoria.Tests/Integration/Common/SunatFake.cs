using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Dtos.Sunat;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Tests.Integration.Common;

/// <summary>
/// Stub de ISunatService para usar en pruebas de integración.
/// Permite simular el resultado de la consulta RUC sin llamar a Decolecta/PeruAPI.
///
/// Uso típico:
///   1) El test agrega un RUC a <see cref="RucsExistentes"/> antes del Act
///   2) Invoca el endpoint
///   3) Verifica el resultado (200 con datos o 404 sin datos)
///
/// Se inyecta vía ConfigureTestServices en CustomWebApplicationFactory
/// como singleton, por lo que el mismo RUC configurado en un test
/// se ve en otros. Cada test agrega lo que necesita; los no configurados
/// se tratan como inexistentes (devuelven null).
/// </summary>
public class SunatFake : ISunatService
{
    public HashSet<string> RucsExistentes { get; } = new();

    public Task<ProveedorConsultaSunatDto> ConsultarRucAsync(string ruc)
    {
        if (!RucsExistentes.Contains(ruc))
            return Task.FromResult<ProveedorConsultaSunatDto>(null!);

        return Task.FromResult(new ProveedorConsultaSunatDto
        {
            NumeroDocumento = ruc,
            RazonSocial = $"Razon Social SUNAT {ruc}",
            Estado = "ACTIVO",
            Condicion = "HABIDO",
            Direccion = "AV. EJEMPLO 123",
        });
    }

    public Task<TipoCambioResponseDto> ConsultarTipoCambio(string? fechaSolicitada)
    {
        // Por ahora TipoCambioController no se está probando con mock;
        // si se llega a usar, devolver un valor fijo.
        return Task.FromResult(new TipoCambioResponseDto
        {
            Fecha = fechaSolicitada ?? DateTime.Today.ToString("yyyy-MM-dd"),
            PrecioCompra = "3.750",
            PrecioVenta = "3.780",
            MonedaBase = "USD",
            CotizacionDeDivisa = "PEN",
        });
    }
}
