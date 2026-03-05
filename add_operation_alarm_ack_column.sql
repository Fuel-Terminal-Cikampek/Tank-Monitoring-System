-- ========================================
-- Tank Operation Alarm System
-- Database Migration Script
-- ========================================
-- Description: Add OperationAlarmAck column to Tank_Movement table
-- Date: 2026-02-04
-- Author: AI Assistant

USE [TMS_Database]  -- Adjust database name as needed
GO

-- Check if column already exists
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Tank_Movement]') 
    AND name = 'OperationAlarmAck'
)
BEGIN
    -- Add OperationAlarmAck column
    ALTER TABLE [dbo].[Tank_Movement]
    ADD OperationAlarmAck BIT NOT NULL DEFAULT 0;
    
    PRINT 'Column OperationAlarmAck added successfully to Tank_Movement table.';
END
ELSE
BEGIN
    PRINT 'Column OperationAlarmAck already exists in Tank_Movement table.';
END
GO

-- Optional: Add index for better query performance
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE name = 'IX_Tank_Movement_Status_Ack' 
    AND object_id = OBJECT_ID('[dbo].[Tank_Movement]')
)
BEGIN
    CREATE INDEX IX_Tank_Movement_Status_Ack 
    ON [dbo].[Tank_Movement](Status, OperationAlarmAck);
    
    PRINT 'Index IX_Tank_Movement_Status_Ack created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_Tank_Movement_Status_Ack already exists.';
END
GO

-- Verify the changes
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE, 
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Tank_Movement' AND COLUMN_NAME = 'OperationAlarmAck';
GO

PRINT 'Migration completed successfully!';
GO
