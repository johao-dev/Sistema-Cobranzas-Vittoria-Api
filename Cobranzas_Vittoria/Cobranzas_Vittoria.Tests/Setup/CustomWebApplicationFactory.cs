using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cobranzas_Vittoria.Tests.Setup;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable
        (
            "ConnectionStrings__Default",
            "Server=127.0.0.1,1433;Database=VittoriaComprasDB_Dev;User Id=sa;Password=AdminPassword12#;TrustServerCertificate=True;"
        );
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}