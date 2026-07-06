using Cobranzas_Vittoria.Tests.Setup;

namespace Cobranzas_Vittoria.Tests.Integration;

public abstract class IntegrationTestBase : IDisposable
{
    protected readonly CustomWebApplicationFactory _factory;
    protected readonly HttpClient _client;

    protected IntegrationTestBase()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}