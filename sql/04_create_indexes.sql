-- =============================================================================
-- 04_create_indexes.sql
-- All non-clustered indexes for the cdm schema.
-- Run after 03_create_tables.sql.
--
-- Index naming: IX_<Table>_<Purpose>
-- FILLFACTOR = 85 on heavily-inserted tables reduces page splits under load.
-- =============================================================================

USE [YourDatabaseName]; -- << replace with your database name
GO

-- ===========================================================================
-- cdm.JsonFile
-- ===========================================================================

-- Powers the Document→Reference→ReferenceSecurity security join used in
-- every query.  Covering includes the most commonly projected metadata columns.
CREATE NONCLUSTERED INDEX IX_JsonFile_Document
ON cdm.JsonFile (Document_ID)
INCLUDE (RepresentationType, ProcessingCompletionTimestamp,
         AuthorisationIdentifier, CspOrganisationName, ProcessedFileID)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.Identifier  (critical — used in Q2 and Q3)
-- ===========================================================================

-- Q2: All occurrences of a specific identifier across the accessible dataset.
-- Narrow seek on IdentifierStandardised; includes all commonly projected columns.
CREATE NONCLUSTERED INDEX IX_Identifier_Standardised
ON cdm.Identifier (IdentifierStandardised)
INCLUDE (JsonFile_ID, IdentifierTypeName, IdentifierTypeID,
         IdentifierSubType, FirstSeen, LastSeen, EventCount)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- Q2 (fallback): lookup by raw value when standardised is unavailable.
CREATE NONCLUSTERED INDEX IX_Identifier_Raw
ON cdm.Identifier (IdentifierRaw)
INCLUDE (JsonFile_ID, IdentifierTypeName, IdentifierTypeID)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- Q3: All unique identifiers from one specific JSON document.
-- Seek on JsonFile_ID; includes all columns needed for the result set.
CREATE NONCLUSTERED INDEX IX_Identifier_JsonFile
ON cdm.Identifier (JsonFile_ID)
INCLUDE (IdentifierRaw, IdentifierStandardised, IdentifierTypeName,
         IdentifierTypeID, IdentifierSubType, Description,
         FirstSeen, LastSeen, EventCount, ValidFrom, ValidTo)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.Location
-- ===========================================================================

CREATE NONCLUSTERED INDEX IX_Location_JsonFile
ON cdm.Location (JsonFile_ID)
INCLUDE (InDocumentID, FirstSeen, LastSeen, EventCount)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.Event
-- ===========================================================================

-- Q1: Enumerate all events belonging to a file (= all files in an application).
CREATE NONCLUSTERED INDEX IX_Event_JsonFile
ON cdm.Event (JsonFile_ID)
INCLUDE (EventType, EventSuperType, ProcessedEventID, SourceEventNumber)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.EventParty
-- ===========================================================================

-- Q1: Fetch parties for a given event including the most-projected columns.
CREATE NONCLUSTERED INDEX IX_EventParty_Event
ON cdm.EventParty (Event_ID)
INCLUDE (StartDateTimeISO8601, EndDateTimeISO8601, DurationSeconds,
         RoleStandardised, DataReceivedBytes, DataSentBytes,
         StartLocation_ID, EndLocation_ID)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- Ingest recovery: quickly retrieve (ID, IngestSeqNum) pairs after bulk insert
-- so the pipeline can build junction rows without a temp-table staging step.
-- Scoped by Event.JsonFile_ID via the IX_Event_JsonFile index.
CREATE NONCLUSTERED INDEX IX_EventParty_IngestSeqNum
ON cdm.EventParty (Event_ID, IngestSeqNum)
INCLUDE (ID)
WITH (FILLFACTOR = 90, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.EventPartyIdentifier  (M:N junction)
-- ===========================================================================

-- Reverse lookup: from an identifier, find all event parties associated with it.
-- Used when expanding results of Q2 to include event context.
CREATE NONCLUSTERED INDEX IX_EPI_IdentifierReverse
ON cdm.EventPartyIdentifier (Identifier_ID, EventParty_ID)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.EventAttribute
-- ===========================================================================

CREATE NONCLUSTERED INDEX IX_EventAttr_Event
ON cdm.EventAttribute (Event_ID, AttrKey)
INCLUDE (AttrValue)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.PartyAttribute
-- ===========================================================================

CREATE NONCLUSTERED INDEX IX_PartyAttr_Party
ON cdm.PartyAttribute (EventParty_ID, AttrKey)
INCLUDE (AttrValue)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.Subscriber
-- ===========================================================================

CREATE NONCLUSTERED INDEX IX_Subscriber_JsonFile
ON cdm.Subscriber (JsonFile_ID)
INCLUDE (SubscriberNameStandardised, DateOfBirth, InDocumentID)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.SubscriberIdentifier  (M:N junction)
-- ===========================================================================

CREATE NONCLUSTERED INDEX IX_SubI_IdentifierReverse
ON cdm.SubscriberIdentifier (Identifier_ID, Subscriber_ID)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.Subscription
-- ===========================================================================

CREATE NONCLUSTERED INDEX IX_Subscription_Subscriber
ON cdm.Subscription (Subscriber_ID)
INCLUDE (SubscriptionActivationDate, SubscriptionDeactivationDate,
         SubscriptionTypeRaw, InDocumentID)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.SubscriptionIdentifier  (M:N junction)
-- ===========================================================================

CREATE NONCLUSTERED INDEX IX_SubsI_IdentifierReverse
ON cdm.SubscriptionIdentifier (Identifier_ID, Subscription_ID)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.SubscriberAttribute / cdm.SubscriptionAttribute
-- ===========================================================================

CREATE NONCLUSTERED INDEX IX_SubscriberAttr_Key
ON cdm.SubscriberAttribute (Subscriber_ID, AttrKey)
INCLUDE (AttrValue)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

CREATE NONCLUSTERED INDEX IX_SubscriptionAttr_Key
ON cdm.SubscriptionAttribute (Subscription_ID, AttrKey)
INCLUDE (AttrValue)
WITH (FILLFACTOR = 85, ONLINE = ON);
GO

-- ===========================================================================
-- cdm.GeoLocation  — spatial index (optional; enable if spatial queries used)
-- ===========================================================================

-- Uncomment if proximity/bounding-box queries are required against SpatialPoint.
--
-- CREATE SPATIAL INDEX SPIX_GeoLocation_SpatialPoint
-- ON cdm.GeoLocation (SpatialPoint)
-- USING GEOGRAPHY_GRID
-- WITH (
--     GRIDS       = (MEDIUM, MEDIUM, MEDIUM, MEDIUM),
--     CELLS_PER_OBJECT = 16,
--     PAD_INDEX   = OFF
-- );
-- GO

PRINT 'All cdm indexes created successfully.';
GO
