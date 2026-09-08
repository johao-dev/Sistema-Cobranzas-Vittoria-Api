CREATE TABLE seguridad.RefreshToken (
    IdRefreshToken INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshToken PRIMARY KEY,
    IdUsuario INT NOT NULL,
    TokenHash CHAR(64) NOT NULL,
    FechaExpiracionUtc DATETIME2(0) NOT NULL,
    FechaCreacionUtc DATETIME2(0) NOT NULL CONSTRAINT DF_RefreshToken_FechaCreacion DEFAULT SYSUTCDATETIME(),
    FechaRevocacionUtc DATETIME2(0) NULL,
    ReemplazadoPorHash CHAR(64) NULL,
    CONSTRAINT UQ_RefreshToken_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_RefreshToken_Usuario FOREIGN KEY (IdUsuario) REFERENCES seguridad.Usuario(IdUsuario)
);

CREATE INDEX IX_RefreshToken_Usuario_Activo
    ON seguridad.RefreshToken (IdUsuario, FechaExpiracionUtc)
    WHERE FechaRevocacionUtc IS NULL;
