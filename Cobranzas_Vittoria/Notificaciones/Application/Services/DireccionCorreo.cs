namespace Cobranzas_Vittoria.Notificaciones.Application.Services;

/// <summary>Datos de dirección independientes del proveedor; no contiene identidad ni estado de persistencia.</summary>
public sealed record DireccionCorreo(string Correo, string? Nombre = null);
