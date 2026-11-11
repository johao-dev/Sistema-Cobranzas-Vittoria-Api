using System.Globalization;
using DbUp.Engine.Output;

namespace Cobranzas_Vittoria.Tests.Setup;

/// <summary>Conserva el diagnóstico de DbUp como adjunto sin llenar la consola de las pruebas.</summary>
internal sealed class LogMigracionPrueba : IUpgradeLog
{
    private readonly string _ruta;
    private readonly object _sincronizacion = new();

    public LogMigracionPrueba()
    {
        string directorio = Path.Combine(Path.GetTempPath(), "vittoria-pruebas", "migraciones");
        Directory.CreateDirectory(directorio);
        _ruta = Path.Combine(directorio, $"{Guid.NewGuid():N}.log");
        File.WriteAllText(_ruta, TestContext.CurrentContext.Test.FullName + Environment.NewLine);
        TestContext.AddTestAttachment(_ruta, "Diagnóstico de migraciones SQL");
    }

    public void LogTrace(string format, params object[] args) => Escribir("TRACE", format, args);
    public void LogDebug(string format, params object[] args) => Escribir("DEBUG", format, args);
    public void LogInformation(string format, params object[] args) => Escribir("INFO", format, args);
    public void LogWarning(string format, params object[] args) => Escribir("WARN", format, args);
    public void LogError(string format, params object[] args) => Escribir("ERROR", format, args);

    public void LogError(Exception ex, string format, params object[] args)
        => Escribir("ERROR", "{0}{1}{2}",
            [string.Format(CultureInfo.InvariantCulture, format, args), Environment.NewLine, ex]);

    private void Escribir(string nivel, string formato, object[] argumentos)
    {
        string mensaje = string.Format(CultureInfo.InvariantCulture, formato, argumentos);
        lock (_sincronizacion)
        {
            File.AppendAllText(_ruta, $"[{nivel}] {mensaje}{Environment.NewLine}");
        }
    }
}
