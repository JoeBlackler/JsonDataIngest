-- =============================================================================
-- 05_sample_queries.sql
-- Required queries Q1, Q2, Q3 plus supplementary helpers.
-- All queries enforce user-level security via ReferenceSecurity.
-- =============================================================================

USE [YourDatabaseName]; -- << replace with your database name
GO

-- ---------------------------------------------------------------------------
-- PARAMETERS (declare once; reuse across all queries in the same batch)
-- ---------------------------------------------------------------------------
DECLARE @Username       NVARCHAR(200)   = N'joe.blackler';  -- maps to dbo.ReferenceSecurity.szUser
DECLARE @ReferenceID    BIGINT          = 42;       -- "application" = Reference record
DECLARE @JsonFileID     BIGINT          = 1001;     -- specific JSON document
DECLARE @IdentifierValue NVARCHAR(500)  = N'+447700900123'; -- standardised identifier

-- =============================================================================
-- Q1 — All events within an application
--      An "application" maps to one Reference record, which owns one or more
--      Documents, each of which owns one or more JsonFiles.
--
-- Index path:
--   IX_JsonFile_Document  SEEK (Document_ID set)
--   IX_Event_JsonFile     SEEK (JsonFile_ID set)
--   IX_EventParty_Event   SEEK (Event_ID set)
--   IX_EPI_IdentifierReverse (optional; LEFT JOIN handled efficiently)
-- =============================================================================
SELECT
    jf.ID                           AS JsonFileID,
    jf.ProcessedFileID,
    jf.ProcessingCompletionTimestamp,
    jf.AuthorisationIdentifier,
    jf.CspOrganisationName,
    jf.RepresentationType,

    e.ProcessedEventID,
    e.SourceEventNumber,
    e.EventType,
    e.EventSuperType,

    ep.RoleStandardised,
    ep.StartDateTimeISO8601,
    ep.EndDateTimeISO8601,
    ep.DurationSeconds,
    ep.DataReceivedBytes,
    ep.DataSentBytes,

    i.IdentifierRaw,
    i.IdentifierStandardised,
    i.IdentifierTypeName,
    i.IdentifierTypeID

FROM        cdm.JsonFile            jf
INNER JOIN  dbo.Document            d   ON  d.ID            = jf.Document_ID
INNER JOIN  dbo.Reference           r   ON  r.ID            = d.Reference_ID
INNER JOIN  dbo.ReferenceSecurity   rs  ON  rs.Reference_ID = r.ID
INNER JOIN  cdm.Event               e   ON  e.JsonFile_ID   = jf.ID
INNER JOIN  cdm.EventParty          ep  ON  ep.Event_ID     = e.ID
LEFT  JOIN  cdm.EventPartyIdentifier epi ON epi.EventParty_ID = ep.ID
LEFT  JOIN  cdm.Identifier          i   ON  i.ID            = epi.Identifier_ID

WHERE   r.ID            = @ReferenceID
  AND   rs.szUser       = @Username

ORDER BY jf.ID, e.ID, ep.ID;
GO

-- =============================================================================
-- Q2 — All occurrences of a specific identifier across the entire accessible
--      dataset (must be very quick).
--
-- Index path:
--   IX_Identifier_Standardised  SEEK on IdentifierStandardised  (primary seek)
--   IX_JsonFile_Document        SEEK to resolve Document_ID
--   ReferenceSecurity filtered  after the seek; hot rows, small resultset
--
-- Note: to search by raw (non-standardised) value, substitute
--   i.IdentifierRaw = @IdentifierValue   (uses IX_Identifier_Raw)
-- =============================================================================
SELECT
    i.IdentifierRaw,
    i.IdentifierStandardised,
    i.IdentifierTypeName,
    i.IdentifierTypeID,
    i.IdentifierSubType,
    i.Description,
    i.FirstSeen,
    i.LastSeen,
    i.EventCount,

    jf.ID                           AS JsonFileID,
    jf.ProcessedFileID,
    jf.ProcessingCompletionTimestamp,
    jf.AuthorisationIdentifier,
    jf.CspOrganisationName,

    d.ID                            AS DocumentID,
    r.ID                            AS ReferenceID

