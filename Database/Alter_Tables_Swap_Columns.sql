-- ═══════════════════════════════════════════════════════════════════════════
-- ALTER TABLES: Swap Legacy Columns
-- ═══════════════════════════════════════════════════════════════════════════
-- Purpose:
--   - Tank_LiveData: DROP 9 legacy columns (become 19 CLEAN columns)
--   - Tank_LiveDataTMS: ADD 9 legacy columns (become 28 FULL columns)
--
-- Connection String:
--   Server=192.168.2.3;Database=TAS_CIKAMPEK2025;User ID=sa;Password=p3rt4m1n@;
-- ═══════════════════════════════════════════════════════════════════════════

USE TAS_CIKAMPEK2025
GO

PRINT '═══════════════════════════════════════════════════════════════════════════'
PRINT '  STEP 1: DROP 9 LEGACY COLUMNS FROM Tank_LiveData'
PRINT '═══════════════════════════════════════════════════════════════════════════'

-- Drop columns from Tank_LiveData (make it CLEAN - 19 columns only)
BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [Ack]
    PRINT '✅  Dropped column: Ack'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column Ack does not exist or already dropped'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [FlowRateMperSecond]
    PRINT '✅  Dropped column: FlowRateMperSecond'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column FlowRateMperSecond does not exist or already dropped'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [LastLiquidLevel]
    PRINT '✅  Dropped column: LastLiquidLevel'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column LastLiquidLevel does not exist or already dropped'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [LastTimeStamp]
    PRINT '✅  Dropped column: LastTimeStamp'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column LastTimeStamp does not exist or already dropped'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [TotalSecond]
    PRINT '✅  Dropped column: TotalSecond'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column TotalSecond does not exist or already dropped'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [LastVolume]
    PRINT '✅  Dropped column: LastVolume'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column LastVolume does not exist or already dropped'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [Pumpable]
    PRINT '✅  Dropped column: Pumpable'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column Pumpable does not exist or already dropped'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [AlarmMessage]
    PRINT '✅  Dropped column: AlarmMessage'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column AlarmMessage does not exist or already dropped'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveData] DROP COLUMN [Ullage]
    PRINT '✅  Dropped column: Ullage'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column Ullage does not exist or already dropped'
END CATCH

PRINT ''
PRINT '═══════════════════════════════════════════════════════════════════════════'
PRINT '  STEP 2: ADD 9 LEGACY COLUMNS TO Tank_LiveDataTMS'
PRINT '═══════════════════════════════════════════════════════════════════════════'

-- Add columns to Tank_LiveDataTMS (make it FULL - 28 columns)
BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [Ack] BIT NULL
    PRINT '✅  Added column: Ack (BIT)'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column Ack already exists'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [FlowRateMperSecond] FLOAT NULL
    PRINT '✅  Added column: FlowRateMperSecond (FLOAT)'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column FlowRateMperSecond already exists'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [LastLiquidLevel] FLOAT NULL
    PRINT '✅  Added column: LastLiquidLevel (FLOAT)'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column LastLiquidLevel already exists'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [LastTimeStamp] DATETIME NULL
    PRINT '✅  Added column: LastTimeStamp (DATETIME)'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column LastTimeStamp already exists'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [TotalSecond] INT NULL
    PRINT '✅  Added column: TotalSecond (INT)'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column TotalSecond already exists'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [LastVolume] FLOAT NULL
    PRINT '✅  Added column: LastVolume (FLOAT)'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column LastVolume already exists'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [Pumpable] FLOAT NULL
    PRINT '✅  Added column: Pumpable (FLOAT)'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column Pumpable already exists'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [AlarmMessage] VARCHAR(100) NULL
    PRINT '✅  Added column: AlarmMessage (VARCHAR(100))'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column AlarmMessage already exists'
END CATCH

BEGIN TRY
    ALTER TABLE [dbo].[Tank_LiveDataTMS] ADD [Ullage] FLOAT NULL
    PRINT '✅  Added column: Ullage (FLOAT)'
END TRY
BEGIN CATCH
    PRINT '⚠️  Column Ullage already exists'
END CATCH

PRINT ''
PRINT '═══════════════════════════════════════════════════════════════════════════'
PRINT '  STEP 3: VERIFICATION'
PRINT '═══════════════════════════════════════════════════════════════════════════'

-- Check column counts
DECLARE @LiveDataColumns INT
DECLARE @TMSColumns INT

SELECT @LiveDataColumns = COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Tank_LiveData'

SELECT @TMSColumns = COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Tank_LiveDataTMS'

PRINT '  Tank_LiveData columns:    ' + CAST(@LiveDataColumns AS VARCHAR(10)) + ' (expected: 19)'
PRINT '  Tank_LiveDataTMS columns: ' + CAST(@TMSColumns AS VARCHAR(10)) + ' (expected: 28)'

IF @LiveDataColumns = 19 AND @TMSColumns = 28
BEGIN
    PRINT ''
    PRINT '✅  Column counts CORRECT!'
END
ELSE
BEGIN
    PRINT ''
    PRINT '⚠️  WARNING: Column counts do not match expected!'
    PRINT '    Tank_LiveData should have 19 columns'
    PRINT '    Tank_LiveDataTMS should have 28 columns'
END

PRINT ''
PRINT '═══════════════════════════════════════════════════════════════════════════'
PRINT '  ✅  ALTER TABLES COMPLETE!'
PRINT '═══════════════════════════════════════════════════════════════════════════'
PRINT ''
PRINT '  NEXT STEPS:'
PRINT '  1. Update TankLiveData.cs model (NotMapped for 9 legacy columns)'
PRINT '  2. Update TankLiveDataTMS.cs model (add 9 legacy columns)'
PRINT '  3. Update DBHelper.cs (adjust column mappings)'
PRINT '  4. Update TMS.Web AlarmController (use Tank_LiveDataTMS)'
PRINT ''
PRINT '═══════════════════════════════════════════════════════════════════════════'
GO
