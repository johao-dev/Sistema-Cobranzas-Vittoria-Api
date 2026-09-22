SET XACT_ABORT ON;
BEGIN TRANSACTION;

CREATE TABLE contable.GastoDirecto
(
    IdGastoDirecto INT IDENTITY(1,1) NOT NULL,
    IdPresupuestoDetalle INT NOT NULL,
    IdProveedor INT NULL,
    IdMoneda INT NOT NULL,
    Fecha DATE NOT NULL,
    Concepto NVARCHAR(250) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Monto DECIMAL(18,2) NOT NULL,
    Estado VARCHAR(20) NOT NULL
        CONSTRAINT DF_GastoDirecto_Estado DEFAULT 'REGISTRADO',
    FechaCreacion DATETIME2(0) NOT NULL
        CONSTRAINT DF_GastoDirecto_FechaCreacion DEFAULT SYSUTCDATETIME(),
    FechaActualizacion DATETIME2(0) NULL,
    CONSTRAINT PK_GastoDirecto PRIMARY KEY (IdGastoDirecto),
    CONSTRAINT FK_GastoDirecto_PresupuestoDetalle FOREIGN KEY (IdPresupuestoDetalle)
        REFERENCES ControlPresupuestario.PresupuestoDetalle(IdPresupuestoDetalle),
    CONSTRAINT FK_GastoDirecto_Proveedor FOREIGN KEY (IdProveedor)
        REFERENCES maestra.Proveedor(IdProveedor),
    CONSTRAINT FK_GastoDirecto_Moneda FOREIGN KEY (IdMoneda)
        REFERENCES maestra.Moneda(IdMoneda),
    CONSTRAINT CK_GastoDirecto_Monto CHECK (Monto > 0),
    CONSTRAINT CK_GastoDirecto_Concepto CHECK (LEN(LTRIM(RTRIM(Concepto))) > 0),
    CONSTRAINT CK_GastoDirecto_Estado CHECK
        (Estado IN ('REGISTRADO', 'CONFIRMADO', 'ANULADO'))
);

CREATE INDEX IX_GastoDirecto_PresupuestoDetalle
    ON contable.GastoDirecto(IdPresupuestoDetalle, Estado)
    INCLUDE (Monto, IdMoneda, Fecha);
CREATE INDEX IX_GastoDirecto_Proveedor
    ON contable.GastoDirecto(IdProveedor) WHERE IdProveedor IS NOT NULL;
CREATE INDEX IX_GastoDirecto_Fecha ON contable.GastoDirecto(Fecha DESC);

CREATE TABLE contable.GastoDirectoDocumento
(
    IdGastoDirectoDocumento INT IDENTITY(1,1) NOT NULL,
    IdGastoDirecto INT NOT NULL,
    TipoDocumento VARCHAR(20) NOT NULL,
    NombreArchivo NVARCHAR(255) NOT NULL,
    RutaArchivo NVARCHAR(500) NOT NULL,
    Extension NVARCHAR(20) NULL,
    FechaCreacion DATETIME2(0) NOT NULL
        CONSTRAINT DF_GastoDirectoDocumento_FechaCreacion DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GastoDirectoDocumento PRIMARY KEY (IdGastoDirectoDocumento),
    CONSTRAINT FK_GastoDirectoDocumento_GastoDirecto FOREIGN KEY (IdGastoDirecto)
        REFERENCES contable.GastoDirecto(IdGastoDirecto),
    CONSTRAINT CK_GastoDirectoDocumento_Tipo CHECK
        (TipoDocumento IN ('Factura', 'Pago')),
    CONSTRAINT CK_GastoDirectoDocumento_Nombre CHECK
        (LEN(LTRIM(RTRIM(NombreArchivo))) > 0),
    CONSTRAINT CK_GastoDirectoDocumento_Ruta CHECK
        (LEN(LTRIM(RTRIM(RutaArchivo))) > 0)
);
CREATE INDEX IX_GastoDirectoDocumento_Gasto
    ON contable.GastoDirectoDocumento(IdGastoDirecto, FechaCreacion DESC);