FROM        cdm.Identifier          i
INNER JOIN  cdm.JsonFile            jf  ON  jf.ID           = i.JsonFile_ID
INNER JOIN  dbo.Document            d   ON  d.ID            = jf.Document_ID
INNER JOIN  dbo.Reference           r   ON  r.ID            = d.Reference_ID
INNER JOIN  dbo.ReferenceSecurity   rs  ON  rs.Reference_ID = r.ID

WHERE   i.IdentifierStandardised    = @IdentifierValue  -- IX_Identifier_Standardised SEEK
  AND   rs.szUser                   = @Username;
GO

-- =============================================================================
-- Q3 — Unique identifiers from one specific JSON document.
--
-- Index path:
--   IX_Identifier_JsonFile  SEEK on JsonFile_ID (covering; all needed columns
--                           are in the INCLUDE list)
--   Security check via Document → ReferenceSecurity (narrow seek)
-- =============================================================================
SELECT
    i.IdentifierRaw,
    i.IdentifierStandardised,
    i.IdentifierTypeName,
    i.IdentifierTypeID,
    i.IdentifierSubType,
    i.Description,
    i.FirstSeen,
    i.LastSeen,
    i.ValidFrom,
    i.ValidTo,
    i.EventCount

FROM        cdm.Identifier          i
INNER JOIN  cdm.JsonFile            jf  ON  jf.ID           = i.JsonFile_ID
INNER JOIN  dbo.Document            d   ON  d.ID            = jf.Document_ID
INNER JOIN  dbo.ReferenceSecurity   rs  ON  rs.Reference_ID = d.Reference_ID

WHERE   jf.ID           = @JsonFileID     -- IX_Identifier_JsonFile SEEK
  AND   rs.szUser       = @Username;
GO

-- =============================================================================
-- SUPPLEMENTARY: Q1-S — Events within an application WITH location detail
--                       (extend Q1 to include cell and geo coordinates)
-- =============================================================================
SELECT
    jf.ProcessedFileID,
    e.ProcessedEventID,
    e.EventType,
    ep.RoleStandardised,
    ep.StartDateTimeISO8601,
    ep.DurationSeconds,
    i.IdentifierStandardised,
    i.IdentifierTypeName,

    -- Start location
    cl_s.CGIStandardised            AS StartCGI,
    cl_s.MCCStandardised            AS StartMCC,
    cl_s.MNCStandardised            AS StartMNC,
    cl_s.LACStandardised            AS StartLAC,
    cl_s.CellIDStandardised         AS StartCellID,
    cl_s.RATStandardised            AS StartRAT,
    cl_s.AzimuthDegrees             AS StartAzimuth,
    geo_s.LatitudeWGS84             AS StartLat,
    geo_s.LongitudeWGS84            AS StartLon,

    -- End location
    cl_e.CGIStandardised            AS EndCGI,
    geo_e.LatitudeWGS84             AS EndLat,
    geo_e.LongitudeWGS84            AS EndLon

FROM        cdm.JsonFile            jf
INNER JOIN  dbo.Document            d       ON  d.ID            = jf.Document_ID
INNER JOIN  dbo.Reference           r       ON  r.ID            = d.Reference_ID
INNER JOIN  dbo.ReferenceSecurity   rs      ON  rs.Reference_ID = r.ID
INNER JOIN  cdm.Event               e       ON  e.JsonFile_ID   = jf.ID
INNER JOIN  cdm.EventParty          ep      ON  ep.Event_ID     = e.ID
LEFT  JOIN  cdm.EventPartyIdentifier epi    ON  epi.EventParty_ID = ep.ID
LEFT  JOIN  cdm.Identifier          i       ON  i.ID            = epi.Identifier_ID
LEFT  JOIN  cdm.Location            loc_s   ON  loc_s.ID        = ep.StartLocation_ID
LEFT  JOIN  cdm.CellLocation        cl_s    ON  cl_s.Location_ID = loc_s.ID
LEFT  JOIN  cdm.GeoLocation         geo_s   ON  geo_s.Location_ID = loc_s.ID
LEFT  JOIN  cdm.Location            loc_e   ON  loc_e.ID        = ep.EndLocation_ID
LEFT  JOIN  cdm.CellLocation        cl_e    ON  cl_e.Location_ID = loc_e.ID
LEFT  JOIN  cdm.GeoLocation         geo_e   ON  geo_e.Location_ID = loc_e.ID

