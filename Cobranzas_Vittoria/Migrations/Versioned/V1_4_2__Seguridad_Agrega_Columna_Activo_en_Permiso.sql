/*
Migración: 1.4.2

Descripción:
Se agrega la columna "Activo" a la tabla "Permiso" para indicar si un permiso está activo o no.
*/

ALTER TABLE seguridad.Permiso
    ADD Activo bit NOT NULL CONSTRAINT DF_Permiso_Activo DEFAULT (1);