CREATE TABLE contable.GastoDirectoLegacyMap
(
    Origen VARCHAR(30) NOT NULL,
    IdLegacy INT NOT NULL,
    IdGastoDirecto INT NOT NULL,
    FechaMigracion DATETIME2(0) NOT NULL
        CONSTRAINT DF_GastoDirectoLegacyMap_FechaMigracion DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GastoDirectoLegacyMap PRIMARY KEY (Origen, IdLegacy),
    CONSTRAINT UQ_GastoDirectoLegacyMap_Destino UNIQUE (IdGastoDirecto),
    CONSTRAINT FK_GastoDirectoLegacyMap_Destino FOREIGN KEY (IdGastoDirecto)
        REFERENCES contable.GastoDirecto(IdGastoDirecto),
    CONSTRAINT CK_GastoDirectoLegacyMap_Origen CHECK
        (Origen IN ('GASTO_PROYECTO', 'GASTO_ADMIN'))
);

DECLARE @MigradosProyecto TABLE (IdLegacy INT NOT NULL, IdGastoDirecto INT NOT NULL);
MERGE contable.GastoDirecto AS destino
USING
(
    SELECT gp.IdGastoProyecto AS IdLegacy, gp.IdPresupuestoDetalle,
        pm.IdProveedor, m.IdMoneda, gp.Fecha, gp.Concepto, gp.Descripcion,
        CASE WHEN UPPER(LTRIM(RTRIM(gp.Moneda))) = 'USD'
             THEN gp.MontoDolares ELSE gp.MontoSoles END AS Monto,
        CASE WHEN gp.Activo = 1 AND gp.Estado = 'Activo'
             THEN 'REGISTRADO' ELSE 'ANULADO' END AS Estado,
        CONVERT(DATETIME2(0), gp.FechaCreacion) AS FechaCreacion,
        CONVERT(DATETIME2(0), gp.FechaActualizacion) AS FechaActualizacion
    FROM contable.GastoProyecto gp
    JOIN ControlPresupuestario.PresupuestoDetalle pd
        ON pd.IdPresupuestoDetalle = gp.IdPresupuestoDetalle
    JOIN ControlPresupuestario.PresupuestoVersion pv
        ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
    JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
    JOIN maestra.Moneda m
        ON m.Codigo = UPPER(LTRIM(RTRIM(gp.Moneda))) AND m.IdMoneda = p.IdMoneda
    LEFT JOIN maestra.ProveedorLegacyMap pm
        ON pm.Origen = 'PROVEEDOR_TERRENO'
       AND pm.IdLegacy = gp.IdProveedorTerreno
    WHERE gp.IdPresupuestoDetalle IS NOT NULL
      AND UPPER(LTRIM(RTRIM(gp.Moneda))) IN ('PEN', 'USD')
      AND CASE WHEN UPPER(LTRIM(RTRIM(gp.Moneda))) = 'USD'
               THEN gp.MontoDolares ELSE gp.MontoSoles END > 0
) AS fuente ON 1 = 0
WHEN NOT MATCHED THEN INSERT
    (IdPresupuestoDetalle, IdProveedor, IdMoneda, Fecha, Concepto, Descripcion,
     Monto, Estado, FechaCreacion, FechaActualizacion)
VALUES
    (fuente.IdPresupuestoDetalle, fuente.IdProveedor, fuente.IdMoneda,
     fuente.Fecha, fuente.Concepto, fuente.Descripcion, fuente.Monto,
     fuente.Estado, fuente.FechaCreacion, fuente.FechaActualizacion)
OUTPUT fuente.IdLegacy, inserted.IdGastoDirecto
INTO @MigradosProyecto(IdLegacy, IdGastoDirecto);

INSERT INTO contable.GastoDirectoLegacyMap (Origen, IdLegacy, IdGastoDirecto)
SELECT 'GASTO_PROYECTO', IdLegacy, IdGastoDirecto FROM @MigradosProyecto;

