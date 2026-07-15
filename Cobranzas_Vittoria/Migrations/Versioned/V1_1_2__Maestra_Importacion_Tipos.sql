-- =============================================================================
-- Migracion versionada: V1_1_2__Maestra_Importacion_Tipos.sql
--
-- Crea los User-Defined Table Types (TVPs) necesarios para los SPs de carga
-- masiva de las entidades de mantenimiento del esquema `maestra`.
--
-- Modulos cubiertos:
--   - UnidadMedida                     (TVP_UnidadMedida)
--   - Especialidad                     (TVP_Especialidad)
--   - Material                         (TVP_Material)
--   - Proveedor                        (TVP_Proveedor)
--   - ProveedorGastoAdministrativo     (TVP_ProveedorGastoAdministrativo)
--   - ProveedorTerreno                 (TVP_ProveedorTerreno)
--   - CategoriaGasto                   (TVP_CategoriaGasto)
--
-- Convenciones:
--   - Nombre: TVP_<Entidad> (sigue el patron del TVP existente en V1_0_7)
--   - Columnas: las del DTO de importacion (mismas que la entidad destino)
--               + _Fila INT NOT NULL al final (numero de fila del archivo,
--               para reportar errores con contexto). El orden de columnas
--               DEBE coincidir con el orden de las propiedades publicas del DTO
--               (TvpMapper usa reflexion y respeta el orden de declaracion).
--   - Las columnas usan nvarchar (no varchar) para que el DataTable de ADO.NET
--     (que es Unicode por defecto) se mapee sin conversion implicita.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- UnidadMedida
--   Campos requeridos: Codigo, Nombre.
--   Tabla destino: maestra.UnidadMedida (Codigo UNIQUE, no puede repetirse).
-- -----------------------------------------------------------------------------
CREATE TYPE maestra.TVP_UnidadMedida AS TABLE (
    Codigo        NVARCHAR(20)  NOT NULL,
    Nombre        NVARCHAR(100) NOT NULL,
    Activo        BIT           NOT NULL,
    _Fila         INT           NOT NULL
);
GO


-- -----------------------------------------------------------------------------
-- Especialidad
--   Campos requeridos: Nombre.
--   No tiene Codigo ni UNIQUE explicita; el SP valida duplicados intra-archivo
--   para evitar dos filas con el mismo Nombre.
-- -----------------------------------------------------------------------------
CREATE TYPE maestra.TVP_Especialidad AS TABLE (
    Nombre        NVARCHAR(100) NOT NULL,
    Descripcion   NVARCHAR(250) NULL,
    Activo        BIT           NOT NULL,
    _Fila         INT           NOT NULL
);
GO


-- -----------------------------------------------------------------------------
-- Material
--   Campos requeridos: IdEspecialidad (FK a maestra.Especialidad),
--                       Descripcion, UnidadMedida.
--   Campos opcionales: Codigo (autogenerado por SP si NULL), IdUnidadMedida
--                       (FK a maestra.UnidadMedida), CodigoProveedor, StockMinimo.
-- -----------------------------------------------------------------------------
CREATE TYPE maestra.TVP_Material AS TABLE (
    IdEspecialidad    INT            NOT NULL,
    Codigo            NVARCHAR(50)   NULL,
    Descripcion       NVARCHAR(200)  NOT NULL,
    UnidadMedida      NVARCHAR(30)   NOT NULL,
    StockMinimo       DECIMAL(18, 2) NOT NULL,
    Activo            BIT            NOT NULL,
    IdUnidadMedida    INT            NULL,
    CodigoProveedor   VARCHAR(100)   NULL,
    _Fila             INT            NOT NULL
);
GO


-- -----------------------------------------------------------------------------
-- Proveedor
--   Campos requeridos: RazonSocial, Ruc.
--   Ruc es UNIQUE (validado por el SP para evitar duplicados en BD).
-- -----------------------------------------------------------------------------
CREATE TYPE maestra.TVP_Proveedor AS TABLE (
    RazonSocial              NVARCHAR(200) NOT NULL,
    Ruc                      NVARCHAR(20)  NOT NULL,
    Contacto                 NVARCHAR(150) NULL,
    Telefono                 NVARCHAR(30)  NULL,
    Correo                   NVARCHAR(150) NULL,
    Direccion                NVARCHAR(250) NULL,
    Banco                    NVARCHAR(50)  NULL,
    CuentaCorriente          NVARCHAR(50)  NULL,
    CCI                      NVARCHAR(50)  NULL,
    CuentaDetraccion         NVARCHAR(50)  NULL,
    DescripcionServicio      NVARCHAR(250) NULL,
    Observacion              NVARCHAR(250) NULL,
    TrabajamosConProveedor   NVARCHAR(10)  NULL,
    Activo                   BIT           NOT NULL,
    _Fila                    INT           NOT NULL
);
GO


-- -----------------------------------------------------------------------------
-- ProveedorGastoAdministrativo
--   Campos requeridos: RazonSocial (UNIQUE).
--   Campo opcional: IdCategoriaGasto (FK a maestra.CategoriaGasto).
--   Ruc es UNIQUE solo cuando es NOT NULL (indice filtrado); el SP valida que,
--   si viene con un valor, no exista en BD.
-- -----------------------------------------------------------------------------
CREATE TYPE maestra.TVP_ProveedorGastoAdministrativo AS TABLE (
    RazonSocial       NVARCHAR(200) NOT NULL,
    Ruc               NVARCHAR(20)  NULL,
    Contacto          NVARCHAR(120) NULL,
    Telefono          NVARCHAR(50)  NULL,
    Correo            NVARCHAR(150) NULL,
    Activo            BIT           NOT NULL,
    IdCategoriaGasto  INT           NULL,
    _Fila             INT           NOT NULL
);
GO


-- -----------------------------------------------------------------------------
-- ProveedorTerreno
--   Campos requeridos: RazonSocial (UNIQUE, tiene indice nonclustered).
-- -----------------------------------------------------------------------------
CREATE TYPE maestra.TVP_ProveedorTerreno AS TABLE (
    RazonSocial   NVARCHAR(250) NOT NULL,
    Ruc           NVARCHAR(20)  NULL,
    Contacto      NVARCHAR(150) NULL,
    Telefono      NVARCHAR(50)  NULL,
    Correo        NVARCHAR(150) NULL,
    Activo        BIT           NOT NULL,
    _Fila         INT           NOT NULL
);
GO


-- -----------------------------------------------------------------------------
-- CategoriaGasto
--   Campos requeridos: Nombre.
--   El SP valida duplicados intra-archivo (la tabla no tiene UNIQUE explicita
--   pero Concepto/Nombre se mantiene unico por convencion de negocio).
-- -----------------------------------------------------------------------------
CREATE TYPE maestra.TVP_CategoriaGasto AS TABLE (
    Nombre   NVARCHAR(150) NOT NULL,
    Activo   BIT           NOT NULL,
    _Fila    INT           NOT NULL
);
GO
