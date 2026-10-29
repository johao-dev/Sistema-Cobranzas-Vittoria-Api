-- La puesta en marcha terminó. Se conserva la distribución y sus auxiliares operativos.
-- DbUp ejecuta las versionadas antes de sincronizar los repeatables actualizados.
DROP PROCEDURE IF EXISTS almacen.usp_StockProyecto_Inicializar;
DROP PROCEDURE IF EXISTS almacen.usp_StockProyecto_ExigirInicializacion;
DROP TABLE IF EXISTS almacen.StockProyectoInicializacion;
GO
