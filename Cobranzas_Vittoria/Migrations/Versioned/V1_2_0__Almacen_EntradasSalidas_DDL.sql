-- =============================================================================
-- Migracion versionada: V1_2_0__Almacen_EntradasSalidas_DDL.sql
--
-- Crea las tablas, tipos y constraints del nuevo modulo Inventario (Kardex
-- manual) en el esquema `almacen`. Esta migracion es estrictamente aditiva:
-- no modifica ninguna tabla, vista, SP ni tipo existente. El kardex ligado al
-- flujo de Compras (almacen.KardexMovimiento) queda intacto.
--
-- Modulo:        Modulo Inventario (Kardex manual de entradas / salidas / stock).
-- Version:       1.2.0 (siguiente versionado tras V1_1_2__Maestra_Importacion_Tipos).
-- Rango errores: 51100-51199 reservado para los SPs de este modulo.
--
-- Objetos creados:
--   - almacen.KardexEntrada          (cabecera + item unico, sin tabla detalle)
--   - almacen.KardexSalida           (cabecera de salida manual)
--   - almacen.KardexSalidaDetalle    (1..N materiales por salida)
--   - almacen.KardexStock            (consolidado en tiempo real)
--   - almacen.TVP_KardexSalidaItem   (UDDT para items de salida)
--
-- Reglas de mantenimiento de KardexStock:
--   TotalEntrada, TotalSalida y Stock se actualizan SOLO desde los SPs
--   almacen.usp_KardexEntrada_* y almacen.usp_KardexSalida_*, dentro de la
--   misma transaccion que la operacion que los origina (SET XACT_ABORT ON).
--   La invariante Stock = TotalEntrada - TotalSalida se enforce por SP, no
--   por CHECK (SQL Server no permite CHECKs derivados entre columnas).
--   Los CHECKs declarados son solo la no-negatividad: si la logica del SP
--   se rompe, los CHECKs actuan como segunda linea de defensa.
--
-- Convenciones aplicadas:
--   - Schema:           almacen.*
--   - Collation:        SQL_Latin1_General_CP1_CI_AS en columnas nvarchar
--   - PKs / FKs / CKs / IXs con nombres explicitos (PK_Tabla, FK_Tabla_Ref, ...)
--   - Tipos de texto:   nvarchar (no varchar) para mapear 1-a-1 con DataTable
--                       de ADO.NET (patron ya aplicado en V1_1_2).
--   - Indentacion:      tabuladores (mismo estilo que V1_0_9__Almacen_DDL.sql,
--                       la unica otra migracion que toca el esquema `almacen`).
-- =============================================================================


-- -----------------------------------------------------------------------------
-- almacen.KardexEntrada
--   Cabecera de entrada manual de almacen. Modela 1 entrada = 1 material
--   (las columnas IdMaterial y Cantidad viven en la misma tabla; no hay
--   tabla de detalle por KardexEntrada).
--
--   Es INDEPENDIENTE del flujo de Compras: no tiene IdCompra, IdOrdenCompra
--   ni IdKardexMovimiento. KardexMovimiento queda aislado para Compras.
--
--   Columnas:
--     - IdKardexEntrada  PK identity
--     - IdEspecialidad   FK a maestra.Especialidad (REQUERIDO)
--     - IdMaterial       FK a maestra.Material     (REQUERIDO)
--     - IdProveedor      FK a maestra.Proveedor    (OPCIONAL)
--     - IdProyecto       FK a maestra.Proyecto     (OPCIONAL, ver decision bloqueada)
--     - NumeroDocumento  ej: "F001-12345"          (OPCIONAL)
--     - Fecha            fecha del movimiento      (REQUERIDO)
--     - Cantidad         cantidad ingresada        (REQUERIDO, >= 0)
--     - Observacion      texto libre               (OPCIONAL)
--     - FechaCreacion    default sysdatetime()     (auditoria)
-- -----------------------------------------------------------------------------
CREATE TABLE almacen.KardexEntrada (
	IdKardexEntrada  int IDENTITY(1,1) NOT NULL,
	IdEspecialidad   int NOT NULL,
	IdMaterial       int NOT NULL,
	IdProveedor      int NULL,
	IdProyecto       int NULL,
	NumeroDocumento  nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Fecha            date NOT NULL,
	Cantidad         decimal(18,2) NOT NULL,
	Observacion      nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion    datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK_KardexEntrada PRIMARY KEY (IdKardexEntrada)
);
GO

ALTER TABLE almacen.KardexEntrada WITH CHECK
	ADD CONSTRAINT CK_KardexEntrada_Cantidad
	CHECK (Cantidad >= 0);
GO

ALTER TABLE almacen.KardexEntrada
	ADD CONSTRAINT FK_KardexEntrada_Especialidad
	FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad);
ALTER TABLE almacen.KardexEntrada
	ADD CONSTRAINT FK_KardexEntrada_Material
	FOREIGN KEY (IdMaterial) REFERENCES maestra.Material(IdMaterial);
