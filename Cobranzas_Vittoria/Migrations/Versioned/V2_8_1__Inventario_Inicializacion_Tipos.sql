-- La distribución inicial la entrega Almacén; nunca se infiere del histórico.
CREATE TABLE almacen.StockProyectoInicializacion (
    Id INT NOT NULL CONSTRAINT PK_StockProyectoInicializacion PRIMARY KEY,
    FechaActivacion DATETIME2(0) NULL,
    CONSTRAINT CK_StockProyectoInicializacion_Unica CHECK (Id = 1)
);
INSERT INTO almacen.StockProyectoInicializacion (Id) VALUES (1);
GO

-- Cantidades firmadas para aplicar diferencias consolidadas por material/proyecto.
-- La regularización recibe el mismo contrato, pero exige cantidades no negativas.
CREATE TYPE almacen.TVP_StockProyectoCantidad AS TABLE (
    IdMaterial INT NOT NULL,
    IdProyecto INT NOT NULL,
    Cantidad DECIMAL(29,2) NOT NULL,
    PRIMARY KEY (IdMaterial, IdProyecto)
);
GO

CREATE TYPE almacen.TVP_TransferenciaProyectoItem AS TABLE (
    IdMaterial INT NOT NULL,
    Cantidad DECIMAL(18,2) NOT NULL
);
GO
