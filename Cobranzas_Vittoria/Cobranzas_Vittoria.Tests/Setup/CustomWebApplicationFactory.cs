using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cobranzas_Vittoria.Tests.Setup;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Singleton compartido entre tests para mockear ISunatService.
    /// Los tests pueden agregar RUCs a RucsExistentes antes de invocar el endpoint.
    /// </summary>
    public SunatFake Sunat { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        // Aqui se configura el ambiente de prueba
        // Reemplaza HttpClient y servicios externos con stubs
        builder.ConfigureTestServices(services =>
        {
            // Quita el HttpClient real registrado por AddHttpClient<ISunatService,...>
            // y vuelve a registrar ISunatService con el stub. AddSingleton es importante
            // para que SunatFake conserve estado entre invocaciones del mismo test.
            services.RemoveAll<ISunatService>();
            services.AddSingleton<ISunatService>(Sunat);
        });
    }
}