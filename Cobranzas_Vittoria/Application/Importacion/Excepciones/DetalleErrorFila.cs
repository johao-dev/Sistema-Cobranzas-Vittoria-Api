namespace Cobranzas_Vittoria.Application.Importacion.Excepciones;

/// <summary>
/// Detalle de un error de validación a nivel de fila dentro del archivo importado.
/// Se serializa dentro de la respuesta 422 que produce <see cref="DatosInvalidosException"/>.
/// </summary>
public sealed record DetalleErrorFila(
    int Fila,
    string Campo,
    string CodigoError,
    string Mensaje);
