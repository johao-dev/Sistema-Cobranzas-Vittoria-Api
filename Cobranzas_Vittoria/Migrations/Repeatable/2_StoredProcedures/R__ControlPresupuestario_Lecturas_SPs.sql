/* Lecturas de entidades reales. Un solo result set; Obtener ausente devuelve cero filas.
Sin NOLOCK, sin sintetizar partidas ni reasignar el ledger.
Reporting financiero: consumir las vistas existentes directamente mediante Dapper.
*/

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_Presupuesto_Listar
    @Activo BIT = NULL,
    @IdCentroCosto INT = NULL,
    @IdMoneda INT = NULL,
    @Busqueda NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Busqueda = NULLIF(LTRIM(RTRIM(@Busqueda)), N'');
    SELECT p.IdPresupuesto, p.Codigo, p.Nombre, p.Descripcion, p.IdCentroCosto,
        cc.Codigo AS CodigoCentroCosto, cc.Nombre AS NombreCentroCosto,
        p.IdMoneda, m.Codigo AS CodigoMoneda, m.Nombre AS NombreMoneda, m.Simbolo AS SimboloMoneda,
        p.FechaInicio, p.FechaFin, p.Activo, p.FechaCreacion, p.FechaModificacion,
        CASE WHEN versiones.CantidadAprobadas = 1 THEN versiones.IdAprobada END AS IdPresupuestoVersionAprobada,
        CASE WHEN versiones.CantidadBorradores = 1 THEN versiones.IdBorrador END AS IdPresupuestoVersionBorrador,
        versiones.CantidadAprobadas, versiones.CantidadBorradores,
        CONVERT(VARCHAR(30), CASE
            WHEN versiones.CantidadAprobadas > 1 OR versiones.CantidadBorradores > 1 THEN 'INCONSISTENTE'
            WHEN versiones.CantidadAprobadas = 1 AND versiones.CantidadBorradores = 1 THEN 'EN_REVISION'
            WHEN versiones.CantidadAprobadas = 1 THEN 'APROBADO'
            WHEN versiones.CantidadBorradores = 1 THEN 'EN_ELABORACION'
            ELSE 'SIN_VERSION_VIGENTE' END) AS EstadoElaboracion
    FROM ControlPresupuestario.Presupuesto p
    JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
    JOIN maestra.Moneda m ON m.IdMoneda = p.IdMoneda
    OUTER APPLY
    (
        SELECT COUNT(CASE WHEN e.Codigo = 'APROBADO' THEN 1 END) AS CantidadAprobadas,
            COUNT(CASE WHEN e.Codigo = 'BORRADOR' THEN 1 END) AS CantidadBorradores,
            MAX(CASE WHEN e.Codigo = 'APROBADO' THEN v.IdPresupuestoVersion END) AS IdAprobada,
            MAX(CASE WHEN e.Codigo = 'BORRADOR' THEN v.IdPresupuestoVersion END) AS IdBorrador
        FROM ControlPresupuestario.PresupuestoVersion v
        JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
        WHERE v.IdPresupuesto = p.IdPresupuesto
    ) versiones
    WHERE (@Activo IS NULL OR p.Activo = @Activo)
        AND (@IdCentroCosto IS NULL OR p.IdCentroCosto = @IdCentroCosto)
        AND (@IdMoneda IS NULL OR p.IdMoneda = @IdMoneda)
        AND (@Busqueda IS NULL OR CHARINDEX(@Busqueda, p.Codigo) > 0 OR CHARINDEX(@Busqueda, p.Nombre) > 0)
    ORDER BY p.Codigo, p.IdPresupuesto;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_Presupuesto_Obtener
    @IdPresupuesto INT
