-- Catálogo transversal. Conservar IDs, códigos y todas las filas; sin IDs hardcodeados.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
 IF OBJECT_ID('maestra.Moneda', 'U') IS NOT NULL
     THROW 51300, 'maestra.Moneda ya existe: revisar colisión antes de migrar el catálogo.', 1;
 CREATE TABLE maestra.Moneda (
     IdMoneda INT IDENTITY(1,1) NOT NULL,
     Codigo VARCHAR(3) NOT NULL,
     Nombre NVARCHAR(50) NOT NULL,
     Simbolo NVARCHAR(10) NOT NULL,
     Activo BIT NOT NULL CONSTRAINT DF_Moneda_Activo DEFAULT (1),
     CONSTRAINT PK_Moneda PRIMARY KEY CLUSTERED (IdMoneda),
     CONSTRAINT UQ_Moneda_Codigo UNIQUE (Codigo)
 );
 SET IDENTITY_INSERT maestra.Moneda ON;
 INSERT INTO maestra.Moneda (IdMoneda,Codigo,Nombre,Simbolo,Activo)
 SELECT IdMoneda,Codigo,Nombre,Simbolo,Activo FROM ControlPresupuestario.Moneda;
 SET IDENTITY_INSERT maestra.Moneda OFF;
 IF NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE Codigo='PEN')
     OR NOT EXISTS (SELECT 1 FROM maestra.Moneda WHERE Codigo='USD')
     THROW 51301, 'El catálogo origen no contiene PEN y USD: revisar datos.', 1;
 DECLARE @sql NVARCHAR(MAX) = N'';
 SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.'
     + QUOTENAME(OBJECT_NAME(parent_object_id)) + N' DROP CONSTRAINT '+QUOTENAME(name)+N';'
 FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID('ControlPresupuestario.Moneda');
 -- El modelo versionado tiene únicamente la FK de Presupuesto; no perder FKs desconocidas.
 IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID('ControlPresupuestario.Moneda')
     AND parent_object_id<>OBJECT_ID('ControlPresupuestario.Presupuesto'))
     THROW 51302, 'Existen FKs adicionales hacia Moneda: auditar antes de continuar.', 1;
 EXEC sys.sp_executesql @sql;
 ALTER TABLE ControlPresupuestario.Presupuesto WITH CHECK ADD CONSTRAINT FK_Presupuesto_Moneda
     FOREIGN KEY (IdMoneda) REFERENCES maestra.Moneda(IdMoneda);
 -- DbUp ejecuta versionadas antes de repeatables: actualizar dependencias ANTES del DROP.
 DECLARE objetos CURSOR LOCAL FAST_FORWARD FOR
 SELECT REPLACE(definition, N'ControlPresupuestario.Moneda', N'maestra.Moneda')
 FROM sys.sql_modules WHERE definition LIKE N'%ControlPresupuestario.Moneda%';
 OPEN objetos;
 FETCH NEXT FROM objetos INTO @sql;
 WHILE @@FETCH_STATUS=0
 BEGIN

     -- sys.sql_modules conserva comentarios iniciales y puede sustituir OR ALTER
     -- por espacios. Leer sólo las palabras del encabezado, sin tocar el cuerpo.
     DECLARE @ddlPos INT=1, @ddlInicio INT, @ddlInicioToken INT,
         @ddlFin INT, @ddlProfundidad INT, @ddlTokens INT=0, @ddlPalabra NVARCHAR(30);
     WHILE @ddlTokens < 3
     BEGIN
         WHILE @ddlPos <= LEN(@sql)
         BEGIN
             IF UNICODE(SUBSTRING(@sql,@ddlPos,1)) IN (9,10,13,32,160,65279)
                 SET @ddlPos+=1;
             ELSE IF SUBSTRING(@sql,@ddlPos,2)=N'--'
             BEGIN
                 SET @ddlFin=PATINDEX(N'%['+NCHAR(10)+NCHAR(13)+N']%',SUBSTRING(@sql,@ddlPos+2,LEN(@sql)));
                 SET @ddlPos=CASE WHEN @ddlFin=0 THEN LEN(@sql)+1 ELSE @ddlPos+@ddlFin+2 END;
             END
             ELSE IF SUBSTRING(@sql,@ddlPos,2)=N'/*'
             BEGIN
                 SET @ddlProfundidad=1;
                 SET @ddlPos+=2;
                 WHILE @ddlPos <= LEN(@sql) AND @ddlProfundidad>0
                 BEGIN
                     IF SUBSTRING(@sql,@ddlPos,2)=N'/*'
                     BEGIN
                         SET @ddlProfundidad+=1;
                         SET @ddlPos+=2;
                     END
                     ELSE IF SUBSTRING(@sql,@ddlPos,2)=N'*/'
                     BEGIN
                         SET @ddlProfundidad-=1;
                         SET @ddlPos+=2;
                     END
                     ELSE SET @ddlPos+=1;
                 END;
                 IF @ddlProfundidad<>0
                     THROW 51304, 'Comentario SQL sin cerrar en una definición dependiente. No ejecutar CREATE sobre un objeto existente.', 1;
             END
             ELSE BREAK;
         END;
         SET @ddlInicioToken=@ddlPos;
         WHILE @ddlPos <= LEN(@sql)
             AND SUBSTRING(@sql,@ddlPos,1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Za-z]'
             SET @ddlPos+=1;
         SET @ddlPalabra=UPPER(SUBSTRING(@sql,@ddlInicioToken,@ddlPos-@ddlInicioToken));
         SET @ddlTokens+=1;
         IF @ddlTokens=1
         BEGIN
             SET @ddlInicio=@ddlInicioToken;
             IF @ddlPalabra=N'ALTER'
             BEGIN
                 SET @sql=STUFF(@sql,@ddlInicio,@ddlPos-@ddlInicio,N'ALTER');
                 BREAK;
             END;
             IF @ddlPalabra<>N'CREATE'
                 THROW 51304, 'Encabezado SQL dependiente no reconocido. Revisar su definición antes de migrar.', 1;
         END
         ELSE IF @ddlTokens=2 AND @ddlPalabra<>N'OR'
         BEGIN
             IF @ddlPalabra NOT IN (N'VIEW',N'PROC',N'PROCEDURE',N'FUNCTION',N'TRIGGER')
                 THROW 51304, 'Tipo de módulo SQL no reconocido en una definición dependiente.', 1;
             SET @sql=STUFF(@sql,@ddlInicio,6,N'ALTER');
             BREAK;
         END
         ELSE IF @ddlTokens=3
         BEGIN
             IF @ddlPalabra<>N'ALTER'
                 THROW 51304, 'Encabezado CREATE OR ALTER inválido en una definición dependiente.', 1;
             SET @sql=STUFF(@sql,@ddlInicio,@ddlPos-@ddlInicio,N'ALTER');
         END;
     END;
     EXEC sys.sp_executesql @sql;
     FETCH NEXT FROM objetos INTO @sql;
 END;
 CLOSE objetos;
 DEALLOCATE objetos;
 IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies
     WHERE referenced_id=OBJECT_ID('ControlPresupuestario.Moneda'))
     THROW 51303, 'Persisten dependencias del catálogo anterior: no eliminarlo.', 1;
 DROP TABLE ControlPresupuestario.Moneda;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
