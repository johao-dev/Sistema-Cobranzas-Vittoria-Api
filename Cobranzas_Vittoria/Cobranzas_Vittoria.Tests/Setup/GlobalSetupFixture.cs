using Testcontainers.MsSql;
using Cobranzas_Vittoria.Tests.Setup;

namespace Cobranzas_Vittoria.Tests;

[SetUpFixture]
public class GlobalSetupFixture
{
    private const string DefaultJwtKey = "integration-tests-super-secret-key-123456";
    private const string DefaultJwtIssuer = "vittoria-api";
    private const string DefaultJwtAudience = "vittoria-client";

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
        SetEnvIfMissing("Jwt__Key", DefaultJwtKey);
        SetEnvIfMissing("Jwt__Issuer", DefaultJwtIssuer);
        SetEnvIfMissing("Jwt__Audience", DefaultJwtAudience);

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

    private static void SetEnvIfMissing(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
