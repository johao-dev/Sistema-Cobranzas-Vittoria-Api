/*
===============================================================================
MIGRACIÓN
-------------------------------------------------------------------------------
Versión     : V2.5.1
Módulo      : Control Presupuestario
Descripción : Catálogo estándar de partidas comunes a todos los proyectos.
===============================================================================

Plantilla base que se presupuesta en cada proyecto: si una partida no aplica se
deja en 0 y si falta alguna se agrega al catálogo (manualmente o por CSV).

Las partidas con sección de gasto se ejecutan por gasto directo desde la pantalla
de esa sección; las de obra (sin sección) se ejecutan por requerimiento.

Reentrante: solo inserta los códigos que no existen. Una hija no se inserta si su
padre ya tiene montos presupuestados (no puede convertirse en agrupadora).
===============================================================================
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Partidas TABLE
(
    Orden INT NOT NULL PRIMARY KEY,
    Codigo VARCHAR(50) NOT NULL UNIQUE,
    Nombre NVARCHAR(200) NOT NULL,
    CodigoTipo VARCHAR(50) NOT NULL,
    CodigoPadre VARCHAR(50) NULL,
    CodigoSeccion VARCHAR(30) NULL
);

-- El orden garantiza que cada padre se procese antes que sus hijas.
INSERT INTO @Partidas (Orden, Codigo, Nombre, CodigoTipo, CodigoPadre, CodigoSeccion) VALUES
    ( 1, '01',    N'Terreno y anteproyecto',                              'INDIRECTOS',      NULL, NULL),
    ( 2, '01.01', N'Compra de terreno',                                   'INDIRECTOS',      '01', 'TERRENO'),
    ( 3, '01.02', N'Alcabala',                                            'INDIRECTOS',      '01', 'TERRENO'),
    ( 4, '01.03', N'Gastos notariales y registrales del terreno',         'INDIRECTOS',      '01', 'TERRENO'),
    ( 5, '01.04', N'Estudios previos (topografía, suelos)',               'INDIRECTOS',      '01', 'TERRENO'),
    ( 6, '01.05', N'Anteproyecto y proyecto (arquitectura e ingenierías)', 'INDIRECTOS',     '01', 'TERRENO'),

    ( 7, '02',    N'Gastos municipales',                                  'INDIRECTOS',      NULL, NULL),
    ( 8, '02.01', N'Licencia de edificación',                             'INDIRECTOS',      '02', 'MUNICIPAL'),
    ( 9, '02.02', N'Independización',                                     'INDIRECTOS',      '02', 'MUNICIPAL'),
    (10, '02.03', N'Declaratoria de fábrica',                             'INDIRECTOS',      '02', 'MUNICIPAL'),
    (11, '02.04', N'Conformidad de obra',                                 'INDIRECTOS',      '02', 'MUNICIPAL'),
    (12, '02.05', N'Instalaciones (conexiones de servicios)',             'INDIRECTOS',      '02', 'MUNICIPAL'),

    (13, '03',    N'Obra',                                                'MATERIALES',      NULL, NULL),
    (14, '03.01', N'Excavaciones y movimiento de tierras',                'SUBCONTRATOS',    '03', NULL),
    (15, '03.02', N'Cemento y agregados',                                 'MATERIALES',      '03', NULL),
    (16, '03.03', N'Fierro / acero corrugado',                            'MATERIALES',      '03', NULL),
    (17, '03.04', N'Ladrillo',                                            'MATERIALES',      '03', NULL),
    (18, '03.05', N'Concreto premezclado',                                'MATERIALES',      '03', NULL),
    (19, '03.06', N'Encofrados y andamios',                               'EQUIPOS',         '03', NULL),
    (20, '03.07', N'Instalaciones sanitarias',                            'MATERIALES',      '03', NULL),
    (21, '03.08', N'Instalaciones eléctricas',                            'MATERIALES',      '03', NULL),
    (22, '03.09', N'Acabados (cerámicos, pintura, carpintería)',          'MATERIALES',      '03', NULL),
    (23, '03.10', N'Ascensores',                                          'SUBCONTRATOS',    '03', NULL),
    (24, '03.11', N'Mano de obra',                                        'MANO_OBRA',       '03', NULL),
    (25, '03.12', N'Supervisión y seguridad de obra',                     'INDIRECTOS',      '03', 'OTROS'),

    (26, '04',    N'Marketing y ventas',                                  'ADMINISTRATIVOS', NULL, NULL),
    (27, '04.01', N'Publicidad',                                          'ADMINISTRATIVOS', '04', 'MARKETING_VENTAS'),
    (28, '04.02', N'Marketing',                                           'ADMINISTRATIVOS', '04', 'MARKETING_VENTAS'),
    (29, '04.03', N'Comisión por ventas',                                 'ADMINISTRATIVOS', '04', 'MARKETING_VENTAS'),
    (30, '04.04', N'Sala de ventas',                                      'ADMINISTRATIVOS', '04', 'MARKETING_VENTAS'),

    (31, '05',    N'Administración',                                      'ADMINISTRATIVOS', NULL, NULL),
    (32, '05.01', N'Alquiler de oficina',                                 'ADMINISTRATIVOS', '05', 'ADMINISTRATIVO'),
    (33, '05.02', N'Servicios básicos',                                   'ADMINISTRATIVOS', '05', 'ADMINISTRATIVO'),
    (34, '05.03', N'Planilla administrativa',                             'ADMINISTRATIVOS', '05', 'ADMINISTRATIVO'),
    (35, '05.04', N'Honorarios profesionales',                            'ADMINISTRATIVOS', '05', 'ADMINISTRATIVO'),
    (36, '05.05', N'Gastos bancarios',                                    'ADMINISTRATIVOS', '05', 'ADMINISTRATIVO'),

    (37, '06',    N'Otros',                                               'INDIRECTOS',      NULL, NULL),
    (38, '06.01', N'Otros gastos',                                        'INDIRECTOS',      '06', 'OTROS');

DECLARE @Orden INT = 1, @MaxOrden INT = (SELECT MAX(Orden) FROM @Partidas);
DECLARE @Codigo VARCHAR(50), @Nombre NVARCHAR(200), @CodigoTipo VARCHAR(50),
    @CodigoPadre VARCHAR(50), @CodigoSeccion VARCHAR(30);
DECLARE @IdTipo INT, @IdSeccion INT, @IdPadre INT, @NivelPadre INT;

WHILE @Orden <= @MaxOrden
BEGIN
    SELECT @Codigo = Codigo, @Nombre = Nombre, @CodigoTipo = CodigoTipo,
        @CodigoPadre = CodigoPadre, @CodigoSeccion = CodigoSeccion
    FROM @Partidas WHERE Orden = @Orden;

    IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida WHERE Codigo = @Codigo)
    BEGIN
        SET @IdTipo = (SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = @CodigoTipo);
        SET @IdSeccion = (SELECT IdSeccionGasto FROM ControlPresupuestario.SeccionGasto WHERE Codigo = @CodigoSeccion);
        SET @IdPadre = NULL;
        SET @NivelPadre = 0;

        IF @CodigoPadre IS NOT NULL
            SELECT @IdPadre = IdCatalogoPartida, @NivelPadre = Nivel
            FROM ControlPresupuestario.CatalogoPartida WHERE Codigo = @CodigoPadre;

        IF @IdTipo IS NULL
            PRINT CONCAT('Partida ', @Codigo, ' omitida: tipo ', @CodigoTipo, ' inexistente.');
        ELSE IF @CodigoPadre IS NOT NULL AND @IdPadre IS NULL
            PRINT CONCAT('Partida ', @Codigo, ' omitida: padre ', @CodigoPadre, ' inexistente.');
        ELSE IF @IdPadre IS NOT NULL AND EXISTS (
            SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle WHERE IdCatalogoPartida = @IdPadre)
            PRINT CONCAT('Partida ', @Codigo, ' omitida: el padre ', @CodigoPadre, ' ya tiene montos presupuestados.');
        ELSE
        BEGIN
            -- Una partida que pasa a tener hijas deja de ser hoja y pierde su sección.
            IF @IdPadre IS NOT NULL
                UPDATE ControlPresupuestario.CatalogoPartida
                SET IdSeccionGasto = NULL
                WHERE IdCatalogoPartida = @IdPadre AND IdSeccionGasto IS NOT NULL;

            INSERT INTO ControlPresupuestario.CatalogoPartida
                (Codigo, Nombre, IdTipoPartida, IdPartidaPadre, Nivel, IdSeccionGasto)
            VALUES (@Codigo, @Nombre, @IdTipo, @IdPadre, @NivelPadre + 1, @IdSeccion);
        END;
    END;

    SET @Orden = @Orden + 1;
END;

COMMIT TRANSACTION;
