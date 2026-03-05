-- ═══════════════════════════════════════════════════════════════════════════
-- CREATE TABLE: Tank_LiveDataTMS
-- ═══════════════════════════════════════════════════════════════════════════
-- Purpose:
--   CLEAN version of Tank_LiveData for TMS Cikampek only (19 columns)
--   Tank_LiveData tetap dengan kolom lengkap untuk backward compatibility dengan FDM Web
--
-- REMOVED COLUMNS (9 kolom yang TIDAK ada di Tank_LiveDataTMS):
--   Ack, FlowRateMperSecond, LastLiquidLevel, LastTimeStamp
--   TotalSecond, LastVolume, Pumpable, AlarmMessage, Ullage
--
-- Connection String:
--   Server=192.168.2.3;Database=TAS_CIKAMPEK2025;User ID=sa;Password=p3rt4m1n@;
-- ═══════════════════════════════════════════════════════════════════════════

USE TAS_CIKAMPEK2025
GO

-- ═══════════════════════════════════════════════════════════════════════════
-- STEP 1: CHECK IF TABLE EXISTS
-- ═══════════════════════════════════════════════════════════════════════════
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Tank_LiveDataTMS]') AND type in (N'U'))
BEGIN
    PRINT '⚠️  WARNING: Table Tank_LiveDataTMS already exists!'
    PRINT '    Dropping existing table...'
    DROP TABLE [dbo].[Tank_LiveDataTMS]
    PRINT '✅  Old table dropped'
END
GO

-- ═══════════════════════════════════════════════════════════════════════════
-- STEP 2: CREATE TABLE Tank_LiveDataTMS
-- ═══════════════════════════════════════════════════════════════════════════
PRINT ''
PRINT '═══════════════════════════════════════════════════════════════════════════'
PRINT '  CREATING TABLE: Tank_LiveDataTMS (CLEAN VERSION - 19 COLUMNS)'
PRINT '═══════════════════════════════════════════════════════════════════════════'

CREATE TABLE [dbo].[Tank_LiveDataTMS] (
    -- ============ PRIMARY KEY ============
    [Tank_Number] VARCHAR(50) NOT NULL PRIMARY KEY,

    -- ============ PRODUCT & TIME ============
    [Product_ID] VARCHAR(20) NULL,
    [TimeStamp] DATETIME NULL,

    -- ============ MEASUREMENTS ============
    [Level] INT NULL,                    -- mm
    [Level_Water] INT NULL,              -- mm
    [Temperature] FLOAT NULL,            -- Celsius
    [Density] FLOAT NULL,                -- g/cm³

    -- ============ VOLUMES ============
    [Volume_Obs] FLOAT NULL,             -- m³ (Observed Volume)
    [Density_Std] FLOAT NULL,            -- Standard Density
    [Volume_Std] FLOAT NULL,             -- m³ (Standard Volume)
    [Volume_LongTons] FLOAT NULL,        -- Long Tons
    [Volume_BBL60F] FLOAT NULL,          -- Barrels at 60°F

    -- ============ FLOW & ALARMS ============
    [Flowrate] FLOAT NULL,               -- KL/h
    [Alarm_Status] INT NULL,             -- 0=Normal, 1=Alarm

    -- ============ CAPACITY LEVELS ============
    [Level_DeadStock] INT NULL,          -- mm
    [Volume_DeadStock] FLOAT NULL,       -- m³
    [Level_Safe_Capacity] INT NULL,      -- mm
    [Volume_Safe_Capacity] FLOAT NULL    -- m³
)
GO

PRINT '✅  Table Tank_LiveDataTMS created successfully!'
PRINT '    Columns: 19 (CLEAN - no legacy columns)'

-- ═══════════════════════════════════════════════════════════════════════════
-- STEP 3: CREATE INDEXES FOR PERFORMANCE
-- ═══════════════════════════════════════════════════════════════════════════
PRINT ''
PRINT '───────────────────────────────────────────────────────────────────────────'
PRINT '  CREATING INDEXES...'
PRINT '───────────────────────────────────────────────────────────────────────────'

-- Index on TimeStamp for time-based queries
CREATE INDEX IX_Tank_LiveDataTMS_TimeStamp
ON [dbo].[Tank_LiveDataTMS]([TimeStamp] DESC)
GO
PRINT '✅  Index IX_Tank_LiveDataTMS_TimeStamp created'

-- Index on Product_ID for product filtering
CREATE INDEX IX_Tank_LiveDataTMS_Product_ID
ON [dbo].[Tank_LiveDataTMS]([Product_ID])
GO
PRINT '✅  Index IX_Tank_LiveDataTMS_Product_ID created'

-- Composite index for common queries (Tank + TimeStamp)
CREATE INDEX IX_Tank_LiveDataTMS_Tank_TimeStamp
ON [dbo].[Tank_LiveDataTMS]([Tank_Number], [TimeStamp] DESC)
GO
PRINT '✅  Index IX_Tank_LiveDataTMS_Tank_TimeStamp created'

-- ═══════════════════════════════════════════════════════════════════════════
-- STEP 4: BACKFILL DATA FROM Tank_LiveData
-- ═══════════════════════════════════════════════════════════════════════════
PRINT ''
PRINT '───────────────────────────────────────────────────────────────────────────'
PRINT '  BACKFILLING DATA FROM Tank_LiveData...'
PRINT '───────────────────────────────────────────────────────────────────────────'

