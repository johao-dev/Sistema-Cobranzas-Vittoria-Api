-- =============================================
-- Author:      Johao Bravo
-- Create date: 2026-09-08
-- Description: Migración para actualizar los passwords del seed de usuarios a hashes BCrypt.
-- =============================================

UPDATE seguridad.Usuario
SET PasswordHash = CASE UsuarioLogin
    -- DbUp usa $nombre$ para variables; NCHAR(36) evita que procese los $ de BCrypt.
    WHEN N'admin' THEN CONCAT(NCHAR(36), N'2y', NCHAR(36), N'12', NCHAR(36), N'w7inzSsF/AkXnk6nKnRM2ufThmcFPfCk3R4kIHtvtHeaKVDm8PnU6')
    WHEN N'ingeniero' THEN CONCAT(NCHAR(36), N'2y', NCHAR(36), N'12', NCHAR(36), N'FXhk3.aqKttwet06AXdkne4iaZYOpuTl4O5N6ys0A5uXg.p7xrxey')
    WHEN N'almacen' THEN CONCAT(NCHAR(36), N'2y', NCHAR(36), N'12', NCHAR(36), N'2tM0b9f0QS1w7dm3KX46JuEMpiOzdER79JNGw9Ji52w4tdfJDYbJW')
    WHEN N'contable' THEN CONCAT(NCHAR(36), N'2y', NCHAR(36), N'12', NCHAR(36), N'rJ3x3oqQ3aY5c0R.QQ00..a4FlflQHCZvwurp4nUvtYIR8SMhyTz6')
    WHEN N'admin2' THEN CONCAT(NCHAR(36), N'2y', NCHAR(36), N'12', NCHAR(36), N'bYTqTMvaFHs1P..mkXcRuuM2I9FqGlzXXzD/nVqKWlxxk.iYfE5.G')
END
WHERE UsuarioLogin IN (N'admin', N'ingeniero', N'almacen', N'contable', N'admin2');