WHERE   r.ID        = @ReferenceID
  AND   rs.szUser   = @Username

ORDER BY jf.ID, e.ID, ep.ID;
GO

-- =============================================================================
-- SUPPLEMENTARY: Q2-S — Identifier occurrences WITH event party context
--                        (extend Q2 to include the events that reference it)
-- =============================================================================
SELECT
    i.IdentifierStandardised,
    i.IdentifierTypeName,
    i.FirstSeen,
    i.LastSeen,
    i.EventCount,

    jf.ProcessedFileID,
    jf.AuthorisationIdentifier,

    e.ProcessedEventID,
    e.EventType,
    e.EventSuperType,
    ep.RoleStandardised,
    ep.StartDateTimeISO8601,
    ep.DurationSeconds

FROM        cdm.Identifier          i
INNER JOIN  cdm.JsonFile            jf  ON  jf.ID               = i.JsonFile_ID
INNER JOIN  dbo.Document            d   ON  d.ID                = jf.Document_ID
INNER JOIN  dbo.Reference           r   ON  r.ID                = d.Reference_ID
INNER JOIN  dbo.ReferenceSecurity   rs  ON  rs.Reference_ID     = r.ID
-- Join back to events that reference this identifier
INNER JOIN  cdm.EventPartyIdentifier epi ON epi.Identifier_ID   = i.ID
INNER JOIN  cdm.EventParty          ep  ON  ep.ID               = epi.EventParty_ID
INNER JOIN  cdm.Event               e   ON  e.ID                = ep.Event_ID

WHERE   i.IdentifierStandardised    = @IdentifierValue
  AND   rs.szUser                   = @Username

ORDER BY jf.ID, e.ID;
GO

-- =============================================================================
-- SUPPLEMENTARY: Q-SUB — All subscriber detail for an application
-- =============================================================================
SELECT
    jf.ProcessedFileID,
    jf.AuthorisationIdentifier,
    sub.InDocumentID            AS SubscriberID,
    sub.SubscriberNameStandardised,
    sub.DateOfBirth,
    sub.GenderRaw,
    sub.OrganisationNameRaw,

    subs.InDocumentID           AS SubscriptionID,
    subs.SubscriptionTypeRaw,
    subs.SubscriptionActivationDate,
    subs.SubscriptionDeactivationDate,

    i.IdentifierStandardised,
    i.IdentifierTypeName

FROM        cdm.JsonFile            jf
INNER JOIN  dbo.Document            d       ON  d.ID            = jf.Document_ID
INNER JOIN  dbo.Reference           r       ON  r.ID            = d.Reference_ID
INNER JOIN  dbo.ReferenceSecurity   rs      ON  rs.Reference_ID = r.ID
INNER JOIN  cdm.Subscriber          sub     ON  sub.JsonFile_ID = jf.ID
LEFT  JOIN  cdm.Subscription        subs    ON  subs.Subscriber_ID = sub.ID
LEFT  JOIN  cdm.SubscriberIdentifier si     ON  si.Subscriber_ID = sub.ID
LEFT  JOIN  cdm.Identifier          i       ON  i.ID            = si.Identifier_ID

WHERE   r.ID        = @ReferenceID
  AND   rs.szUser   = @Username

ORDER BY jf.ID, sub.ID, subs.ID;
GO