DECLARE @RowCount INT

-- Copy data dari Tank_LiveData ke Tank_LiveDataTMS
-- Hanya copy 19 kolom yang CLEAN (skip 9 kolom legacy)
INSERT INTO [dbo].[Tank_LiveDataTMS] (
    Tank_Number,
    Product_ID,
    TimeStamp,
    Level,
    Level_Water,
    Temperature,
    Density,
    Volume_Obs,
    Density_Std,
    Volume_Std,
    Volume_LongTons,
    Volume_BBL60F,
    Flowrate,
    Alarm_Status,
    Level_DeadStock,
    Volume_DeadStock,
    Level_Safe_Capacity,
    Volume_Safe_Capacity
)
SELECT
    Tank_Number,
    Product_ID,
    TimeStamp,
    Level,
    Level_Water,
    Temperature,
    Density,
    Volume_Obs,
    Density_Std,
    Volume_Std,
    Volume_LongTons,
    Volume_BBL60F,
    Flowrate,
    Alarm_Status,
    Level_DeadStock,
    Volume_DeadStock,
    Level_Safe_Capacity,
    Volume_Safe_Capacity
FROM [dbo].[Tank_LiveData]
-- WHERE TimeStamp > DATEADD(DAY, -30, GETDATE())  -- Optional: Only backfill last 30 days

SET @RowCount = @@ROWCOUNT

PRINT '✅  Data backfilled successfully!'
PRINT '    Rows copied: ' + CAST(@RowCount AS VARCHAR(10))

-- ═══════════════════════════════════════════════════════════════════════════
-- STEP 5: VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
PRINT ''
PRINT '───────────────────────────────────────────────────────────────────────────'
PRINT '  VERIFICATION'
PRINT '───────────────────────────────────────────────────────────────────────────'

-- Check row counts
DECLARE @LiveDataCount INT
DECLARE @TMSCount INT

SELECT @LiveDataCount = COUNT(*) FROM [dbo].[Tank_LiveData]
SELECT @TMSCount = COUNT(*) FROM [dbo].[Tank_LiveDataTMS]

PRINT '  Tank_LiveData:    ' + CAST(@LiveDataCount AS VARCHAR(10)) + ' rows'
PRINT '  Tank_LiveDataTMS: ' + CAST(@TMSCount AS VARCHAR(10)) + ' rows'

IF @LiveDataCount = @TMSCount
BEGIN
    PRINT '✅  Row counts match!'
END
ELSE
BEGIN
    PRINT '⚠️  WARNING: Row counts do NOT match!'
    PRINT '    Difference: ' + CAST(ABS(@LiveDataCount - @TMSCount) AS VARCHAR(10)) + ' rows'
END

-- Show sample data
PRINT ''
PRINT '───────────────────────────────────────────────────────────────────────────'
PRINT '  SAMPLE DATA (First 5 tanks):'
PRINT '───────────────────────────────────────────────────────────────────────────'

SELECT TOP 5
    Tank_Number,
    Product_ID,
    TimeStamp,
    Level,
    Flowrate,
    Alarm_Status
FROM [dbo].[Tank_LiveDataTMS]
ORDER BY Tank_Number

-- ═══════════════════════════════════════════════════════════════════════════
-- STEP 6: COMPARISON QUERY (Optional - for testing)
-- ═══════════════════════════════════════════════════════════════════════════
PRINT ''
PRINT '───────────────────────────────────────────────────────────────────────────'
PRINT '  COMPARISON QUERY TEMPLATE (for manual testing):'
PRINT '───────────────────────────────────────────────────────────────────────────'
PRINT '  Run this query to compare data between tables:'
PRINT ''
PRINT '  SELECT '
PRINT '      ld.Tank_Number,'
PRINT '      ld.Level as LiveData_Level,'
PRINT '      tms.Level as TMS_Level,'
PRINT '      ld.Flowrate as LiveData_Flowrate,'
PRINT '      tms.Flowrate as TMS_Flowrate,'
PRINT '      ld.TimeStamp as LiveData_Time,'
PRINT '      tms.TimeStamp as TMS_Time'
PRINT '  FROM Tank_LiveData ld'
PRINT '  LEFT JOIN Tank_LiveDataTMS tms ON ld.Tank_Number = tms.Tank_Number'
PRINT '  ORDER BY ld.Tank_Number'
PRINT ''

-- ═══════════════════════════════════════════════════════════════════════════
-- SUMMARY
-- ═══════════════════════════════════════════════════════════════════════════
PRINT '═══════════════════════════════════════════════════════════════════════════'
PRINT '  ✅  SETUP COMPLETE!'
PRINT '═══════════════════════════════════════════════════════════════════════════'
PRINT ''
PRINT '  Table Created:  Tank_LiveDataTMS'
PRINT '  Columns:        19 (CLEAN - no legacy columns)'
PRINT '  Indexes:        3 indexes created'
PRINT '  Data:           ' + CAST(@TMSCount AS VARCHAR(10)) + ' rows backfilled'
PRINT ''
PRINT '  NEXT STEPS:'
PRINT '  1. Test ATG Service WorkerXmlMode locally'
PRINT '  2. Verify data sync between Tank_LiveData and Tank_LiveDataTMS'
PRINT '  3. Monitor logs for any errors'
PRINT ''
PRINT '═══════════════════════════════════════════════════════════════════════════'
GO
