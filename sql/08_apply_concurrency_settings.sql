-- =============================================================================
-- 08_apply_concurrency_settings.sql
-- Apply to an EXISTING database to enable Snapshot Isolation and disable
-- lock escalation on high-volume tables.
--
-- For fresh deployments these settings are already embedded in
-- 01_database_settings.sql and 03_create_tables.sql.
--
-- Safe to run on a live database:
--   * No data changes, no table rebuilds, no blocking of concurrent activity.
--   * Idempotent — safe to run more than once.
-- =============================================================================

USE [s43k_CyclopsCycComms];   -- << replace with your database name if different
GO

-- ---------------------------------------------------------------------------
-- STEP 1 — Allow Snapshot Isolation
-- Enables the row-version store so reporting queries can opt in via:
--   SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
-- Does NOT change the default isolation level for existing queries.
-- ---------------------------------------------------------------------------
IF (SELECT snapshot_isolation_state
    FROM sys.databases WHERE name = DB_NAME()) = 0
BEGIN
    ALTER DATABASE [s43k_CyclopsCycComms] SET ALLOW_SNAPSHOT_ISOLATION ON;
    PRINT 'Snapshot isolation enabled.';
END
ELSE
    PRINT 'Snapshot isolation already enabled — skipped.';
GO

-- ---------------------------------------------------------------------------
-- STEP 2 — Disable lock escalation on high-volume tables
-- By default SQL Server escalates row locks to a table lock when a statement
-- touches ~5,000 rows. A bulk insert that escalates to a table lock blocks
-- ALL concurrent reads for the entire duration of the bulk copy.
-- DISABLE keeps row-level locks, so readers can still access rows that are
-- not part of the current bulk-insert batch.
-- ---------------------------------------------------------------------------
ALTER TABLE cdm.JsonFile              SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.Identifier            SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.Location              SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.Event                 SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.EventAttribute        SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.EventParty            SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.PartyAttribute        SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.EventPartyIdentifier  SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.Subscriber            SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.SubscriberIdentifier  SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.Subscription          SET (LOCK_ESCALATION = DISABLE);
ALTER TABLE cdm.SubscriptionLocation  SET (LOCK_ESCALATION = DISABLE);
PRINT 'Lock escalation disabled on all high-volume tables.';
GO

-- ---------------------------------------------------------------------------
-- Verify — confirm both settings applied correctly
-- ---------------------------------------------------------------------------
SELECT
    SCHEMA_NAME(t.schema_id)         AS schema_name,
    t.name                           AS table_name,
    t.lock_escalation_desc           AS lock_escalation
FROM sys.tables t
WHERE t.schema_id = SCHEMA_ID('cdm')
ORDER BY t.name;

SELECT
    name,
    snapshot_isolation_state_desc    AS snapshot_isolation
FROM sys.databases
WHERE name = DB_NAME();
GO
