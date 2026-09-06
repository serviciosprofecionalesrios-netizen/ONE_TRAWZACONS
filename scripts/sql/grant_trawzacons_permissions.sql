/*
  Script: Grant permissions for TRAWZACONS Service Desk
  Principal: DESKTOP-5QF4PAU\EQCOORDINADORIT
  DB: TrawzaconsDB
*/

SET NOCOUNT ON;

DECLARE @LoginName sysname = N'DESKTOP-5QF4PAU\EQCOORDINADORIT';
DECLARE @DbName sysname = N'TrawzaconsDB';

PRINT N'--- Verificando login en master ---';
IF SUSER_ID(@LoginName) IS NULL
BEGIN
    DECLARE @CreateLoginSql nvarchar(max) = N'CREATE LOGIN [' + REPLACE(@LoginName, N']', N']]') + N'] FROM WINDOWS;';
    EXEC (@CreateLoginSql);
    PRINT N'Login creado: ' + @LoginName;
END
ELSE
BEGIN
    PRINT N'Login ya existe: ' + @LoginName;
END

PRINT N'--- Verificando base de datos ---';
IF DB_ID(@DbName) IS NULL
BEGIN
    DECLARE @CreateDbSql nvarchar(max) = N'CREATE DATABASE [' + REPLACE(@DbName, N']', N']]') + N'];';
    EXEC (@CreateDbSql);
    PRINT N'Base de datos creada: ' + @DbName;
END
ELSE
BEGIN
    PRINT N'Base de datos ya existe: ' + @DbName;
END

DECLARE @Sql nvarchar(max) = N'';
SET @Sql = N'
USE [' + REPLACE(@DbName, N']', N']]') + N'];

IF USER_ID(N''' + REPLACE(@LoginName, '''', '''''') + N''') IS NULL
BEGIN
    CREATE USER [' + REPLACE(@LoginName, N']', N']]') + N'] FOR LOGIN [' + REPLACE(@LoginName, N']', N']]') + N'];
END

ALTER USER [' + REPLACE(@LoginName, N']', N']]') + N'] WITH DEFAULT_SCHEMA = [dbo];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
    INNER JOIN sys.database_principals m ON drm.member_principal_id = m.principal_id
    WHERE r.name = N''db_owner''
      AND m.name = N''' + REPLACE(@LoginName, '''', '''''') + N'''
)
BEGIN
    ALTER ROLE [db_owner] ADD MEMBER [' + REPLACE(@LoginName, N']', N']]') + N'];
END;
';

EXEC (@Sql);

PRINT N'Permisos aplicados correctamente para ' + @LoginName + N' en ' + @DbName;

