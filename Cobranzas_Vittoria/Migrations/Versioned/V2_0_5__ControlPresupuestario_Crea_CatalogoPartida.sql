/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.0.5
Módulo      : Control Presupuestario
Descripción : Creación del catálogo jerárquico de partidas presupuestales.
Autor       : Johao Bravo
===============================================================================
*/


/*
===============================================================================
CATÁLOGO DE PARTIDAS
-------------------------------------------------------------------------------
Representa el catálogo maestro de conceptos presupuestales.

La estructura es jerárquica mediante IdPartidaPadre.

Ejemplo:

    01          Obras preliminares
    ├── 01.01   Limpieza
    └── 01.02   Trazo

    02          Estructuras
    ├── 02.01   Concreto
    │   ├── 02.01.01   Concreto en cimentación
    │   └── 02.01.02   Concreto en columnas
    └── 02.02   Acero

IdTipoPartida clasifica la naturaleza económica de la partida:

    MATERIALES
    MANO_OBRA
    EQUIPOS
    SUBCONTRATOS
    INDIRECTOS
    ADMINISTRATIVOS

IMPORTANTE:

CatalogoPartida define "qué es" una partida.

NO almacena:
    - Presupuesto.
    - Versión.
    - Monto presupuestado.
    - Monto comprometido.
    - Monto ejecutado.

Esos conceptos pertenecen a otras entidades del módulo.
===============================================================================
*/
CREATE TABLE ControlPresupuestario.CatalogoPartida
(
    IdCatalogoPartida INT IDENTITY(1,1) NOT NULL,
    Codigo VARCHAR(50) NOT NULL,
    Nombre NVARCHAR(200) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    IdPartidaPadre INT NULL,
    IdTipoPartida INT NOT NULL,
    Nivel INT NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_CatalogoPartida_Activo DEFAULT (1),
    FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_CatalogoPartida_FechaCreacion DEFAULT (SYSDATETIME()),
    FechaActualizacion DATETIME2(0) NULL,

    CONSTRAINT PK_CatalogoPartida PRIMARY KEY CLUSTERED(IdCatalogoPartida),
    CONSTRAINT UQ_CatalogoPartida_Codigo UNIQUE(Codigo),
    CONSTRAINT FK_CatalogoPartida_PartidaPadre FOREIGN KEY(IdPartidaPadre)
        REFERENCES ControlPresupuestario.CatalogoPartida
        (IdCatalogoPartida),
    CONSTRAINT FK_CatalogoPartida_TipoPartida FOREIGN KEY(IdTipoPartida)
        REFERENCES ControlPresupuestario.TipoPartida(IdTipoPartida),
    CONSTRAINT CK_CatalogoPartida_Nivel CHECK(Nivel > 0),
    CONSTRAINT CK_CatalogoPartida_NoAutoReferencia CHECK
        (IdPartidaPadre IS NULL OR IdPartidaPadre <> IdCatalogoPartida)
);
GO


/*
===============================================================================
ÍNDICE - PARTIDAS HIJAS
-------------------------------------------------------------------------------
Optimiza la navegación del árbol.

Ejemplo:

    SELECT ...
    FROM ControlPresupuestario.CatalogoPartida
    WHERE IdPartidaPadre = @IdPadre;

Será utilizado frecuentemente por la API para obtener los hijos de una partida.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_CatalogoPartida_IdPartidaPadre
ON ControlPresupuestario.CatalogoPartida
(
    IdPartidaPadre
)
INCLUDE
(
    Codigo,
    Nombre,
    IdTipoPartida,
    Nivel,
    Activo
);
GO


/*
===============================================================================
ÍNDICE - TIPO DE PARTIDA
-------------------------------------------------------------------------------
Facilita consultas y reportes por naturaleza de partida.

Ejemplos:

    - Todas las partidas de materiales.
    - Todas las partidas administrativas.
    - Todas las partidas correspondientes a subcontratos.
===============================================================================
*/
CREATE NONCLUSTERED INDEX IX_CatalogoPartida_IdTipoPartida
ON ControlPresupuestario.CatalogoPartida
(
    IdTipoPartida
)
INCLUDE
(
    Codigo,
    Nombre,
    IdPartidaPadre,
    Nivel,
    Activo
);
GO