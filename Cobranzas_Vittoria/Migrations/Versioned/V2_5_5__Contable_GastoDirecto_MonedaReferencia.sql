/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.5.5
Módulo      : Contable / Gasto directo
Descripción : Moneda original de la factura, solo como referencia.
===============================================================================

El gasto se registra siempre en la moneda del presupuesto (IdMoneda + Monto):
el módulo no convierte monedas. Si la factura viene en otra moneda, se guardan
la moneda, el monto original y el tipo de cambio usado, SOLO como referencia.
Ningún cálculo de saldo los usa.

Regla: las tres columnas van juntas o ninguna; la moneda original debe ser
distinta de la del gasto y monto y tipo de cambio deben ser positivos.

Reentrante: cada objeto valida su propia ausencia.
===============================================================================
*/

-- La tabla nace en V2.3.1; se protege para bases que aún no la tienen.
IF OBJECT_ID(N'contable.GastoDirecto', N'U') IS NOT NULL
   AND COL_LENGTH(N'contable.GastoDirecto', N'IdMonedaOriginal') IS NULL
    ALTER TABLE contable.GastoDirecto
        ADD IdMonedaOriginal INT NULL
                CONSTRAINT FK_GastoDirecto_MonedaOriginal REFERENCES maestra.Moneda (IdMoneda),
            MontoOriginal DECIMAL(18,2) NULL,
            TipoCambio DECIMAL(18,6) NULL;
GO

-- EXEC dinámico: la sentencia solo se compila si las columnas existen.
IF COL_LENGTH(N'contable.GastoDirecto', N'IdMonedaOriginal') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_GastoDirecto_MonedaReferencia')
    EXEC (N'
    ALTER TABLE contable.GastoDirecto
        ADD CONSTRAINT CK_GastoDirecto_MonedaReferencia CHECK
        (
            (IdMonedaOriginal IS NULL AND MontoOriginal IS NULL AND TipoCambio IS NULL)
            OR (IdMonedaOriginal IS NOT NULL AND MontoOriginal > 0 AND TipoCambio > 0
                AND IdMonedaOriginal <> IdMoneda)
        );');
GO
