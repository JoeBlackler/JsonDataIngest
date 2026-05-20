-- =============================================================================
-- 01_database_settings.sql
-- Apply once to the target database before running any other scripts.
-- SQL Server 2022 targeted.
-- =============================================================================

USE [YourDatabaseName]; -- << replace with your database name
GO

-- ---------------------------------------------------------------------------
-- Read Committed Snapshot Isolation (RCSI)
-- Prevents write operations from blocking concurrent reads.
-- Critical at 120+ docs/hour with constant read traffic.
-- ---------------------------------------------------------------------------
-- ALTER DATABASE [YourDatabaseName]
--    SET READ_COMMITTED_SNAPSHOT ON
--    WITH ROLLBACK AFTER 30 SECONDS;
-- GO
-->


-- ---------------------------------------------------------------------------
-- Snapshot Isolation
-- Allows reporting queries to opt in to non-blocking, row-version reads
-- without changing the default isolation level for all connections.
-- Read queries must explicitly request it:
--   SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
-- Uses the same tempdb version store as RCSI — no extra overhead if RCSI
-- is already enabled.
-- ---------------------------------------------------------------------------
ALTER DATABASE [YourDatabaseName]
    SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

-- ---------------------------------------------------------------------------
-- Async statistics update
-- Prevents query-plan stalls when statistics are rebuilt mid-execution.
-- ---------------------------------------------------------------------------
ALTER DATABASE [YourDatabaseName]
    SET AUTO_UPDATE_STATISTICS_ASYNC ON;
GO

-- ---------------------------------------------------------------------------
-- Query Store — enabled for performance monitoring and plan regression alerts
-- ---------------------------------------------------------------------------
ALTER DATABASE [YourDatabaseName]
    SET QUERY_STORE = ON (
        OPERATION_MODE          = READ_WRITE,
        CLEANUP_POLICY          = (STALE_QUERY_THRESHOLD_DAYS = 30),
        DATA_FLUSH_INTERVAL_SECONDS = 900,
        MAX_STORAGE_SIZE_MB     = 256,
        QUERY_CAPTURE_MODE      = AUTO,
        SIZE_BASED_CLEANUP_MODE = AUTO
    );
GO

-- ---------------------------------------------------------------------------
-- Accelerated Database Recovery (ADR)
-- Reduces recovery time for long-running transactions (large file ingestion).
-- On by default for Azure SQL; explicitly enable for on-prem SQL Server 2022.
-- ---------------------------------------------------------------------------
ALTER DATABASE [YourDatabaseName]
    SET ACCELERATED_DATABASE_RECOVERY = ON;
GO

-- ---------------------------------------------------------------------------
-- Verify settings
-- ---------------------------------------------------------------------------
SELECT
    name,
    is_read_committed_snapshot_on   AS rcsi_enabled,
    is_auto_update_stats_async_on   AS async_stats,
    is_accelerated_database_recovery_on AS adr_enabled
FROM sys.databases
WHERE name = DB_NAME();
GO