ALTER TABLE almacen.KardexEntrada
	ADD CONSTRAINT FK_KardexEntrada_Proveedor
	FOREIGN KEY (IdProveedor) REFERENCES maestra.Proveedor(IdProveedor);
ALTER TABLE almacen.KardexEntrada
	ADD CONSTRAINT FK_KardexEntrada_Proyecto
	FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);
GO

CREATE NONCLUSTERED INDEX IX_KardexEntrada_Fecha
	ON almacen.KardexEntrada (Fecha DESC);
CREATE NONCLUSTERED INDEX IX_KardexEntrada_Material_Especialidad
	ON almacen.KardexEntrada (IdMaterial, IdEspecialidad);
CREATE NONCLUSTERED INDEX IX_KardexEntrada_Proyecto
	ON almacen.KardexEntrada (IdProyecto)
	WHERE IdProyecto IS NOT NULL;
GO


-- -----------------------------------------------------------------------------
-- almacen.KardexSalida
--   Cabecera de salida manual. Una salida tiene 1..N materiales en
--   KardexSalidaDetalle. Esta tabla NO contiene Cantidad (la cantidad vive
--   en el detalle, por material).
--
--   Columnas:
--     - IdKardexSalida   PK identity
--     - IdEspecialidad   FK a maestra.Especialidad (REQUERIDO)
--     - IdProyecto       FK a maestra.Proyecto     (OPCIONAL)
--     - NumeroDocumento  ej: "S001-12345"          (OPCIONAL)
--     - Fecha            fecha del movimiento      (REQUERIDO)
--     - Solicitante      nombre de quien solicita  (REQUERIDO, no vacio)
--     - Observacion      texto libre general       (OPCIONAL)
--     - FechaCreacion    default sysdatetime()     (auditoria)
-- -----------------------------------------------------------------------------
CREATE TABLE almacen.KardexSalida (
	IdKardexSalida   int IDENTITY(1,1) NOT NULL,
	IdEspecialidad   int NOT NULL,
	IdProyecto       int NULL,
	NumeroDocumento  nvarchar(50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	Fecha            date NOT NULL,
	Solicitante      nvarchar(150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	Observacion      nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	FechaCreacion    datetime2(0) DEFAULT sysdatetime() NOT NULL,
	CONSTRAINT PK_KardexSalida PRIMARY KEY (IdKardexSalida)
);
GO

ALTER TABLE almacen.KardexSalida
	ADD CONSTRAINT FK_KardexSalida_Especialidad
	FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad);
ALTER TABLE almacen.KardexSalida
	ADD CONSTRAINT FK_KardexSalida_Proyecto
	FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);
GO

CREATE NONCLUSTERED INDEX IX_KardexSalida_Fecha
	ON almacen.KardexSalida (Fecha DESC);
CREATE NONCLUSTERED INDEX IX_KardexSalida_Proyecto
	ON almacen.KardexSalida (IdProyecto)
	WHERE IdProyecto IS NOT NULL;
GO