AS
BEGIN
    SET NOCOUNT ON;
        IF @IdPresupuesto IS NULL OR @IdPresupuesto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuesto positivo.', 1;
    SELECT p.IdPresupuesto, p.Codigo, p.Nombre, p.Descripcion, p.IdCentroCosto,
        cc.Codigo AS CodigoCentroCosto, cc.Nombre AS NombreCentroCosto,
        p.IdMoneda, m.Codigo AS CodigoMoneda, m.Nombre AS NombreMoneda, m.Simbolo AS SimboloMoneda,
        p.FechaInicio, p.FechaFin, p.Activo, p.FechaCreacion, p.FechaModificacion,
        CASE WHEN versiones.CantidadAprobadas = 1 THEN versiones.IdAprobada END AS IdPresupuestoVersionAprobada,
        CASE WHEN versiones.CantidadBorradores = 1 THEN versiones.IdBorrador END AS IdPresupuestoVersionBorrador,
        versiones.CantidadAprobadas, versiones.CantidadBorradores,
        CONVERT(VARCHAR(30), CASE
            WHEN versiones.CantidadAprobadas > 1 OR versiones.CantidadBorradores > 1 THEN 'INCONSISTENTE'
            WHEN versiones.CantidadAprobadas = 1 AND versiones.CantidadBorradores = 1 THEN 'EN_REVISION'
            WHEN versiones.CantidadAprobadas = 1 THEN 'APROBADO'
            WHEN versiones.CantidadBorradores = 1 THEN 'EN_ELABORACION'
            ELSE 'SIN_VERSION_VIGENTE' END) AS EstadoElaboracion
    FROM ControlPresupuestario.Presupuesto p
    JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
    JOIN maestra.Moneda m ON m.IdMoneda = p.IdMoneda
    OUTER APPLY
    (
        SELECT COUNT(CASE WHEN e.Codigo = 'APROBADO' THEN 1 END) AS CantidadAprobadas,
            COUNT(CASE WHEN e.Codigo = 'BORRADOR' THEN 1 END) AS CantidadBorradores,
            MAX(CASE WHEN e.Codigo = 'APROBADO' THEN v.IdPresupuestoVersion END) AS IdAprobada,
            MAX(CASE WHEN e.Codigo = 'BORRADOR' THEN v.IdPresupuestoVersion END) AS IdBorrador
        FROM ControlPresupuestario.PresupuestoVersion v
        JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
        WHERE v.IdPresupuesto = p.IdPresupuesto
    ) versiones
    WHERE p.IdPresupuesto = @IdPresupuesto;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoVersion_Listar
    @IdPresupuesto INT
AS
BEGIN
    SET NOCOUNT ON;
        IF @IdPresupuesto IS NULL OR @IdPresupuesto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuesto positivo.', 1;
    SELECT v.IdPresupuestoVersion, v.IdPresupuesto, v.NumeroVersion,
        v.IdEstadoPresupuesto, e.Codigo AS EstadoPresupuesto, e.Nombre AS NombreEstadoPresupuesto,
        v.Descripcion, v.MotivoCambio, v.FechaCreacion, v.FechaAprobacion,
        v.UsuarioCreacion, v.UsuarioAprobacion,
        p.Codigo AS CodigoPresupuesto, p.Nombre AS NombrePresupuesto,
        p.IdCentroCosto, p.IdMoneda, m.Codigo AS CodigoMoneda, m.Simbolo AS SimboloMoneda
    FROM ControlPresupuestario.PresupuestoVersion v
    JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
    JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = v.IdPresupuesto
    JOIN maestra.Moneda m ON m.IdMoneda = p.IdMoneda
    WHERE v.IdPresupuesto = @IdPresupuesto ORDER BY v.NumeroVersion DESC;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoVersion_Obtener
    @IdPresupuestoVersion INT
AS
BEGIN
    SET NOCOUNT ON;
        IF @IdPresupuestoVersion IS NULL OR @IdPresupuestoVersion <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoVersion positivo.', 1;
    SELECT v.IdPresupuestoVersion, v.IdPresupuesto, v.NumeroVersion,
        v.IdEstadoPresupuesto, e.Codigo AS EstadoPresupuesto, e.Nombre AS NombreEstadoPresupuesto,
        v.Descripcion, v.MotivoCambio, v.FechaCreacion, v.FechaAprobacion,
        v.UsuarioCreacion, v.UsuarioAprobacion,
        p.Codigo AS CodigoPresupuesto, p.Nombre AS NombrePresupuesto,
        p.IdCentroCosto, p.IdMoneda, m.Codigo AS CodigoMoneda, m.Simbolo AS SimboloMoneda
    FROM ControlPresupuestario.PresupuestoVersion v
    JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
    JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = v.IdPresupuesto
    JOIN maestra.Moneda m ON m.IdMoneda = p.IdMoneda
    WHERE v.IdPresupuestoVersion = @IdPresupuestoVersion;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_PresupuestoDetalle_ListarPorVersion
    @IdPresupuestoVersion INT
