-- =============================================================================
-- 07_recovery_disk_full.sql
-- Run this when the database is full (Error 1101) and/or the log is full
-- (Error 9002).  Steps must be run in order.
--
-- IMPORTANT: Stop the ingestion container BEFORE running this script.
-- An open transaction from the service will hold the log open (9002,
-- ACTIVE_TRANSACTION) and prevent log truncation.
--
--   docker stop json-ingest
--
-- After recovery, restart with:
--   docker start json-ingest
-- =============================================================================

USE [s43k_CyclopsCycComms];
GO

-- ---------------------------------------------------------------------------
-- STEP 0 — Confirm no active transactions are holding the log open.
-- If DBCC OPENTRAN reports an open transaction, stop the ingestion container
-- (docker stop json-ingest) and kill any other blocking sessions shown below.
-- ---------------------------------------------------------------------------
DBCC OPENTRAN;          -- should report "No active open transactions"
GO

-- Sessions currently blocking log truncation (empty = safe to proceed):
SELECT
    s.session_id,
    s.login_name,
    s.program_name,
    t.transaction_begin_time,
    t.name                  AS transaction_name,
    r.command,
    r.status,
    r.wait_type,
    r.wait_time / 1000.0    AS wait_seconds
FROM sys.dm_exec_sessions         s
JOIN sys.dm_tran_session_transactions st ON st.session_id = s.session_id
JOIN sys.dm_tran_active_transactions  t  ON t.transaction_id = st.transaction_id
LEFT JOIN sys.dm_exec_requests        r  ON r.session_id = s.session_id
WHERE t.transaction_begin_time IS NOT NULL
ORDER BY t.transaction_begin_time;
GO
-- To kill a blocking session:  KILL <session_id>;
GO
-- Switching to SIMPLE recovery allows SQL Server to reuse the log space
-- that is held for transactions that have already committed/rolled back.
-- ---------------------------------------------------------------------------
ALTER DATABASE [s43k_CyclopsCycComms] SET RECOVERY SIMPLE WITH NO_WAIT;
GO

-- Force a checkpoint so SQL Server marks the inactive log as reusable.
USE [s43k_CyclopsCycComms];
GO
CHECKPOINT;
GO

-- Shrink the log file back to 64 MB.  Find the logical name first:
--   SELECT name, type_desc, size/128 AS size_MB FROM sys.database_files;
-- Then replace 's43k_CyclopsCycComms_log' below if yours differs.
DBCC SHRINKFILE (N's43k_CyclopsCycComms_log', 64);
GO

-- ---------------------------------------------------------------------------
-- STEP 2 — Clear all cdm data so the data file can be shrunk
-- Run 06_reset_data.sql here, or paste the DELETE block inline.
-- ---------------------------------------------------------------------------
DELETE FROM cdm.EventPartyIdentifier;
DELETE FROM cdm.PartyAttribute;
DELETE FROM cdm.SubscriptionLocation;
DELETE FROM cdm.SubscriptionIdentifier;
DELETE FROM cdm.SubscriptionAttribute;
DELETE FROM cdm.SubscriberLocation;
DELETE FROM cdm.SubscriberIdentifier;
DELETE FROM cdm.SubscriberAttribute;
DELETE FROM cdm.EventParty;
DELETE FROM cdm.EventAttribute;
DELETE FROM cdm.Subscription;
DELETE FROM cdm.Subscriber;
DELETE FROM cdm.Event;
DELETE FROM cdm.WifiAccessPoint;
DELETE FROM cdm.PostalAddress;
DELETE FROM cdm.GeoLocation;
DELETE FROM cdm.CellLocation;
DELETE FROM cdm.Location;
DELETE FROM cdm.Identifier;
DELETE FROM cdm.JsonFile;
GO

-- ---------------------------------------------------------------------------
-- STEP 3 — Shrink the data file back to a sensible size
-- TRUNCATEONLY releases pages at the end of the file without moving data.
-- Use DBCC SHRINKDATABASE to reclaim space more aggressively if needed.
-- ---------------------------------------------------------------------------
CHECKPOINT;
GO
DBCC SHRINKDATABASE ([s43k_CyclopsCycComms], 10);  -- leave 10% free space
GO

-- ---------------------------------------------------------------------------
-- STEP 4 — Re-enable autogrowth on every file so this can't happen again
-- (sized grows: 512 MB for data files, 256 MB for the log)
-- ---------------------------------------------------------------------------
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER DATABASE [s43k_CyclopsCycComms] MODIFY FILE (NAME = N'''
    + name + N''', FILEGROWTH = '
    + CASE type_desc WHEN 'ROWS' THEN '512MB' ELSE '256MB' END
    + N');' + CHAR(10)
FROM sys.master_files
WHERE database_id = DB_ID(N's43k_CyclopsCycComms');
EXEC sp_executesql @sql;
PRINT 'Autogrowth set on all files.';
GO

-- ---------------------------------------------------------------------------
-- STEP 5 — Restore FULL recovery if you need point-in-time backups
--           Leave as SIMPLE if this is a dev/test database.
-- ---------------------------------------------------------------------------
-- ALTER DATABASE [s43k_CyclopsCycComms] SET RECOVERY FULL;
-- GO

-- ---------------------------------------------------------------------------
-- Verify: check current file sizes and autogrowth settings
-- ---------------------------------------------------------------------------
SELECT
    name,
    type_desc,
    CAST(size / 128.0 AS DECIMAL(10,1))         AS current_size_MB,
    CASE is_percent_growth
        WHEN 1 THEN CAST(growth AS VARCHAR) + '%'
        ELSE CAST(growth / 128 AS VARCHAR) + ' MB'
    END                                          AS filegrowth,
    CASE max_size
        WHEN -1 THEN 'Unlimited'
        ELSE CAST(max_size / 128 AS VARCHAR) + ' MB'
    END                                          AS max_size
FROM sys.master_files
WHERE database_id = DB_ID(N's43k_CyclopsCycComms');
GO
