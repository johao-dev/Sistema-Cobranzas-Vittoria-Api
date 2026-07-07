namespace Cobranzas_Vittoria.Tests.Integration.Common;

/// <summary>
/// IDs canónicos insertados por V1_1_0__SeedData.sql.
/// Sirven como contrato explícito entre los tests y el seed:
/// si el seed cambia, aquí se actualiza y los tests siguen pasando.
/// </summary>
public static class SeedIds
{
    // --- seguridad.Usuario (orden del seed) ---
    public const int AdminId = 1;        // UsuarioLogin = admin
    public const int IngenieroId = 2;    // UsuarioLogin = ingeniero
    public const int AlmacenId = 3;      // UsuarioLogin = almacen
    public const int ContableId = 4;     // UsuarioLogin = contable

    // --- maestra.Proyecto (forzado con SET IDENTITY_INSERT) ---
    public const int ProyectoMaytaCapacII = 10;

    // --- maestra.Especialidad (las primeras con Activo = 1) ---
    public const int EspecialidadAlbanileria = 2;
    public const int EspecialidadArquitectura = 3;
    public const int EspecialidadCasco = 4;
    public const int EspecialidadElectrico = 5;
    public const int EspecialidadEstructura = 6;
    public const int EspecialidadGas = 7;
    public const int EspecialidadMecanicas = 8;
    public const int EspecialidadObra = 9;
    public const int EspecialidadOficinaTf = 10;
    public const int EspecialidadPolifusion = 11;
    public const int EspecialidadSanitario = 12;
    public const int EspecialidadSsoma = 13;
    public const int EspecialidadTf = 14;

    // --- maestra.UnidadMedida (las del seed) ---
    public const int UnidadMedidaUm001 = 1;     // UM-001 - Unidad
    public const int UnidadMedidaBal = 2;       // BAL - Balde
    public const int UnidadMedidaBol = 3;       // BOL - Bolsa
    public const int UnidadMedidaCaj = 4;       // CAJ - Caja
    public const int UnidadMedidaGal = 5;       // GAL - Galón
    public const int UnidadMedidaKg = 6;        // KG  - Kilogramo
    public const int UnidadMedidaLat = 7;       // LAT - Lata
    public const int UnidadMedidaM2 = 8;        // M2  - Metro cuadrado
    public const int UnidadMedidaMl = 9;        // ML  - Metro lineal
    public const int UnidadMedidaPom = 10;      // POM - Pomo
    public const int UnidadMedidaRol = 11;      // ROL - Rollo
    public const int UnidadMedidaUnd = 12;      // UND - Unidad
}
