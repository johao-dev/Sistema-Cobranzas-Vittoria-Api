namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

/// <summary>
/// Puerto de lectura de archivos CSV/XLSX: valida el archivo (extensión, tipo, tamaño) y devuelve
/// sus filas de datos. Los errores de archivo o estructura se informan con las excepciones del
/// motor de importación compartido (400/413/422).
/// </summary>
public interface ILectorArchivoTabular
{
    IReadOnlyList<FilaTabular> Leer(ArchivoTabular archivo);
}
