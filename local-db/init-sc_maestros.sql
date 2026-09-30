-- Réplica mínima de sc_maestros (datos maestros legados) para desarrollo local.
-- Solo incluye lo que consume SCNexus.Core.API (UsuariosAlmacenesController).
-- Idempotente: se puede ejecutar varias veces.

IF DB_ID(N'sc_maestros') IS NULL
    CREATE DATABASE sc_maestros;
GO

USE sc_maestros;
GO

IF OBJECT_ID(N'dbo.almacenes_local', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.almacenes_local (
        whs_code NVARCHAR(20)  NOT NULL CONSTRAINT PK_almacenes_local PRIMARY KEY,
        whs_name NVARCHAR(150) NULL
    );
END
GO

-- Almacenes de ejemplo (códigos tomados de docs/JWT-WAREHOUSE-ACCESS.md).
MERGE dbo.almacenes_local AS t
USING (VALUES
    (N'RL-PSC',  N'Almacén RL-PSC (local)'),
    (N'RL-PSP',  N'Almacén RL-PSP (local)'),
    (N'OUT-LLG', N'Almacén OUT-LLG (local)')
) AS s (whs_code, whs_name)
ON t.whs_code = s.whs_code
WHEN NOT MATCHED THEN
    INSERT (whs_code, whs_name) VALUES (s.whs_code, s.whs_name);
GO

PRINT 'sc_maestros listo';
GO