-- -----------------------------------------------------------------------------
-- almacen.KardexSalidaDetalle
--   1..N materiales por salida. ON DELETE CASCADE: al eliminar la cabecera
--   KardexSalida, sus detalles se eliminan automaticamente.
--
--   Columnas:
--     - IdKardexSalidaDetalle  PK identity
--     - IdKardexSalida         FK a KardexSalida (CASCADE)
--     - IdMaterial             FK a maestra.Material
--     - Cantidad               cantidad despachada   (REQUERIDO, >= 0)
--     - Observacion            nota del item         (OPCIONAL)
-- -----------------------------------------------------------------------------
CREATE TABLE almacen.KardexSalidaDetalle (
	IdKardexSalidaDetalle  int IDENTITY(1,1) NOT NULL,
	IdKardexSalida         int NOT NULL,
	IdMaterial             int NOT NULL,
	Cantidad               decimal(18,2) NOT NULL,
	Observacion            nvarchar(250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	CONSTRAINT PK_KardexSalidaDetalle PRIMARY KEY (IdKardexSalidaDetalle)
);
GO

ALTER TABLE almacen.KardexSalidaDetalle WITH CHECK
	ADD CONSTRAINT CK_KardexSalidaDetalle_Cantidad
	CHECK (Cantidad >= 0);
GO

ALTER TABLE almacen.KardexSalidaDetalle
	ADD CONSTRAINT FK_KardexSalidaDetalle_Salida
	FOREIGN KEY (IdKardexSalida) REFERENCES almacen.KardexSalida(IdKardexSalida) ON DELETE CASCADE;
ALTER TABLE almacen.KardexSalidaDetalle
	ADD CONSTRAINT FK_KardexSalidaDetalle_Material
	FOREIGN KEY (IdMaterial) REFERENCES maestra.Material(IdMaterial);
GO

CREATE NONCLUSTERED INDEX IX_KardexSalidaDetalle_Salida
	ON almacen.KardexSalidaDetalle (IdKardexSalida);
CREATE NONCLUSTERED INDEX IX_KardexSalidaDetalle_Material
	ON almacen.KardexSalidaDetalle (IdMaterial);
GO


-- -----------------------------------------------------------------------------
-- almacen.KardexStock
--   Inventario consolidado en tiempo real. Mantenido SOLO desde los SPs
--   almacen.usp_KardexEntrada_* y almacen.usp_KardexSalida_*.
--   Lectura directa desde vw_Kardex_StockActual_v2.
--
--   Columnas:
--     - IdKardexStock          PK identity
--     - IdMaterial             FK a maestra.Material     (REQUERIDO)
--     - IdEspecialidad         FK a maestra.Especialidad (REQUERIDO)
--     - IdProyecto             FK a maestra.Proyecto     (OPCIONAL)
--     - TotalEntrada           acumulado de entradas     (default 0)
--     - TotalSalida            acumulado de salidas      (default 0)
--     - Stock                  TotalEntrada - TotalSalida (default 0, >= 0)
--     - FechaUltimaMovimiento  fecha del ultimo INSERT/UPDATE en KardexStock
--
--   UNIQUE (IdMaterial, IdEspecialidad, IdProyecto): una sola fila por triada
--   (semantica historica; reemplazada por stock global en V1_4_1).
--   A partir de V1_4_1 KardexStock ya no tiene IdProyecto y el stock es
--   global por (IdMaterial, IdEspecialidad).
--
--   Invariante:
--     Stock = TotalEntrada - TotalSalida
--   Enforced por SP dentro de la misma TX que la operacion que origina el
--   cambio. Los CHECKs son salvaguarda de no-negatividad.
-- -----------------------------------------------------------------------------
CREATE TABLE almacen.KardexStock (
	IdKardexStock          int IDENTITY(1,1) NOT NULL,
	IdMaterial             int NOT NULL,
	IdEspecialidad         int NOT NULL,
	IdProyecto             int NULL,
	TotalEntrada           decimal(18,2) NOT NULL DEFAULT 0,
	TotalSalida            decimal(18,2) NOT NULL DEFAULT 0,
	Stock                  decimal(18,2) NOT NULL DEFAULT 0,
	FechaUltimaMovimiento  date NOT NULL,
	CONSTRAINT PK_KardexStock PRIMARY KEY (IdKardexStock)
);
GO

ALTER TABLE almacen.KardexStock WITH CHECK
	ADD CONSTRAINT UQ_KardexStock_Material_Especialidad_Proyecto
	UNIQUE (IdMaterial, IdEspecialidad, IdProyecto);
ALTER TABLE almacen.KardexStock WITH CHECK
	ADD CONSTRAINT CK_KardexStock_TotalEntrada_NonNeg
	CHECK (TotalEntrada >= 0);
ALTER TABLE almacen.KardexStock WITH CHECK
	ADD CONSTRAINT CK_KardexStock_TotalSalida_NonNeg
	CHECK (TotalSalida >= 0);
ALTER TABLE almacen.KardexStock WITH CHECK
	ADD CONSTRAINT CK_KardexStock_Stock_NonNeg
	CHECK (Stock >= 0);
GO

ALTER TABLE almacen.KardexStock
	ADD CONSTRAINT FK_KardexStock_Material
	FOREIGN KEY (IdMaterial) REFERENCES maestra.Material(IdMaterial);
ALTER TABLE almacen.KardexStock
	ADD CONSTRAINT FK_KardexStock_Especialidad
	FOREIGN KEY (IdEspecialidad) REFERENCES maestra.Especialidad(IdEspecialidad);
ALTER TABLE almacen.KardexStock
	ADD CONSTRAINT FK_KardexStock_Proyecto
	FOREIGN KEY (IdProyecto) REFERENCES maestra.Proyecto(IdProyecto);
GO


-- -----------------------------------------------------------------------------
-- almacen.TVP_KardexSalidaItem (UDDT / Table-Valued Parameter)
--   TVP que recibe el SP almacen.usp_KardexSalida_Registrar con
--   los 1..N items de la salida. Las columnas estan en el mismo orden que
--   las propiedades del DTO C# KardexSalidaItemCreateDto (TvpMapper respeta
--   el orden de declaracion, ver Application/Importacion/Persistence/TvpMapper.cs).
--
--   Tipos nvarchar (no varchar) para que el DataTable de ADO.NET (Unicode
--   por defecto) se mapee sin conversion implicita (convencion ya aplicada
--   en V1_1_2__Maestra_Importacion_Tipos.sql).
-- -----------------------------------------------------------------------------
CREATE TYPE almacen.TVP_KardexSalidaItem AS TABLE (
	IdMaterial   int            NOT NULL,
	Cantidad     decimal(18, 2) NOT NULL,
	Observacion  nvarchar(250)  NULL
);
GO
