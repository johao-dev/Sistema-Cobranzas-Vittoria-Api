namespace Cobranzas_Vittoria.Compras.Domain.ValueObject;

/// <summary>Identifica los campos solicitados: un PATCH no debe sobrescribir los campos omitidos.</summary>
[Flags]
public enum CamposItemRequerimiento
{
    Ninguno = 0,
    Cantidad = 1,
    Partida = 2,
    Observacion = 4,
    Todos = Cantidad | Partida | Observacion
}