AS
BEGIN
    SET NOCOUNT ON;
        IF @IdPresupuestoVersion IS NULL OR @IdPresupuestoVersion <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoVersion positivo.', 1;
    SELECT d.IdPresupuestoDetalle, d.IdPresupuestoVersion, d.IdCatalogoPartida,
        c.Codigo AS CodigoPartida, c.Nombre AS NombrePartida, c.IdPartidaPadre, c.Nivel,
        c.IdTipoPartida, t.Codigo AS CodigoTipoPartida, t.Nombre AS NombreTipoPartida,
        c.Activo AS PartidaActiva, CONVERT(BIT, CASE WHEN EXISTS
            (SELECT 1 FROM ControlPresupuestario.CatalogoPartida h WHERE h.IdPartidaPadre = c.IdCatalogoPartida)
            THEN 0 ELSE 1 END) AS EsHoja,
        d.MontoPresupuestado, d.Observacion, d.FechaCreacion, d.FechaModificacion
    FROM ControlPresupuestario.PresupuestoDetalle d
    JOIN ControlPresupuestario.CatalogoPartida c ON c.IdCatalogoPartida = d.IdCatalogoPartida
    JOIN ControlPresupuestario.TipoPartida t ON t.IdTipoPartida = c.IdTipoPartida
    WHERE d.IdPresupuestoVersion = @IdPresupuestoVersion
    ORDER BY c.Codigo, d.IdPresupuestoDetalle;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CentroCosto_Listar
    @Activo BIT = NULL,
    @IdTipoCentroCosto INT = NULL,
    @Busqueda NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Busqueda = NULLIF(LTRIM(RTRIM(@Busqueda)), N'');
    SELECT x.IdCentroCosto, x.Codigo, x.Nombre, x.Descripcion,
        x.IdTipoCentroCosto, t.Codigo AS CodigoTipoCentroCosto, t.Nombre AS NombreTipoCentroCosto,
        x.Activo, x.FechaCreacion, x.FechaModificacion
    FROM ControlPresupuestario.CentroCosto x
    JOIN ControlPresupuestario.TipoCentroCosto t ON t.IdTipoCentroCosto = x.IdTipoCentroCosto
    WHERE (@Activo IS NULL OR x.Activo = @Activo)
        AND (@IdTipoCentroCosto IS NULL OR x.IdTipoCentroCosto = @IdTipoCentroCosto)
        AND (@Busqueda IS NULL OR CHARINDEX(@Busqueda, x.Codigo) > 0 OR CHARINDEX(@Busqueda, x.Nombre) > 0)
    ORDER BY x.Codigo, x.IdCentroCosto;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CentroCosto_Obtener
    @IdCentroCosto INT
