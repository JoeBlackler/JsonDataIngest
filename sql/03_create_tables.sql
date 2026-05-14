-- =============================================================================
-- 03_create_tables.sql
-- Creates all 20 cdm schema tables in dependency order.
-- Run after 02_create_schema.sql.
--
-- Design decisions:
--   * Standardised/queryable values are typed columns.
--   * Raw + Composition audit fields are packed into RawData NVARCHAR(MAX) JSON
--     per row, avoiding column explosion while preserving full fidelity.
--   * IngestSeqNum on cdm.EventParty is a per-file monotonic int used only
--     during the ingest transaction to recover IDENTITY values after SqlBulkCopy.
--   * DATA_COMPRESSION = ROW applied to the three highest-volume tables.
--   * All FK constraints are enforced; disable before bulk load and re-enable
--     after if further throughput tuning is required.
-- =============================================================================

USE [YourDatabaseName]; -- << replace with your database name
GO

-- ---------------------------------------------------------------------------
-- 1. cdm.JsonFile
--    Anchor table — one row per ingested JSON file.
--    Folds in cspDisclosureRepresentation and cspResultsFile (both 1:1).
--    References the existing dbo.Document table.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.JsonFile', 'U') IS NULL
CREATE TABLE cdm.JsonFile (
    ID                              BIGINT          IDENTITY(1,1)   NOT NULL,
    Document_ID                     BIGINT          NOT NULL,

    -- Top-level metadata
    SchemaVersion                   VARCHAR(20)     NULL,
    RepresentationType              VARCHAR(60)     NOT NULL
        CONSTRAINT CK_JsonFile_RepType CHECK (RepresentationType IN (
            'Standardised Telephony Events',
            'Standardised Access Events',
            'Standardised Subscriber')),
    ProcessingEngine                VARCHAR(100)    NULL,
    ProcessingEngineVersion         VARCHAR(200)    NULL,
    ProcessingRequestID             VARCHAR(200)    NULL,
    ProcessingCompletionTimestamp   DATETIME2(3)    NULL,
    ProcessedFileID                 VARCHAR(200)    NULL,

    -- CSP Organisation
    CspOrganisationID               VARCHAR(100)    NULL,
    CspOrganisationName             NVARCHAR(300)   NULL,
    CspDisclosureSystemID           VARCHAR(100)    NULL,
    CspDisclosureSystemName         NVARCHAR(300)   NULL,
    CspDisclosureProductID          VARCHAR(100)    NULL,
    CspDisclosureProductName        NVARCHAR(300)   NULL,
    CspDisclosureProductSchemaID    VARCHAR(100)    NULL,
    CspDisclosureProductSchemaName  NVARCHAR(300)   NULL,
    CspDisclosureProductSchemaVersion VARCHAR(50)   NULL,

    -- Authority / request
    OriginalRequestingAuthority     VARCHAR(100)    NULL,
    AuthorisationIdentifier         NVARCHAR(300)   NULL,
    AuthorityRequestID              NVARCHAR(300)   NULL,
    CspRequestCreatedOn             DATETIME2(3)    NULL,
    CdRequestDescription            NVARCHAR(500)   NULL,

    -- CSP Results File
    CspResultsFileID                VARCHAR(200)    NULL,
    CspResultsFileName              NVARCHAR(500)   NULL,
    CspResultsFileCreatedOn         DATETIME2(3)    NULL,
    CspResultsFileFormat            VARCHAR(100)    NULL,
    CspResultsDescription           NVARCHAR(500)   NULL,
    CspResultsFileSizeBytes         BIGINT          NULL,

    -- Overflow: infrequently queried arrays stored as JSON blobs
    RequestParameters               NVARCHAR(MAX)   NULL, -- [{key, value}]
    HashDetails                     NVARCHAR(MAX)   NULL, -- [cdHashDetail]

    -- Ingest audit
    IngestedAt                      DATETIME2(3)    NOT NULL
        CONSTRAINT DF_JsonFile_IngestedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_JsonFile PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_JsonFile_Document
        FOREIGN KEY (Document_ID) REFERENCES dbo.Document(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 2. cdm.Identifier
--    All identifiers extracted from one file. Central to Q2 and Q3.
--    InDocumentID = identifierID value from the source JSON (unique per file).
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.Identifier', 'U') IS NULL
CREATE TABLE cdm.Identifier (
    ID                          BIGINT          IDENTITY(1,1)   NOT NULL,
    JsonFile_ID                 BIGINT          NOT NULL,
    InDocumentID                VARCHAR(200)    NOT NULL, -- identifierID from JSON

    IdentifierRaw               NVARCHAR(500)   NOT NULL,
    IdentifierStandardised      NVARCHAR(500)   NULL,
    IdentifierTypeID            VARCHAR(100)    NOT NULL,
    IdentifierTypeName          NVARCHAR(200)   NOT NULL,
    IdentifierSubType           NVARCHAR(200)   NULL,
    Description                 NVARCHAR(500)   NULL,
    FirstSeen                   DATETIME2(3)    NULL,
    LastSeen                    DATETIME2(3)    NULL,
    ValidFrom                   DATE            NULL,
    ValidTo                     DATE            NULL,
    EventCount                  INT             NULL,

    -- JSON: {identifierRawComposition, identifierStandardisedComposition}
    RawData                     NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_Identifier PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT UQ_Identifier_FileDocId UNIQUE (JsonFile_ID, InDocumentID),
    CONSTRAINT FK_Identifier_JsonFile
        FOREIGN KEY (JsonFile_ID) REFERENCES cdm.JsonFile(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 3. cdm.Location
--    All locations extracted from one file. Sub-type tables hang off this.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.Location', 'U') IS NULL
CREATE TABLE cdm.Location (
    ID              BIGINT          IDENTITY(1,1)   NOT NULL,
    JsonFile_ID     BIGINT          NOT NULL,
    InDocumentID    VARCHAR(200)    NOT NULL, -- locationID from JSON
    FirstSeen       DATETIME2(3)    NULL,
    LastSeen        DATETIME2(3)    NULL,
    ValidFrom       DATE            NULL,
    ValidTo         DATE            NULL,
    EventCount      INT             NULL,

    CONSTRAINT PK_Location PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT UQ_Location_FileDocId UNIQUE (JsonFile_ID, InDocumentID),
    CONSTRAINT FK_Location_JsonFile
        FOREIGN KEY (JsonFile_ID) REFERENCES cdm.JsonFile(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 4. cdm.CellLocation  (1:1 with Location)
--    Standardised cell network values as typed columns.
--    All *Raw and *Composition fields packed into RawData JSON blob.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.CellLocation', 'U') IS NULL
CREATE TABLE cdm.CellLocation (
    Location_ID             BIGINT      NOT NULL, -- PK and FK

    -- Standardised queryable values
    MCCStandardised         VARCHAR(10)     NULL,
    MNCStandardised         VARCHAR(10)     NULL,
    LACStandardised         VARCHAR(20)     NULL,
    CellIDStandardised      VARCHAR(30)     NULL,
    CGIStandardised         VARCHAR(50)     NULL,
    SACStandardised         VARCHAR(20)     NULL,
    AzimuthDegrees          VARCHAR(20)     NULL,
    RATStandardised         VARCHAR(50)     NULL,
    ECellIDStandardised     VARCHAR(30)     NULL,
    ENodeBStandardised      VARCHAR(30)     NULL,
    ECGIStandardised        VARCHAR(50)     NULL,
    TACStandardised         VARCHAR(20)     NULL,

    -- Raw non-standardised values (no standardised equivalent)
    BeamwidthRaw            VARCHAR(30)     NULL,
    RadiatedPowerRaw        VARCHAR(30)     NULL,
    AntennaHeightRaw        VARCHAR(30)     NULL,
    RangeRaw                VARCHAR(30)     NULL,

    -- JSON blob: all *Raw, *RawComposition, *StandardisedComposition fields
    RawData                 NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_CellLocation PRIMARY KEY CLUSTERED (Location_ID),
    CONSTRAINT FK_CellLocation_Location
        FOREIGN KEY (Location_ID) REFERENCES cdm.Location(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 5. cdm.GeoLocation  (1:1 with Location)
--    Lat/Long and OSGB easting/northing as typed columns for spatial queries.
--    SpatialPoint is a persisted computed column (WGS84, SRID 4326).
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.GeoLocation', 'U') IS NULL
CREATE TABLE cdm.GeoLocation (
    Location_ID             BIGINT  NOT NULL,

    LatitudeWGS84           FLOAT   NULL,
    LongitudeWGS84          FLOAT   NULL,
    EastingOSGBMeterRef     INT     NULL,
    NorthingOSGBMeterRef    INT     NULL,

    -- Spatial column: populated by ingestion service via geography::Point()
    -- Use spatial index SPIX_GeoLocation (defined in 04_create_indexes.sql)
    SpatialPoint            GEOGRAPHY   NULL,

    -- JSON blob: all *Raw and *Composition fields
    RawData                 NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_GeoLocation PRIMARY KEY CLUSTERED (Location_ID),
    CONSTRAINT FK_GeoLocation_Location
        FOREIGN KEY (Location_ID) REFERENCES cdm.Location(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 6. cdm.PostalAddress  (1:1 with Location)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.PostalAddress', 'U') IS NULL
CREATE TABLE cdm.PostalAddress (
    Location_ID                 BIGINT          NOT NULL,

    -- Standardised
    FullAddressStandardised     NVARCHAR(500)   NULL,

    -- Raw address components (commonly queried individually)
    BuildingNameRaw             NVARCHAR(200)   NULL,
    BuildingNumberRaw           VARCHAR(20)     NULL,
    FlatNumberRaw               VARCHAR(20)     NULL,
    StreetNameRaw               NVARCHAR(200)   NULL,
    CityRaw                     NVARCHAR(100)   NULL,
    PostcodeRaw                 VARCHAR(20)     NULL,
    PoBoxRaw                    VARCHAR(50)     NULL,
    CountryRaw                  NVARCHAR(100)   NULL,

    -- JSON blob: *RawComposition and *StandardisedComposition fields
    RawData                     NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_PostalAddress PRIMARY KEY CLUSTERED (Location_ID),
    CONSTRAINT FK_PostalAddress_Location
        FOREIGN KEY (Location_ID) REFERENCES cdm.Location(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 7. cdm.WifiAccessPoint  (1:1 with Location)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.WifiAccessPoint', 'U') IS NULL
CREATE TABLE cdm.WifiAccessPoint (
    Location_ID             BIGINT          NOT NULL,
    AccessPointIDRaw        VARCHAR(100)    NULL,
    AccessPointNameRaw      NVARCHAR(200)   NULL,
    SSIDRaw                 NVARCHAR(200)   NULL,

    CONSTRAINT PK_WifiAccessPoint PRIMARY KEY CLUSTERED (Location_ID),
    CONSTRAINT FK_WifiAccessPoint_Location
        FOREIGN KEY (Location_ID) REFERENCES cdm.Location(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 8. cdm.Event
--    One row per event in the source JSON.  High volume.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.Event', 'U') IS NULL
CREATE TABLE cdm.Event (
    ID                  BIGINT          IDENTITY(1,1)   NOT NULL,
    JsonFile_ID         BIGINT          NOT NULL,
    ProcessedEventID    VARCHAR(200)    NULL,
    SourceEventNumber   VARCHAR(200)    NULL,

    EventType           VARCHAR(50)     NOT NULL
        CONSTRAINT CK_Event_EventType CHECK (EventType IN (
            'Call','Call Forwarding','Text(SMS)','Text(MMS)',
            'Access','RADIUS Session','NAT Session','Other','Unknown')),

    EventSuperType      VARCHAR(50)     NOT NULL
        CONSTRAINT CK_Event_SuperType CHECK (EventSuperType IN (
            'Call','Message','Access','Device Event','Other','Unknown')),

    -- JSON blob: {eventTypeRaw, eventTypeRawComposition}
    RawData             NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_Event PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_Event_JsonFile
        FOREIGN KEY (JsonFile_ID) REFERENCES cdm.JsonFile(ID)
) WITH (DATA_COMPRESSION = ROW);
GO

-- ---------------------------------------------------------------------------
-- 9. cdm.EventAttribute
--    Queryable key-value pairs on events.  High volume.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.EventAttribute', 'U') IS NULL
CREATE TABLE cdm.EventAttribute (
    ID          BIGINT          IDENTITY(1,1)   NOT NULL,
    Event_ID    BIGINT          NOT NULL,
    AttrKey     NVARCHAR(200)   NOT NULL,
    AttrValue   NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_EventAttribute PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_EventAttribute_Event
        FOREIGN KEY (Event_ID) REFERENCES cdm.Event(ID)
) WITH (DATA_COMPRESSION = ROW);
GO

-- ---------------------------------------------------------------------------
-- 10. cdm.EventParty
--     Highest-volume table.  One row per party-in-event.
--     IngestSeqNum: monotonic int assigned by ingestion service to each party
--     row within a file.  Used only during the ingest transaction to recover
--     IDENTITY values after SqlBulkCopy (avoids temp-table staging overhead).
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.EventParty', 'U') IS NULL
CREATE TABLE cdm.EventParty (
    ID                      BIGINT          IDENTITY(1,1)   NOT NULL,
    Event_ID                BIGINT          NOT NULL,

    RoleStandardised        VARCHAR(50)     NOT NULL
        CONSTRAINT CK_EventParty_Role CHECK (RoleStandardised IN (
            'Originating-Party','Terminating-Party','Forwarded-to-Party',
            'Access User Equipment','Access Provider','Access Destination',
            'Other','Unknown')),

    -- Typed datetime/duration/data columns for range queries
    StartDateTimeISO8601    DATETIME2(3)    NULL,
    EndDateTimeISO8601      DATETIME2(3)    NULL,
    DurationSeconds         INT             NULL,
    DataReceivedBytes       BIGINT          NULL,
    DataSentBytes           BIGINT          NULL,

    -- Location references (resolved from InDocumentID during ingest)
    StartLocation_ID        BIGINT          NULL,
    EndLocation_ID          BIGINT          NULL,

    -- Ingest-time correlation key; not a business key
    IngestSeqNum            INT             NOT NULL
        CONSTRAINT DF_EventParty_IngestSeqNum DEFAULT 0,

    -- JSON blob: all *Raw, *Composition, roleRaw, roleStandardisedComposition fields
    RawData                 NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_EventParty PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_EventParty_Event
        FOREIGN KEY (Event_ID) REFERENCES cdm.Event(ID),
    CONSTRAINT FK_EventParty_StartLocation
        FOREIGN KEY (StartLocation_ID) REFERENCES cdm.Location(ID),
    CONSTRAINT FK_EventParty_EndLocation
        FOREIGN KEY (EndLocation_ID) REFERENCES cdm.Location(ID)
) WITH (DATA_COMPRESSION = ROW);
GO

-- ---------------------------------------------------------------------------
-- 11. cdm.PartyAttribute
--     Queryable key-value pairs on event parties.  High volume.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.PartyAttribute', 'U') IS NULL
CREATE TABLE cdm.PartyAttribute (
    ID              BIGINT          IDENTITY(1,1)   NOT NULL,
    EventParty_ID   BIGINT          NOT NULL,
    AttrKey         NVARCHAR(200)   NOT NULL,
    AttrValue       NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_PartyAttribute PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_PartyAttribute_EventParty
        FOREIGN KEY (EventParty_ID) REFERENCES cdm.EventParty(ID)
) WITH (DATA_COMPRESSION = ROW);
GO

-- ---------------------------------------------------------------------------
-- 12. cdm.EventPartyIdentifier  (M:N junction)
--     Links event parties to identifiers.  Very high volume.
--     Clustered PK on (EventParty_ID, Identifier_ID) — optimal for the
--     forward lookup from party to identifier.
--     Reverse index defined in 04_create_indexes.sql.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.EventPartyIdentifier', 'U') IS NULL
CREATE TABLE cdm.EventPartyIdentifier (
    EventParty_ID   BIGINT  NOT NULL,
    Identifier_ID   BIGINT  NOT NULL,

    CONSTRAINT PK_EventPartyIdentifier
        PRIMARY KEY CLUSTERED (EventParty_ID, Identifier_ID),
    CONSTRAINT FK_EPI_EventParty
        FOREIGN KEY (EventParty_ID) REFERENCES cdm.EventParty(ID),
    CONSTRAINT FK_EPI_Identifier
        FOREIGN KEY (Identifier_ID) REFERENCES cdm.Identifier(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 13. cdm.Subscriber
--     Entity path — used when RepresentationType = 'Standardised Subscriber'.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.Subscriber', 'U') IS NULL
CREATE TABLE cdm.Subscriber (
    ID                          BIGINT          IDENTITY(1,1)   NOT NULL,
    JsonFile_ID                 BIGINT          NOT NULL,
    InDocumentID                VARCHAR(200)    NOT NULL, -- subscriberID from JSON

    -- Standardised
    SubscriberNameRaw           NVARCHAR(500)   NOT NULL,
    SubscriberNameStandardised  NVARCHAR(500)   NULL,
    DateOfBirth                 DATE            NULL,

    -- Raw personal fields (no standardised equivalent)
    GenderRaw                   VARCHAR(50)     NULL,
    SalutationRaw               VARCHAR(50)     NULL,
    FirstNameRaw                NVARCHAR(200)   NULL,
    SecondNameRaw               NVARCHAR(200)   NULL,
    MiddleNamesRaw              NVARCHAR(200)   NULL,
    SurnameRaw                  NVARCHAR(200)   NULL,
    OrganisationNameRaw         NVARCHAR(500)   NULL,
    ProfessionRaw               NVARCHAR(200)   NULL,

    -- JSON blob: all *Composition and *Standardised composition fields
    RawData                     NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_Subscriber PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT UQ_Subscriber_FileDocId UNIQUE (JsonFile_ID, InDocumentID),
    CONSTRAINT FK_Subscriber_JsonFile
        FOREIGN KEY (JsonFile_ID) REFERENCES cdm.JsonFile(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 14. cdm.SubscriberAttribute  (queryable key-value on subscribers)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.SubscriberAttribute', 'U') IS NULL
CREATE TABLE cdm.SubscriberAttribute (
    ID              BIGINT          IDENTITY(1,1)   NOT NULL,
    Subscriber_ID   BIGINT          NOT NULL,
    AttrKey         NVARCHAR(200)   NOT NULL,
    AttrValue       NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_SubscriberAttribute PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_SubscriberAttr_Subscriber
        FOREIGN KEY (Subscriber_ID) REFERENCES cdm.Subscriber(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 15. cdm.SubscriberIdentifier  (M:N junction)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.SubscriberIdentifier', 'U') IS NULL
CREATE TABLE cdm.SubscriberIdentifier (
    Subscriber_ID   BIGINT  NOT NULL,
    Identifier_ID   BIGINT  NOT NULL,

    CONSTRAINT PK_SubscriberIdentifier
        PRIMARY KEY CLUSTERED (Subscriber_ID, Identifier_ID),
    CONSTRAINT FK_SubI_Subscriber
        FOREIGN KEY (Subscriber_ID) REFERENCES cdm.Subscriber(ID),
    CONSTRAINT FK_SubI_Identifier
        FOREIGN KEY (Identifier_ID) REFERENCES cdm.Identifier(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 16. cdm.SubscriberLocation
--     Date-ranged link from subscriber to a location record.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.SubscriberLocation', 'U') IS NULL
CREATE TABLE cdm.SubscriberLocation (
    ID              BIGINT          IDENTITY(1,1)   NOT NULL,
    Subscriber_ID   BIGINT          NOT NULL,
    Location_ID     BIGINT          NOT NULL,
    StartDate       DATE            NULL,
    EndDate         DATE            NULL,
    Role            NVARCHAR(100)   NULL,

    CONSTRAINT PK_SubscriberLocation PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_SubLoc_Subscriber
        FOREIGN KEY (Subscriber_ID) REFERENCES cdm.Subscriber(ID),
    CONSTRAINT FK_SubLoc_Location
        FOREIGN KEY (Location_ID) REFERENCES cdm.Location(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 17. cdm.Subscription
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.Subscription', 'U') IS NULL
CREATE TABLE cdm.Subscription (
    ID                              BIGINT          IDENTITY(1,1)   NOT NULL,
    Subscriber_ID                   BIGINT          NOT NULL,
    InDocumentID                    VARCHAR(200)    NOT NULL, -- subscriptionID from JSON

    SubscriptionTypeRaw             NVARCHAR(200)   NULL,
    SubscriptionActivationDate      DATE            NULL,
    SubscriptionDeactivationDate    DATE            NULL,
    SubscriptionDeactivationReason  NVARCHAR(500)   NULL,

    -- JSON blob: all *Raw/*Composition date fields + subscriptionAttribute
    RawData                         NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_Subscription PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_Subscription_Subscriber
        FOREIGN KEY (Subscriber_ID) REFERENCES cdm.Subscriber(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 18. cdm.SubscriptionAttribute  (queryable key-value on subscriptions)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.SubscriptionAttribute', 'U') IS NULL
CREATE TABLE cdm.SubscriptionAttribute (
    ID              BIGINT          IDENTITY(1,1)   NOT NULL,
    Subscription_ID BIGINT          NOT NULL,
    AttrKey         NVARCHAR(200)   NOT NULL,
    AttrValue       NVARCHAR(MAX)   NULL,

    CONSTRAINT PK_SubscriptionAttribute PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_SubscriptionAttr_Subscription
        FOREIGN KEY (Subscription_ID) REFERENCES cdm.Subscription(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 19. cdm.SubscriptionIdentifier  (M:N junction)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.SubscriptionIdentifier', 'U') IS NULL
CREATE TABLE cdm.SubscriptionIdentifier (
    Subscription_ID BIGINT  NOT NULL,
    Identifier_ID   BIGINT  NOT NULL,

    CONSTRAINT PK_SubscriptionIdentifier
        PRIMARY KEY CLUSTERED (Subscription_ID, Identifier_ID),
    CONSTRAINT FK_SubsI_Subscription
        FOREIGN KEY (Subscription_ID) REFERENCES cdm.Subscription(ID),
    CONSTRAINT FK_SubsI_Identifier
        FOREIGN KEY (Identifier_ID) REFERENCES cdm.Identifier(ID)
);
GO

-- ---------------------------------------------------------------------------
-- 20. cdm.SubscriptionLocation
--     datetime2(3) for start/end (schema specifies date-time format).
-- ---------------------------------------------------------------------------
IF OBJECT_ID('cdm.SubscriptionLocation', 'U') IS NULL
CREATE TABLE cdm.SubscriptionLocation (
    ID              BIGINT          IDENTITY(1,1)   NOT NULL,
    Subscription_ID BIGINT          NOT NULL,
    Location_ID     BIGINT          NOT NULL,
    StartDatetime   DATETIME2(3)    NULL,
    EndDatetime     DATETIME2(3)    NULL,
    Role            NVARCHAR(100)   NULL,

    CONSTRAINT PK_SubscriptionLocation PRIMARY KEY CLUSTERED (ID),
    CONSTRAINT FK_SubsLoc_Subscription
        FOREIGN KEY (Subscription_ID) REFERENCES cdm.Subscription(ID),
    CONSTRAINT FK_SubsLoc_Location
        FOREIGN KEY (Location_ID) REFERENCES cdm.Location(ID)
);
GO

-- ---------------------------------------------------------------------------
-- User-Defined Table Type: cdm.BigIntList
-- Used by the ingestion service as a TVP to pass sets of BIGINT IDs to
-- parameterised queries, avoiding dynamic SQL string interpolation.
-- ---------------------------------------------------------------------------
IF TYPE_ID(N'cdm.BigIntList') IS NULL
BEGIN
    EXEC('CREATE TYPE cdm.BigIntList AS TABLE (ID BIGINT NOT NULL PRIMARY KEY)');
END
GO

PRINT 'All 20 cdm tables and supporting types created successfully.';
GO