DECLARE @MigradosAdmin TABLE (IdLegacy INT NOT NULL, IdGastoDirecto INT NOT NULL);
MERGE contable.GastoDirecto AS destino
USING
(
    SELECT ga.IdGastoAdministrativo AS IdLegacy, ga.IdPresupuestoDetalle,
        COALESCE(pm.IdProveedor, ga.IdProveedor) AS IdProveedor,
        m.IdMoneda, ga.Fecha, CONVERT(NVARCHAR(250), N'GASTO ADMINISTRATIVO') AS Concepto,
        ga.Descripcion, ga.Monto,
        CASE WHEN ga.Activo = 1 THEN 'REGISTRADO' ELSE 'ANULADO' END AS Estado,
        CONVERT(DATETIME2(0), ga.FechaCreacion) AS FechaCreacion,
        CAST(NULL AS DATETIME2(0)) AS FechaActualizacion
    FROM contable.GastoAdministrativo ga
    JOIN ControlPresupuestario.PresupuestoDetalle pd
        ON pd.IdPresupuestoDetalle = ga.IdPresupuestoDetalle
    JOIN ControlPresupuestario.PresupuestoVersion pv
        ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
    JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
    JOIN maestra.Moneda m
        ON m.Codigo = UPPER(LTRIM(RTRIM(ga.Moneda))) AND m.IdMoneda = p.IdMoneda
    LEFT JOIN maestra.ProveedorLegacyMap pm
        ON pm.Origen = 'PROVEEDOR_GASTO_ADMIN'
       AND pm.IdLegacy = ga.IdProveedorGastoAdministrativo
    WHERE ga.IdPresupuestoDetalle IS NOT NULL
      AND UPPER(LTRIM(RTRIM(ga.Moneda))) IN ('PEN', 'USD')
      AND ga.Monto > 0
      AND (COALESCE(pm.IdProveedor, ga.IdProveedor) IS NULL OR EXISTS
           (SELECT 1 FROM maestra.Proveedor p2
            WHERE p2.IdProveedor = COALESCE(pm.IdProveedor, ga.IdProveedor)))
) AS fuente ON 1 = 0
WHEN NOT MATCHED THEN INSERT
    (IdPresupuestoDetalle, IdProveedor, IdMoneda, Fecha, Concepto, Descripcion,
     Monto, Estado, FechaCreacion, FechaActualizacion)
VALUES
    (fuente.IdPresupuestoDetalle, fuente.IdProveedor, fuente.IdMoneda,
     fuente.Fecha, fuente.Concepto, fuente.Descripcion, fuente.Monto,
     fuente.Estado, fuente.FechaCreacion, fuente.FechaActualizacion)
OUTPUT fuente.IdLegacy, inserted.IdGastoDirecto
INTO @MigradosAdmin(IdLegacy, IdGastoDirecto);

INSERT INTO contable.GastoDirectoLegacyMap (Origen, IdLegacy, IdGastoDirecto)
SELECT 'GASTO_ADMIN', IdLegacy, IdGastoDirecto FROM @MigradosAdmin;

INSERT INTO contable.GastoDirectoDocumento
    (IdGastoDirecto, TipoDocumento, NombreArchivo, RutaArchivo, Extension, FechaCreacion)
SELECT gm.IdGastoDirecto, 'Factura', d.NombreArchivo, d.RutaArchivo,
    d.Extension, CONVERT(DATETIME2(0), d.FechaCreacion)
FROM contable.GastoProyectoDocumento d
JOIN contable.GastoDirectoLegacyMap gm
  ON gm.Origen = 'GASTO_PROYECTO' AND gm.IdLegacy = d.IdGastoProyecto;

INSERT INTO contable.GastoDirectoDocumento
    (IdGastoDirecto, TipoDocumento, NombreArchivo, RutaArchivo, Extension, FechaCreacion)
SELECT gm.IdGastoDirecto,
    CASE WHEN d.TipoDocumento = 'Pago' THEN 'Pago' ELSE 'Factura' END,
    d.NombreArchivo, d.RutaArchivo, d.Extension,
    CONVERT(DATETIME2(0), d.FechaCreacion)
FROM contable.GastoAdministrativoDocumento d
JOIN contable.GastoDirectoLegacyMap gm
  ON gm.Origen = 'GASTO_ADMIN' AND gm.IdLegacy = d.IdGastoAdministrativo;

COMMIT TRANSACTION;
