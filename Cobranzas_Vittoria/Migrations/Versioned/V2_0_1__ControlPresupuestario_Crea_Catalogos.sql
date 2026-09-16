/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.1
Módulo      : Control Presupuestario
Descripción : Creación de catálogos base para el módulo de Control Presupuestario.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
1. ESTADO PRESUPUESTO
-------------------------------------------------------------------------------
Define los estados que puede asumir una versión de presupuesto.

BORRADOR  -> Puede ser modificada.
APROBADO  -> Versión aprobada y vigente.
HISTORICO -> Versión aprobada que posteriormente fue reemplazada.
ANULADO   -> Versión descartada y sin efecto presupuestario.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.EstadoPresupuesto
(
    IdEstadoPresupuesto INT IDENTITY(1,1) NOT NULL,
    Codigo VARCHAR(30) NOT NULL,
    Nombre NVARCHAR(50) NOT NULL,
    Descripcion NVARCHAR(255) NULL,
    Activo BIT NOT NULL
    
    CONSTRAINT DF_EstadoPresupuesto_Activo DEFAULT (1),
    CONSTRAINT PK_EstadoPresupuesto PRIMARY KEY CLUSTERED (IdEstadoPresupuesto),
    CONSTRAINT UQ_EstadoPresupuesto_Codigo UNIQUE (Codigo)
);
GO


/*
===============================================================================
2. TIPO CENTRO DE COSTO
-------------------------------------------------------------------------------
Clasifica los centros de costo de la organización.

Ejemplos:
- Proyecto
- Administración
- Marketing
- Ventas
- Gerencia

El catálogo permite incorporar nuevos tipos sin modificar la estructura
del módulo.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.TipoCentroCosto
(
    IdTipoCentroCosto INT IDENTITY(1,1) NOT NULL,
    Codigo VARCHAR(30) NOT NULL,
    Nombre NVARCHAR(50) NOT NULL,
    Descripcion NVARCHAR(255) NULL,
    Activo BIT NOT NULL

    CONSTRAINT DF_TipoCentroCosto_Activo DEFAULT (1),
    CONSTRAINT PK_TipoCentroCosto PRIMARY KEY CLUSTERED (IdTipoCentroCosto),
    CONSTRAINT UQ_TipoCentroCosto_Codigo UNIQUE (Codigo)
);
GO


/*
===============================================================================
3. TIPO PARTIDA
-------------------------------------------------------------------------------
Clasificación general de las partidas presupuestales.

No representa la jerarquía de partidas.
La jerarquía será responsabilidad de CatalogoPartida.

Ejemplos:
- Materiales
- Mano de obra
- Equipos
- Subcontratos
- Indirectos
- Administrativos
===============================================================================
*/
CREATE TABLE ControlPresupuestario.TipoPartida
(
    IdTipoPartida INT IDENTITY(1,1) NOT NULL,
    Codigo VARCHAR(30) NOT NULL,
    Nombre NVARCHAR(50) NOT NULL,
    Descripcion NVARCHAR(255) NULL,
    Activo BIT NOT NULL

    CONSTRAINT DF_TipoPartida_Activo DEFAULT (1),
    CONSTRAINT PK_TipoPartida PRIMARY KEY CLUSTERED (IdTipoPartida),
    CONSTRAINT UQ_TipoPartida_Codigo UNIQUE (Codigo)
);
GO


