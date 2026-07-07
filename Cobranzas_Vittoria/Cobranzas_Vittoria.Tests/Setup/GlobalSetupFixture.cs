using Testcontainers.MsSql;
using Cobranzas_Vittoria.Tests.Setup;

namespace Cobranzas_Vittoria.Tests;

[SetUpFixture]
public class GlobalSetupFixture
{
    public static MsSqlContainer DbContainer { get; private set; } = null!;
    public static CustomWebApplicationFactory Factory { get; private set; } = null!;
    public static HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    [Obsolete] // La iniciación de MsSqlBuilder sin parametros está obsoleta.
    public async Task RunBeforeAnyTests()
    {
        DbContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword("TestAdmin123#")
            .Build();
        await DbContainer.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", DbContainer.GetConnectionString());

        Factory = new CustomWebApplicationFactory();
        Client =Factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task RunAfterAllTests()
    {
        Client?.Dispose();
        Factory?.Dispose();
        if (DbContainer != null)
        {
            await DbContainer.DisposeAsync();
        }
    }
}
