-- =============================================================================
-- 06_reset_data.sql
-- Deletes all ingested data from every cdm table so the same source files
-- can be reprocessed from scratch.
--
-- Deletes in leaf-to-root (FK) order so no constraint violations occur.
-- IDENTITY counters are reset via DBCC CHECKIDENT so JsonFile IDs restart at 1.
--
-- Usage:
--   1.  Run this script in SSMS against s43k_CyclopsCycComms.
--   2.  Move processed files back to the drop folder:
--           Move-Item "C:\ingest\processed\*.json" "C:\ingest\drop\"
--   3.  Restart the container to clear the in-memory seen-file set:
--           docker restart json-ingest
-- =============================================================================

USE [s43k_CyclopsCycComms];
GO

-- ---- leaf tables (no children) ----------------------------------------------
DELETE FROM cdm.EventPartyIdentifier;
DELETE FROM cdm.PartyAttribute;
DELETE FROM cdm.SubscriptionLocation;
DELETE FROM cdm.SubscriptionIdentifier;
DELETE FROM cdm.SubscriptionAttribute;
DELETE FROM cdm.SubscriberLocation;
DELETE FROM cdm.SubscriberIdentifier;
DELETE FROM cdm.SubscriberAttribute;

-- ---- mid-level tables -------------------------------------------------------
DELETE FROM cdm.EventParty;
DELETE FROM cdm.EventAttribute;
DELETE FROM cdm.Subscription;
DELETE FROM cdm.Subscriber;

-- ---- event / location sub-types ---------------------------------------------
DELETE FROM cdm.Event;
DELETE FROM cdm.WifiAccessPoint;
DELETE FROM cdm.PostalAddress;
DELETE FROM cdm.GeoLocation;
DELETE FROM cdm.CellLocation;

-- ---- core tables ------------------------------------------------------------
DELETE FROM cdm.Location;
DELETE FROM cdm.Identifier;
DELETE FROM cdm.JsonFile;

-- ---- reset IDENTITY counters so IDs restart from 1 -------------------------
DBCC CHECKIDENT ('cdm.JsonFile',         RESEED, 0);
DBCC CHECKIDENT ('cdm.Identifier',       RESEED, 0);
DBCC CHECKIDENT ('cdm.Location',         RESEED, 0);
-- CellLocation, GeoLocation, PostalAddress, WifiAccessPoint have no IDENTITY:
-- their PK is Location_ID (FK to cdm.Location), so no reseed is needed.
DBCC CHECKIDENT ('cdm.Event',            RESEED, 0);
DBCC CHECKIDENT ('cdm.EventAttribute',   RESEED, 0);
DBCC CHECKIDENT ('cdm.EventParty',       RESEED, 0);
DBCC CHECKIDENT ('cdm.PartyAttribute',   RESEED, 0);
DBCC CHECKIDENT ('cdm.EventPartyIdentifier', RESEED, 0);
DBCC CHECKIDENT ('cdm.Subscriber',       RESEED, 0);
DBCC CHECKIDENT ('cdm.SubscriberAttribute',  RESEED, 0);
DBCC CHECKIDENT ('cdm.SubscriberIdentifier', RESEED, 0);
DBCC CHECKIDENT ('cdm.SubscriberLocation',   RESEED, 0);
DBCC CHECKIDENT ('cdm.Subscription',         RESEED, 0);
DBCC CHECKIDENT ('cdm.SubscriptionAttribute',RESEED, 0);
DBCC CHECKIDENT ('cdm.SubscriptionIdentifier',RESEED, 0);
DBCC CHECKIDENT ('cdm.SubscriptionLocation', RESEED, 0);

PRINT 'cdm data reset complete. All tables empty, IDENTITY counters at 0.';
GO
