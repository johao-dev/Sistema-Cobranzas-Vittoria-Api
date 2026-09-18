SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
 IF EXISTS (SELECT 1 FROM compras.Requerimiento r WHERE r.IdPresupuestoDetalle IS NOT NULL
     AND NOT EXISTS (SELECT 1 FROM compras.RequerimientoDetalle d WHERE d.IdRequerimiento=r.IdRequerimiento))
     THROW 51310, 'Hay partidas en requerimientos sin detalles: revisar AUDITORIA_COMPRAS antes de eliminar la asociación.', 1;
 ALTER TABLE compras.RequerimientoDetalle ADD IdPresupuestoDetalle INT NULL;
 EXEC sys.sp_executesql N'UPDATE d SET IdPresupuestoDetalle=r.IdPresupuestoDetalle
     FROM compras.RequerimientoDetalle d JOIN compras.Requerimiento r ON r.IdRequerimiento=d.IdRequerimiento;';
 EXEC sys.sp_executesql N'ALTER TABLE compras.RequerimientoDetalle WITH CHECK ADD CONSTRAINT FK_RequerimientoDetalle_PresupuestoDetalle
     FOREIGN KEY (IdPresupuestoDetalle) REFERENCES ControlPresupuestario.PresupuestoDetalle(IdPresupuestoDetalle);
 CREATE INDEX IX_RequerimientoDetalle_IdPresupuestoDetalle ON compras.RequerimientoDetalle(IdPresupuestoDetalle)
     WHERE IdPresupuestoDetalle IS NOT NULL;';
 ALTER TABLE compras.Requerimiento DROP CONSTRAINT FK_Requerimiento_PresupuestoDetalle;
 DROP INDEX IX_Requerimiento_IdPresupuestoDetalle ON compras.Requerimiento;
 ALTER TABLE compras.Requerimiento DROP COLUMN IdPresupuestoDetalle;
 -- Los tipos tabla no admiten ALTER; preservar temporalmente las definiciones de sus consumidores.
 DECLARE @consumidores TABLE (Id INT IDENTITY, Nombre NVARCHAR(517), Definicion NVARCHAR(MAX));
 INSERT INTO @consumidores (Nombre,Definicion)
 SELECT QUOTENAME(OBJECT_SCHEMA_NAME(p.object_id))+'.'+QUOTENAME(OBJECT_NAME(p.object_id)), m.definition
 FROM sys.parameters p JOIN sys.sql_modules m ON m.object_id=p.object_id
 WHERE p.user_type_id=TYPE_ID('compras.TVP_RequerimientoDetalle') GROUP BY p.object_id,m.definition;
 -- DROP/CREATE elimina permisos explícitos del tipo y sus SPs; conservarlos también.
 DECLARE @permisos TABLE (Id INT IDENTITY, Sentencia NVARCHAR(MAX));
 INSERT INTO @permisos (Sentencia)
 SELECT CASE WHEN dp.state='D' THEN N'DENY ' ELSE N'GRANT ' END + dp.permission_name
     + CASE WHEN dp.class=6 THEN N' ON TYPE::[compras].[TVP_RequerimientoDetalle]'
         ELSE N' ON OBJECT::'+QUOTENAME(OBJECT_SCHEMA_NAME(dp.major_id))+N'.'+QUOTENAME(OBJECT_NAME(dp.major_id)) END
     + N' TO '+QUOTENAME(USER_NAME(dp.grantee_principal_id))
     + CASE WHEN dp.state='W' THEN N' WITH GRANT OPTION' ELSE N'' END
     + N' AS '+QUOTENAME(USER_NAME(dp.grantor_principal_id))+N';'
 FROM sys.database_permissions dp
 WHERE (dp.class=6 AND dp.major_id=TYPE_ID('compras.TVP_RequerimientoDetalle'))
     OR (dp.class=1 AND EXISTS (SELECT 1 FROM @consumidores c
         WHERE c.Nombre=QUOTENAME(OBJECT_SCHEMA_NAME(dp.major_id))+N'.'+QUOTENAME(OBJECT_NAME(dp.major_id))));
 DECLARE @id INT=1, @nombre NVARCHAR(517), @sql NVARCHAR(MAX);
 WHILE @id <= (SELECT COUNT(*) FROM @consumidores)
 BEGIN
     SELECT @nombre=Nombre FROM @consumidores WHERE Id=@id;
     SET @sql=N'DROP PROCEDURE '+@nombre;
     EXEC sys.sp_executesql @sql;
     SET @id+=1;
 END;
 DROP TYPE compras.TVP_RequerimientoDetalle;
 EXEC sys.sp_executesql N'CREATE TYPE compras.TVP_RequerimientoDetalle AS TABLE (
     IdMaterial INT NOT NULL, Cantidad DECIMAL(18,2) NOT NULL,
     Observacion NVARCHAR(250) NULL, IdPresupuestoDetalle INT NULL);';
 SET @id=1;
 WHILE @id <= (SELECT COUNT(*) FROM @consumidores)
 BEGIN
     SELECT @sql=Definicion FROM @consumidores WHERE Id=@id;

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
                 SET @sql=STUFF(@sql,@ddlInicio,@ddlPos-@ddlInicio,N'CREATE');
                 BREAK;
             END;
             IF @ddlPalabra<>N'CREATE'
                 THROW 51304, 'Encabezado SQL dependiente no reconocido. Revisar su definición antes de migrar.', 1;
         END
         ELSE IF @ddlTokens=2 AND @ddlPalabra<>N'OR'
         BEGIN
             IF @ddlPalabra NOT IN (N'VIEW',N'PROC',N'PROCEDURE',N'FUNCTION',N'TRIGGER')
                 THROW 51304, 'Tipo de módulo SQL no reconocido en una definición dependiente.', 1;
             SET @sql=STUFF(@sql,@ddlInicio,6,N'CREATE');
             BREAK;
         END
         ELSE IF @ddlTokens=3
         BEGIN
             IF @ddlPalabra<>N'ALTER'
                 THROW 51304, 'Encabezado CREATE OR ALTER inválido en una definición dependiente.', 1;
             SET @sql=STUFF(@sql,@ddlInicio,@ddlPos-@ddlInicio,N'CREATE');
         END;
     END;
     EXEC sys.sp_executesql @sql;
     SET @id+=1;
 END;
 SET @id=1;
 WHILE @id <= (SELECT COUNT(*) FROM @permisos)
 BEGIN
     SELECT @sql=Sentencia FROM @permisos WHERE Id=@id;
     EXEC sys.sp_executesql @sql;
     SET @id+=1;
 END;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