AS
BEGIN
    SET NOCOUNT ON;
        IF @IdCentroCosto IS NULL OR @IdCentroCosto <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdCentroCosto positivo.', 1;
    SELECT x.IdCentroCosto, x.Codigo, x.Nombre, x.Descripcion,
        x.IdTipoCentroCosto, t.Codigo AS CodigoTipoCentroCosto, t.Nombre AS NombreTipoCentroCosto,
        x.Activo, x.FechaCreacion, x.FechaModificacion
    FROM ControlPresupuestario.CentroCosto x
    JOIN ControlPresupuestario.TipoCentroCosto t ON t.IdTipoCentroCosto = x.IdTipoCentroCosto
    WHERE x.IdCentroCosto = @IdCentroCosto;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CatalogoPartida_Listar
    @Activo BIT = NULL,
    @IdTipoPartida INT = NULL,
    @IdPartidaPadre INT = NULL,
    @SoloRaices BIT = 0,
    @EsHoja BIT = NULL,
    @Busqueda NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Busqueda = NULLIF(LTRIM(RTRIM(@Busqueda)), N'');
    IF @SoloRaices = 1 AND @IdPartidaPadre IS NOT NULL
        THROW 51217, 'JERARQUIA_INVALIDA: no combinar SoloRaices e IdPartidaPadre.', 1;
    SELECT x.IdCatalogoPartida, x.Codigo, x.Nombre, x.Descripcion,
        x.IdPartidaPadre, padre.Codigo AS CodigoPartidaPadre, padre.Nombre AS NombrePartidaPadre,
        x.Nivel, x.IdTipoPartida, t.Codigo AS CodigoTipoPartida, t.Nombre AS NombreTipoPartida,
        x.Activo, hoja.EsHoja, x.FechaCreacion, x.FechaActualizacion
    FROM ControlPresupuestario.CatalogoPartida x
    LEFT JOIN ControlPresupuestario.CatalogoPartida padre ON padre.IdCatalogoPartida = x.IdPartidaPadre
    JOIN ControlPresupuestario.TipoPartida t ON t.IdTipoPartida = x.IdTipoPartida
    CROSS APPLY (SELECT CONVERT(BIT, CASE WHEN EXISTS
        (SELECT 1 FROM ControlPresupuestario.CatalogoPartida h WHERE h.IdPartidaPadre = x.IdCatalogoPartida)
        THEN 0 ELSE 1 END) AS EsHoja) hoja
    WHERE (@Activo IS NULL OR x.Activo = @Activo)
        AND (@IdTipoPartida IS NULL OR x.IdTipoPartida = @IdTipoPartida)
        AND (@IdPartidaPadre IS NULL OR x.IdPartidaPadre = @IdPartidaPadre)
        AND (ISNULL(@SoloRaices, 0) = 0 OR x.IdPartidaPadre IS NULL)
        AND (@EsHoja IS NULL OR hoja.EsHoja = @EsHoja)
        AND (@Busqueda IS NULL OR CHARINDEX(@Busqueda, x.Codigo) > 0 OR CHARINDEX(@Busqueda, x.Nombre) > 0)
    ORDER BY x.Codigo, x.IdCatalogoPartida;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_CatalogoPartida_Obtener
    @IdCatalogoPartida INT
AS
BEGIN
    SET NOCOUNT ON;
        IF @IdCatalogoPartida IS NULL OR @IdCatalogoPartida <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdCatalogoPartida positivo.', 1;
    SELECT x.IdCatalogoPartida, x.Codigo, x.Nombre, x.Descripcion,
        x.IdPartidaPadre, padre.Codigo AS CodigoPartidaPadre, padre.Nombre AS NombrePartidaPadre,
        x.Nivel, x.IdTipoPartida, t.Codigo AS CodigoTipoPartida, t.Nombre AS NombreTipoPartida,
        x.Activo, hoja.EsHoja, x.FechaCreacion, x.FechaActualizacion
    FROM ControlPresupuestario.CatalogoPartida x
    LEFT JOIN ControlPresupuestario.CatalogoPartida padre ON padre.IdCatalogoPartida = x.IdPartidaPadre
    JOIN ControlPresupuestario.TipoPartida t ON t.IdTipoPartida = x.IdTipoPartida
    CROSS APPLY (SELECT CONVERT(BIT, CASE WHEN EXISTS
        (SELECT 1 FROM ControlPresupuestario.CatalogoPartida h WHERE h.IdPartidaPadre = x.IdCatalogoPartida)
        THEN 0 ELSE 1 END) AS EsHoja) hoja
    WHERE x.IdCatalogoPartida = @IdCatalogoPartida;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_MovimientoPresupuestal_ListarPorDetalle
    @IdPresupuestoDetalle INT
AS
BEGIN
    SET NOCOUNT ON;
        IF @IdPresupuestoDetalle IS NULL OR @IdPresupuestoDetalle <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdPresupuestoDetalle positivo.', 1;
    SELECT mp.IdMovimientoPresupuestal, mp.IdPresupuestoDetalle, mp.IdTipoMovimientoPresupuestal,
        tm.Codigo AS TipoMovimiento, tm.Nombre AS NombreTipoMovimiento, mp.ClaveEvento,
        mp.Origen, mp.IdOrigen, mp.Afectacion, mp.Direccion, mp.Fecha, mp.Monto, mp.Observacion,
        v.IdPresupuesto, v.IdPresupuestoVersion, v.NumeroVersion, e.Codigo AS EstadoPresupuesto,
        d.IdCatalogoPartida, c.Codigo AS CodigoPartida, c.Nombre AS NombrePartida,
        p.Codigo AS CodigoPresupuesto, p.IdMoneda, m.Codigo AS CodigoMoneda, m.Simbolo AS SimboloMoneda
    FROM ControlPresupuestario.MovimientoPresupuestal mp
    JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
        ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
    JOIN ControlPresupuestario.PresupuestoDetalle d ON d.IdPresupuestoDetalle = mp.IdPresupuestoDetalle
    JOIN ControlPresupuestario.CatalogoPartida c ON c.IdCatalogoPartida = d.IdCatalogoPartida
    JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion = d.IdPresupuestoVersion
    JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
    JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = v.IdPresupuesto
    JOIN maestra.Moneda m ON m.IdMoneda = p.IdMoneda
    WHERE mp.IdPresupuestoDetalle = @IdPresupuestoDetalle
    ORDER BY mp.Fecha, mp.IdMovimientoPresupuestal;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_MovimientoPresupuestal_Obtener
    @IdMovimientoPresupuestal BIGINT
