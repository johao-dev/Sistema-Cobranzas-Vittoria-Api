/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.7.2
Módulo      : Control Presupuestario
Descripción : Retira las partidas estándar cargadas por V2.5.1.
===============================================================================

V2.5.1 cargó un catálogo base de partidas para todos los proyectos. Esta
migración elimina exclusivamente los códigos de ese catálogo.

No se eliminan datos presupuestales: si alguna de estas partidas ya fue usada
en PresupuestoDetalle, la migración se detiene y revierte por completo. Así se
evita eliminar montos o movimientos que pertenecen al usuario.
===============================================================================
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @CodigosPartidasEstandar TABLE
(
    Codigo VARCHAR(50) NOT NULL PRIMARY KEY
);

INSERT INTO @CodigosPartidasEstandar (Codigo) VALUES
    ('01'), ('01.01'), ('01.02'), ('01.03'), ('01.04'), ('01.05'),
    ('02'), ('02.01'), ('02.02'), ('02.03'), ('02.04'), ('02.05'),
    ('03'), ('03.01'), ('03.02'), ('03.03'), ('03.04'), ('03.05'),
    ('03.06'), ('03.07'), ('03.08'), ('03.09'), ('03.10'), ('03.11'),
    ('03.12'),
    ('04'), ('04.01'), ('04.02'), ('04.03'), ('04.04'),
    ('05'), ('05.01'), ('05.02'), ('05.03'), ('05.04'), ('05.05'),
    ('06'), ('06.01');

-- Las partidas con presupuesto ya representan información operativa; no se
-- eliminan de forma implícita junto con el catálogo estándar.
IF EXISTS
(
    SELECT 1
    FROM ControlPresupuestario.PresupuestoDetalle detalle
    JOIN ControlPresupuestario.CatalogoPartida partida
        ON partida.IdCatalogoPartida = detalle.IdCatalogoPartida
    JOIN @CodigosPartidasEstandar codigos ON codigos.Codigo = partida.Codigo
)
    THROW 51674, 'PARTIDAS_ESTANDAR_EN_USO: existen partidas estándar con montos presupuestados.', 1;

-- Una partida por defecto de material es una referencia auxiliar; se limpia
-- antes de retirar la partida del catálogo.
UPDATE material
SET IdCatalogoPartida = NULL
FROM maestra.Material material
JOIN ControlPresupuestario.CatalogoPartida partida
    ON partida.IdCatalogoPartida = material.IdCatalogoPartida
JOIN @CodigosPartidasEstandar codigos ON codigos.Codigo = partida.Codigo;

-- No se deben borrar partidas estándar que tengan hijas agregadas fuera del
-- catálogo de V2.5.1, pues dichas hijas requieren una revisión manual.
IF EXISTS
(
    SELECT 1
    FROM ControlPresupuestario.CatalogoPartida hija
    JOIN ControlPresupuestario.CatalogoPartida padre
        ON padre.IdCatalogoPartida = hija.IdPartidaPadre
    JOIN @CodigosPartidasEstandar codigosPadre ON codigosPadre.Codigo = padre.Codigo
    LEFT JOIN @CodigosPartidasEstandar codigosHija ON codigosHija.Codigo = hija.Codigo
    WHERE codigosHija.Codigo IS NULL
)
    THROW 51675, 'PARTIDAS_ESTANDAR_CON_HIJAS: existe una partida no estándar dependiente del catálogo estándar.', 1;

-- Se eliminan las hojas antes que sus padres para cumplir la FK jerárquica.
WHILE EXISTS
(
    SELECT 1
    FROM ControlPresupuestario.CatalogoPartida partida
    JOIN @CodigosPartidasEstandar codigos ON codigos.Codigo = partida.Codigo
)
BEGIN
    DELETE partida
    FROM ControlPresupuestario.CatalogoPartida partida
    JOIN @CodigosPartidasEstandar codigos ON codigos.Codigo = partida.Codigo
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM ControlPresupuestario.CatalogoPartida hija
        WHERE hija.IdPartidaPadre = partida.IdCatalogoPartida
    );

    IF @@ROWCOUNT = 0
        THROW 51676, 'PARTIDAS_ESTANDAR_NO_ELIMINABLES: existen dependencias no reconocidas.', 1;
END;

COMMIT TRANSACTION;
GO