/*
===============================================================================
4. TIPO MOVIMIENTO PRESUPUESTAL
-------------------------------------------------------------------------------
Define la naturaleza de un movimiento dentro del ledger presupuestario.

COMPROMISO -> Reserva presupuesto, normalmente originada por una OC aprobada.
LIBERACION -> Libera total o parcialmente un compromiso anterior.
EJECUCION  -> Registra consumo efectivo del presupuesto.
AJUSTE     -> Incrementa o disminuye COMPROMISO/EJECUCION según Afectacion
              y Direccion del movimiento.

Los movimientos presupuestales serán inmutables. Una operación ya registrada
no debe corregirse mediante UPDATE/DELETE, sino mediante un nuevo movimiento
compensatorio.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.TipoMovimientoPresupuestal
(
    IdTipoMovimientoPresupuestal INT IDENTITY(1,1) NOT NULL,
    Codigo VARCHAR(30) NOT NULL,
    Nombre NVARCHAR(100) NOT NULL,
    Descripcion NVARCHAR(255) NULL,
    Activo BIT NOT NULL

    CONSTRAINT DF_TipoMovimientoPresupuestal_Activo DEFAULT (1),
    CONSTRAINT PK_TipoMovimientoPresupuestal PRIMARY KEY CLUSTERED (IdTipoMovimientoPresupuestal),
    CONSTRAINT UQ_TipoMovimientoPresupuestal_Codigo UNIQUE (Codigo)
);
GO


/*
===============================================================================
5. MONEDA
-------------------------------------------------------------------------------
La moneda pertenece al Presupuesto y se conserva en todas sus versiones.
Los movimientos utilizan esa misma moneda; no se realizan conversiones.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.Moneda
(
    IdMoneda INT IDENTITY(1,1) NOT NULL,
    Codigo VARCHAR(3) NOT NULL,
    Nombre NVARCHAR(50) NOT NULL,
    Simbolo NVARCHAR(10) NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Moneda_Activo DEFAULT (1),

    CONSTRAINT PK_Moneda PRIMARY KEY CLUSTERED (IdMoneda),
    CONSTRAINT UQ_Moneda_Codigo UNIQUE (Codigo)
);
GO


/*
===============================================================================
SEED - ESTADOS DE PRESUPUESTO
===============================================================================
*/
INSERT INTO ControlPresupuestario.EstadoPresupuesto
(
    Codigo,
    Nombre,
    Descripcion
)
VALUES
(
    'BORRADOR',
    N'Borrador',
    N'Versión de presupuesto en elaboración y susceptible de modificaciones.'
),
(
    'APROBADO',
    N'Aprobado',
    N'Versión de presupuesto aprobada y vigente.'
),
(
    'HISTORICO',
    N'Histórico',
    N'Versión de presupuesto que fue reemplazada por una versión posterior.'
),
(
    'ANULADO',
    N'Anulado',
    N'Versión descartada y sin efecto presupuestario.'
);
GO


/*
===============================================================================
SEED - TIPOS DE CENTRO DE COSTO
===============================================================================
*/
INSERT INTO ControlPresupuestario.TipoCentroCosto
(
    Codigo,
    Nombre,
    Descripcion
)
VALUES
(
    'PROYECTO',
    N'Proyecto',
    N'Centro de costo asociado a un proyecto inmobiliario.'
),
(
    'ADMINISTRACION',
    N'Administración',
    N'Centro de costo correspondiente a gastos administrativos generales.'
),
(
    'MARKETING',
    N'Marketing',
    N'Centro de costo correspondiente a actividades de marketing.'
),
(
    'VENTAS',
    N'Ventas',
    N'Centro de costo correspondiente a actividades comerciales y de ventas.'
),
(
    'GERENCIA',
    N'Gerencia',
    N'Centro de costo correspondiente a gastos propios de gerencia.'
),
(
    'LEGAL',
    N'Legal',
    N'Centro de costo correspondiente a actividades y servicios legales.'
),
(
    'FINANZAS',
    N'Finanzas',
    N'Centro de costo correspondiente a actividades financieras.'
);
GO


/*
===============================================================================
SEED - TIPOS DE PARTIDA
===============================================================================
*/
INSERT INTO ControlPresupuestario.TipoPartida
(
    Codigo,
    Nombre,
    Descripcion
)
VALUES
(
    'MATERIALES',
    N'Materiales',
    N'Partidas correspondientes a materiales e insumos.'
),
(
    'MANO_OBRA',
    N'Mano de obra',
    N'Partidas correspondientes al costo de mano de obra.'
),
(
    'EQUIPOS',
    N'Equipos',
    N'Partidas correspondientes a equipos, maquinaria y herramientas.'
),
(
    'SUBCONTRATOS',
    N'Subcontratos',
    N'Partidas correspondientes a trabajos ejecutados por terceros.'
),
(
    'INDIRECTOS',
    N'Costos indirectos',
    N'Partidas correspondientes a costos indirectos asociados a la operación.'
),
(
    'ADMINISTRATIVOS',
    N'Administrativos',
    N'Partidas correspondientes a gastos administrativos.'
);
GO


/*
===============================================================================
SEED - TIPOS DE MOVIMIENTO PRESUPUESTAL
===============================================================================
*/
INSERT INTO ControlPresupuestario.TipoMovimientoPresupuestal
(
    Codigo,
    Nombre,
    Descripcion
)
VALUES
(
    'COMPROMISO',
    N'Compromiso',
    N'Reserva de presupuesto generada por una obligación asumida.'
),
(
    'LIBERACION',
    N'Liberación',
    N'Liberación total o parcial de presupuesto previamente comprometido.'
),
(
    'EJECUCION',
    N'Ejecución',
    N'Consumo efectivo de presupuesto.'
),
(
    'AJUSTE',
    N'Ajuste',
    N'Corrección de compromiso o ejecución mediante afectación y dirección explícitas.'
);
GO


/*
===============================================================================
SEED - MONEDAS
===============================================================================
*/
INSERT INTO ControlPresupuestario.Moneda (Codigo, Nombre, Simbolo)
VALUES
    ('PEN', N'Sol peruano', N'S/'),
    ('USD', N'Dólar estadounidense', N'US' + NCHAR(36));
GO
