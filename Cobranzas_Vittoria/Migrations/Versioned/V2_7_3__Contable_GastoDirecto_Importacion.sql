/* Reutiliza la relación de documentos para referencias de factura sin PDF.
   NombreArchivo conserva el número como etiqueta; RutaArchivo NULL distingue una referencia.
   No modifica los adjuntos existentes ni crea otra relación de facturas. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;
ALTER TABLE contable.GastoDirectoDocumento DROP CONSTRAINT CK_GastoDirectoDocumento_Ruta;
ALTER TABLE contable.GastoDirectoDocumento ALTER COLUMN RutaArchivo NVARCHAR(500) NULL;
ALTER TABLE contable.GastoDirectoDocumento ADD CONSTRAINT CK_GastoDirectoDocumento_Ruta CHECK
(
    (RutaArchivo IS NOT NULL AND LEN(LTRIM(RTRIM(RutaArchivo))) > 0)
    OR (RutaArchivo IS NULL AND TipoDocumento = 'Factura' AND Extension IS NULL)
);
ALTER TABLE contable.GastoDirecto ADD UsuarioCreacion NVARCHAR(256) NULL;
COMMIT TRANSACTION;