AS
BEGIN
    SET NOCOUNT ON;
        IF @IdMovimientoPresupuestal IS NULL OR @IdMovimientoPresupuestal <= 0
            THROW 51200, 'CAMPO_REQUERIDO: IdMovimientoPresupuestal positivo.', 1;
    SELECT mp.IdMovimientoPresupuestal, mp.IdPresupuestoDetalle, mp.IdTipoMovimientoPresupuestal,
        tm.Codigo AS TipoMovimiento, tm.Nombre AS NombreTipoMovimiento, mp.ClaveEvento,
        mp.Origen, mp.IdOrigen, mp.Afectacion, mp.Direccion, mp.Fecha, mp.Monto, mp.Observacion,
        v.IdPresupuesto, v.IdPresupuestoVersion, v.NumeroVersion, e.Codigo AS EstadoPresupuesto,
        d.IdCatalogoPartida, c.Codigo AS CodigoPartida, c.Nombre AS NombrePartida,
        p.Codigo AS CodigoPresupuesto, p.IdMoneda, m.Codigo AS CodigoMoneda, m.Simbolo AS SimboloMoneda
    FROM ControlPresupuestario.MovimientoPresupuestal mp
    JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
        ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
    JOIN ControlPresupuestario.PresupuestoDetalle d ON d.IdPresupuestoDetalle = mp.IdPresupuestoDetalle
    JOIN ControlPresupuestario.CatalogoPartida c ON c.IdCatalogoPartida = d.IdCatalogoPartida
    JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion = d.IdPresupuestoVersion
    JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
    JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = v.IdPresupuesto
    JOIN maestra.Moneda m ON m.IdMoneda = p.IdMoneda
    WHERE mp.IdMovimientoPresupuestal = @IdMovimientoPresupuestal;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_EstadoPresupuesto_Listar
    @Activo BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT IdEstadoPresupuesto, Codigo, Nombre, Descripcion, Activo
    FROM ControlPresupuestario.EstadoPresupuesto
    WHERE @Activo IS NULL OR Activo = @Activo
    ORDER BY Codigo, IdEstadoPresupuesto;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_TipoCentroCosto_Listar
    @Activo BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT IdTipoCentroCosto, Codigo, Nombre, Descripcion, Activo
    FROM ControlPresupuestario.TipoCentroCosto
    WHERE @Activo IS NULL OR Activo = @Activo
    ORDER BY Codigo, IdTipoCentroCosto;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_TipoPartida_Listar
    @Activo BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT IdTipoPartida, Codigo, Nombre, Descripcion, Activo
    FROM ControlPresupuestario.TipoPartida
    WHERE @Activo IS NULL OR Activo = @Activo
    ORDER BY Codigo, IdTipoPartida;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_TipoMovimientoPresupuestal_Listar
    @Activo BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT IdTipoMovimientoPresupuestal, Codigo, Nombre, Descripcion, Activo
    FROM ControlPresupuestario.TipoMovimientoPresupuestal
    WHERE @Activo IS NULL OR Activo = @Activo
    ORDER BY Codigo, IdTipoMovimientoPresupuestal;
END;
GO

CREATE OR ALTER PROCEDURE ControlPresupuestario.usp_Moneda_Listar
    @Activo BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT IdMoneda, Codigo, Nombre, Simbolo, Activo
    FROM maestra.Moneda
    WHERE @Activo IS NULL OR Activo = @Activo
    ORDER BY Codigo, IdMoneda;
END;
GO


