using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Notificaciones.Application.Options;

public sealed class NotificacionProcesamientoOptions
{
    [Range(1, int.MaxValue)]
    public int TamanoLote { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int MaximoIntentos { get; set; } = 3;

    [Range(1, int.MaxValue)]
    public int IntervaloReintentoMinutos { get; set; } = 5;

    [Range(1, int.MaxValue)]
    public int TimeoutProcesamientoMinutos { get; set; } = 10;

    /// <summary>Los handlers validan antes de operar, incluso cuando se construyen sin un contenedor DI.</summary>
    public void Validar()
    {
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
    }
}
