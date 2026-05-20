# JsonDataIngest

A .NET 8 background worker service that ingests standardised CSP (Communications Service Provider) disclosure JSON files into a SQL Server database. The service is designed for high-throughput, transactional bulk loading and is deployable as a Docker container in Kubernetes or standalone environments.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Input Format](#input-format)
- [Ingestion Pipeline](#ingestion-pipeline)
- [Database Structure](#database-structure)
- [Indexes](#indexes)
- [Sample Queries](#sample-queries)
- [Configuration](#configuration)
- [SQL Setup Scripts](#sql-setup-scripts)
- [Docker Deployment](#docker-deployment)
- [Volume / Load Testing](#volume--load-testing)
- [Maintenance Scripts](#maintenance-scripts)
- [Dependencies](#dependencies)

---

## Overview

`JsonIngestService` watches a file-drop folder for incoming JSON files, parses each file, and bulk-inserts all entities (identifiers, locations, events, subscribers) into a SQL Server database within a single atomic transaction. Files that process successfully are moved to a `processed/` folder; files that fail are moved to an `error/` folder and logged.

The service is built around two representation types that map to the two main data paths:

| Representation Type | Description |
|---|---|
| `Standardised Telephony Events` | Telephony call/text/access events with event parties |
| `Standardised Access Events` | Internet access events with event parties |
| `Standardised Subscriber` | Subscriber identity and subscription records |

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                    IngestWorker                      │
│  (BackgroundService — Kubernetes/Docker compatible)  │
│   SemaphoreSlim → MaxConcurrentFiles parallel slots  │
└───────────────────┬─────────────────────────────────┘
                    │
          ┌─────────▼──────────┐
          │ FileDropIngestionSource │
          │  Polling loop (no FSW)  │
          │  Channel<IngestionRequest> (bounded 50) │
          └─────────┬──────────┘
                    │  {filePath, documentId}
          ┌─────────▼──────────┐
          │ IngestionOrchestrator │
          │  Opens connection     │
          │  Begins transaction   │
          │  Retry (up to 3×)     │
          └──┬──────────────────┘
             │
     ┌───────┼───────────────────────────────────┐
     │       │                                   │
     ▼       ▼                                   ▼
JsonFileInserter  IdentifierProcessor    LocationProcessor
(anchor row)      (bulk cdm.Identifier)  (bulk cdm.Location +
                                          CellLocation, GeoLocation,
                                          PostalAddress, WifiAccessPoint)
                                                  │
                              ┌───────────────────┴────────────────┐
                              ▼                                     ▼
                     EventPathProcessor               SubscriberPathProcessor
                     (Event, EventAttribute,          (Subscriber, SubscriberAttribute,
                      EventParty, PartyAttribute,      SubscriberIdentifier,
                      EventPartyIdentifier)             SubscriberLocation,
                                                        Subscription,
                                                        SubscriptionAttribute,
                                                        SubscriptionIdentifier,
                                                        SubscriptionLocation)
```

**Key design decisions:**

- `FileSystemWatcher` is **not used** — inotify events are not propagated into Linux containers on Docker Desktop / Windows bind-mounts. A polling loop with a configurable interval is used instead.
- All inserts use `SqlBulkCopy` for throughput. Column-name mapping is used so DataTable column order is irrelevant.
- Each file is wrapped in a single SQL Server transaction. On failure the transaction is rolled back and the file is moved to the error folder.
- Up to 3 automatic retries are attempted with exponential back-off (2 s, 4 s) before a file is declared failed.
- `IDENTITY` values are recovered after bulk insert by seeking on unique constraints (e.g. `(JsonFile_ID, InDocumentID)`), avoiding the overhead of a staging temp-table.
- `IngestSeqNum` on `cdm.EventParty` is a per-file monotonic integer assigned during the ingestion transaction to recover `EventParty` identity values for junction row construction.

---

## Project Structure

```
JsonDataIngest.sln
│
├── scripts/
│   └── Generate-VolumeTestFiles.ps1   # Throughput testing helper
│
├── sql/
│   ├── 01_database_settings.sql       # Snapshot isolation, ADR, Query Store
│   ├── 02_create_schema.sql           # Creates the cdm schema
│   ├── 03_create_tables.sql           # Creates all 20 cdm tables
│   ├── 04_create_indexes.sql          # All non-clustered indexes
│   ├── 05_sample_queries.sql          # Q1, Q2, Q3 + supplementary queries
│   ├── 06_reset_data.sql              # Truncates data for reprocessing
│   ├── 07_recovery_disk_full.sql      # Disk-full recovery procedures
│   └── 08_apply_concurrency_settings.sql  # Concurrency tuning
│
└── src/
    └── JsonIngestService/
        ├── Program.cs                 # Host builder, DI registration
        ├── appsettings.json           # Default configuration
        ├── Dockerfile
        │
        ├── DataAccess/
        │   ├── BulkCopyHelper.cs      # SqlBulkCopy wrapper
        │   ├── DbConnectionFactory.cs
        │   └── IDbConnectionFactory.cs
        │
        ├── Models/
        │   ├── Root/                  # Top-level JSON deserialization models
        │   ├── Events/                # EventModel, EventPartyModel
        │   ├── Subscribers/           # SubscriberModel, SubscriptionModel
        │   └── Shared/                # IdentifierModel, LocationModel, sub-types
        │
        ├── Pipeline/
        │   ├── IngestionOrchestrator.cs
        │   ├── JsonFileInserter.cs
        │   ├── IdentifierProcessor.cs
        │   ├── LocationProcessor.cs
        │   ├── EventPathProcessor.cs
        │   └── SubscriberPathProcessor.cs
        │
        └── Worker/
            ├── IngestWorker.cs        # BackgroundService entry point
            ├── FileDropIngestionSource.cs
            ├── IIngestionSource.cs
            └── IngestOptions.cs       # Typed configuration classes
```

---

## Input Format

### File Naming Convention

Files must be named using the pattern:

```
{documentId}_{anything}.json
```

For example: `4821_standardised_events.json`

The numeric prefix is parsed as the `dbo.Document.ID` foreign key, which must already exist in the database before the file is processed. The remainder of the filename is ignored.

### JSON Schema

All files share a common root wrapper:

```json
{
  "standardisedRepresentation": {
    "standardisedRepresentationSchemaVersion": "...",
    "standardisedRepresentationType": "Standardised Telephony Events",
    "processingEngine": "...",
    "processingEngineVersion": "...",
    "processingRequestID": "...",
    "processingCompletionTimestamp": "2024-01-15T10:30:00Z",
    "processedFileID": "...",
    "cspDisclosureRepresentation": { ... },
    "identifier": [ ... ],
    "location":   [ ... ]
  }
}
```

#### `cspDisclosureRepresentation`

Contains CSP organisation metadata, authorisation details, request parameters, results file metadata, and the `standardisedType` object which holds either events or subscribers.

#### `identifier[]`

Each identifier represents a communications identifier (phone number, IMEI, IMSI, IP address, etc.):

```json
{
  "identifierID": "id-001",
  "identifierRaw": "+447700900123",
  "identifierStandardised": "+447700900123",
  "identifierTypeID": "MSISDN",
  "identifierTypeName": "MSISDN",
  "identifierSubType": null,
  "firstSeen": "2024-01-01T00:00:00Z",
  "lastSeen": "2024-01-15T00:00:00Z",
  "eventCount": 42
}
```

#### `location[]`

Each location may contain one or more sub-type objects:

| Sub-type | Description |
|---|---|
| `cellLocation` | Cell tower identity (MCC, MNC, LAC, Cell ID, CGI, RAT, azimuth, etc.) |
| `geoLocation` | WGS84 latitude/longitude or OSGB easting/northing |
| `postalAddress` | Postal address components |
| `wifiAccessPoint` | Wi-Fi access point ID/SSID |

#### Event path (`standardisedEventParty.events[]`)

Used when `RepresentationType` is `Standardised Telephony Events` or `Standardised Access Events`:

```
Event
  ├── eventAttribute[]           (key-value pairs)
  └── eventParty[]
        ├── partyIdentifierID[]  (references to identifier[].identifierID)
        ├── startLocationID      (reference to location[].locationID)
        ├── endLocationID
        ├── startDateTimeISO8601
        ├── endDateTimeISO8601
        ├── durationSeconds
        ├── dataReceivedBytes
        ├── dataSentBytes
        └── partyAttribute[]     (key-value pairs)
```

Valid `EventType` values: `Call`, `Call Forwarding`, `Text(SMS)`, `Text(MMS)`, `Access`, `RADIUS Session`, `NAT Session`, `Other`, `Unknown`

Valid `RoleStandardised` values: `Originating-Party`, `Terminating-Party`, `Forwarded-to-Party`, `Access User Equipment`, `Access Provider`, `Access Destination`, `Other`, `Unknown`

#### Subscriber path (`standardisedEntitySubscriber.subscribers[]`)

Used when `RepresentationType` is `Standardised Subscriber`:

```
Subscriber
  ├── subscriberAttribute[]      (key-value pairs)
  ├── partyIdentifierID[]        (references to identifier[])
  ├── subscriberLocation[]       (date-ranged location references)
  └── subscription[]
        ├── subscriptionAttribute[]
        ├── partyIdentifierID[]
        └── subscriptionLocation[]
```

---

## Ingestion Pipeline

The `IngestionOrchestrator` processes each file in the following steps within a single SQL Server transaction:

| Step | Component | Tables written |
|---|---|---|
| 1 | `JsonFileInserter` | `cdm.JsonFile` (anchor row) |
| 2 | `IdentifierProcessor` | `cdm.Identifier` |
| 3 | `LocationProcessor` | `cdm.Location`, `cdm.CellLocation`, `cdm.GeoLocation`, `cdm.PostalAddress`, `cdm.WifiAccessPoint` |
| 4a | `EventPathProcessor` | `cdm.Event`, `cdm.EventAttribute`, `cdm.EventParty`, `cdm.PartyAttribute`, `cdm.EventPartyIdentifier` |
| 4b | `SubscriberPathProcessor` | `cdm.Subscriber`, `cdm.SubscriberAttribute`, `cdm.SubscriberIdentifier`, `cdm.SubscriberLocation`, `cdm.Subscription`, `cdm.SubscriptionAttribute`, `cdm.SubscriptionIdentifier`, `cdm.SubscriptionLocation` |

Steps 4a and 4b are mutually exclusive — a file takes one path based on its `standardisedRepresentationType`.

After location bulk insert, `GeoLocation.SpatialPoint` (a `GEOGRAPHY` column) is populated via a SQL `UPDATE` using `geography::Point()`. `SqlBulkCopy` cannot write to spatial columns directly.

---

## Database Structure

All tables are created in the `cdm` (Communications Data Management) schema. The service expects the `dbo.Document`, `dbo.Reference`, and `dbo.ReferenceSecurity` tables to exist in the target database (these are pre-existing application tables).

### Entity-Relationship Overview

```
dbo.Document (pre-existing)
    └── cdm.JsonFile          (one row per ingested file)
            ├── cdm.Identifier[]         (all identifiers in the file)
            ├── cdm.Location[]           (all locations in the file)
            │       ├── cdm.CellLocation      (1:1)
            │       ├── cdm.GeoLocation       (1:1, includes GEOGRAPHY SpatialPoint)
            │       ├── cdm.PostalAddress     (1:1)
            │       └── cdm.WifiAccessPoint   (1:1)
            │
            ├── cdm.Event[]              (event path)
            │       ├── cdm.EventAttribute[]
            │       └── cdm.EventParty[]
            │               ├── cdm.PartyAttribute[]
            │               └── cdm.EventPartyIdentifier[]  ──► cdm.Identifier
            │
            └── cdm.Subscriber[]         (subscriber path)
                    ├── cdm.SubscriberAttribute[]
                    ├── cdm.SubscriberIdentifier[]  ──► cdm.Identifier
                    ├── cdm.SubscriberLocation[]    ──► cdm.Location
                    └── cdm.Subscription[]
                            ├── cdm.SubscriptionAttribute[]
                            ├── cdm.SubscriptionIdentifier[]  ──► cdm.Identifier
                            └── cdm.SubscriptionLocation[]    ──► cdm.Location
```

### Table Reference

| # | Table | Description | Volume |
|---|---|---|---|
| 1 | `cdm.JsonFile` | Anchor row per ingested file. Folds in CSP organisation metadata, authorisation details, and results file metadata (all 1:1 with the file). | Low |
| 2 | `cdm.Identifier` | All communication identifiers (MSISDNs, IMEIs, IMSIs, IPs, etc.) from a file. Keyed by `(JsonFile_ID, InDocumentID)`. | Medium |
| 3 | `cdm.Location` | Parent location record. Sub-type tables hang off this via FK. | Medium |
| 4 | `cdm.CellLocation` | Cell tower fields: MCC, MNC, LAC, Cell ID, CGI, SAC, RAT, azimuth, eCellID, eNodeB, ECGI, TAC. 1:1 with `cdm.Location`. | Medium |
| 5 | `cdm.GeoLocation` | WGS84 lat/long, OSGB easting/northing, `SpatialPoint GEOGRAPHY`. 1:1 with `cdm.Location`. | Medium |
| 6 | `cdm.PostalAddress` | Full postal address with individual raw components. 1:1 with `cdm.Location`. | Low |
| 7 | `cdm.WifiAccessPoint` | Wi-Fi access point ID, name, SSID. 1:1 with `cdm.Location`. | Low |
| 8 | `cdm.Event` | One row per event. `EventType` and `EventSuperType` are typed/constrained columns; raw data packed into `RawData` JSON. Row compression applied. | High |
| 9 | `cdm.EventAttribute` | Key-value pairs on events. Row compression applied. | High |
| 10 | `cdm.EventParty` | One row per party in an event. Highest-volume table. Typed datetime, duration, and data columns. `IngestSeqNum` used transiently during ingestion for IDENTITY recovery. Row compression applied. | Very High |
| 11 | `cdm.PartyAttribute` | Key-value pairs on event parties. Row compression applied. | High |
| 12 | `cdm.EventPartyIdentifier` | M:N junction between `EventParty` and `Identifier`. Clustered PK on `(EventParty_ID, Identifier_ID)`. | Very High |
| 13 | `cdm.Subscriber` | Subscriber personal details. Standardised name and DOB as typed columns. | Low–Medium |
| 14 | `cdm.SubscriberAttribute` | Key-value pairs on subscribers. | Low |
| 15 | `cdm.SubscriberIdentifier` | M:N junction between `Subscriber` and `Identifier`. | Low–Medium |
| 16 | `cdm.SubscriberLocation` | Date-ranged link from subscriber to a location record. | Low |
| 17 | `cdm.Subscription` | Subscription records with activation/deactivation dates. | Low–Medium |
| 18 | `cdm.SubscriptionAttribute` | Key-value pairs on subscriptions. | Low |
| 19 | `cdm.SubscriptionIdentifier` | M:N junction between `Subscription` and `Identifier`. | Low–Medium |
| 20 | `cdm.SubscriptionLocation` | Date-ranged link from subscription to a location record. | Low |

### Design Conventions

- **Standardised vs raw values** — queryable, normalised values are stored as typed columns (e.g. `CGIStandardised VARCHAR(50)`). Raw source values and composition metadata are packed into a `RawData NVARCHAR(MAX)` JSON blob per row to avoid column explosion while preserving full fidelity.
- **Row compression** — applied to the three highest-volume tables: `cdm.Event`, `cdm.EventParty`, `cdm.EventAttribute`, `cdm.PartyAttribute`.
- **Lock escalation disabled** — `LOCK_ESCALATION = DISABLE` is set on all high-volume tables to support concurrent multi-pod ingestion without full-table lock escalation.
- **FK enforcement** — all FK constraints are enforced. If further bulk-load throughput tuning is required, constraints can be disabled before a batch and re-enabled after.

---

## Indexes

All non-clustered indexes are defined in `sql/04_create_indexes.sql`. Key indexes:

| Index | Table | Purpose |
|---|---|---|
| `IX_JsonFile_Document` | `cdm.JsonFile` | Document → Reference → ReferenceSecurity security join used by every query |
| `IX_Identifier_Standardised` | `cdm.Identifier` | Q2: fast seek on `IdentifierStandardised` across the full dataset |
| `IX_Identifier_Raw` | `cdm.Identifier` | Fallback seek on raw value when standardised form is unavailable |
| `IX_Identifier_JsonFile` | `cdm.Identifier` | Q3: covering seek on `JsonFile_ID`, includes all result columns |
| `IX_Location_JsonFile` | `cdm.Location` | Resolve InDocumentID after bulk insert |
| `IX_Event_JsonFile` | `cdm.Event` | Q1: enumerate all events for a file |
| `IX_EventParty_Event` | `cdm.EventParty` | Q1: fetch parties per event, includes projected columns |
| `IX_EventParty_IngestSeqNum` | `cdm.EventParty` | Ingest recovery: map `IngestSeqNum` → `ID` after bulk insert |
| `IX_EPI_IdentifierReverse` | `cdm.EventPartyIdentifier` | Reverse lookup: from identifier to event parties (used in Q2-S) |
| `IX_SubI_IdentifierReverse` | `cdm.SubscriberIdentifier` | Reverse lookup: from identifier to subscribers |
| `SPIX_GeoLocation_SpatialPoint` | `cdm.GeoLocation` | Optional spatial index for proximity/bounding-box queries (commented out by default) |

All heavily-inserted indexes use `FILLFACTOR = 85` to reduce page splits under sustained load.

---

## Sample Queries

`sql/05_sample_queries.sql` contains three required queries and supplementary variants. All queries filter rows through `dbo.ReferenceSecurity` to enforce user-level access control.

Queries must be run with `SET TRANSACTION ISOLATION LEVEL SNAPSHOT` to prevent reads from being blocked by concurrent bulk inserts (requires `ALLOW_SNAPSHOT_ISOLATION ON` — see `01_database_settings.sql`).

| Query | Description | Key index path |
|---|---|---|
| **Q1** | All events within an application (Reference record) | `IX_JsonFile_Document` → `IX_Event_JsonFile` → `IX_EventParty_Event` |
| **Q2** | All occurrences of a specific identifier across the accessible dataset | `IX_Identifier_Standardised` seek, then security join |
| **Q3** | Unique identifiers from one specific JSON document | `IX_Identifier_JsonFile` covering seek |
| **Q1-S** | Q1 extended with full location detail (cell tower + geo coordinates) | As Q1, with additional Location sub-type joins |
| **Q2-S** | Q2 extended with event party context for each occurrence | As Q2, with reverse join through `IX_EPI_IdentifierReverse` |
| **Q-SUB** | All subscriber and subscription detail for an application | `IX_Subscriber_JsonFile` → junction tables |

---

## Configuration

Configuration is read from `appsettings.json` and can be overridden via environment variables (standard .NET configuration precedence). In Kubernetes, sensitive values (e.g. connection strings) should be supplied via Secrets mounted as environment variables.

```json
{
  "ConnectionStrings": {
    "CdmDatabase": "Server=...;Database=...;Integrated Security=True;TrustServerCertificate=True;"
  },
  "Ingest": {
    "FileDrop": {
      "WatchPath":          "/data/ingest/drop",
      "ProcessedPath":      "/data/ingest/processed",
      "ErrorPath":          "/data/ingest/error",
      "FilePattern":        "*.json",
      "PollIntervalSeconds": 5
    },
    "BulkCopy": {
      "BatchSize":      5000,
      "TimeoutSeconds": 300
    },
    "MaxConcurrentFiles": 1
  },
  "Serilog": { ... }
}
```

| Setting | Default | Description |
|---|---|---|
| `ConnectionStrings:CdmDatabase` | _(required)_ | SQL Server connection string |
| `Ingest:FileDrop:WatchPath` | `/data/ingest/drop` | Folder polled for incoming JSON files |
| `Ingest:FileDrop:ProcessedPath` | `/data/ingest/processed` | Destination for successfully ingested files |
| `Ingest:FileDrop:ErrorPath` | `/data/ingest/error` | Destination for files that fail ingestion |
| `Ingest:FileDrop:FilePattern` | `*.json` | Glob pattern for file discovery |
| `Ingest:FileDrop:PollIntervalSeconds` | `5` | How often the drop folder is scanned |
| `Ingest:BulkCopy:BatchSize` | `5000` | Rows per `SqlBulkCopy` batch |
| `Ingest:BulkCopy:TimeoutSeconds` | `300` | `SqlBulkCopy` operation timeout |
| `Ingest:MaxConcurrentFiles` | `1` | Maximum files processed in parallel |

Connection strings can also be supplied as environment variables using the standard .NET format:

```
ConnectionStrings__CdmDatabase=Server=...;Database=...;
```

Use `.NET User Secrets` (secret ID `json-ingest-service`) for local development.

---

## SQL Setup Scripts

Run the scripts in the `sql/` folder **in order** against your target SQL Server database. Replace `[YourDatabaseName]` in each script with your actual database name.

| Script | Run once? | Purpose |
|---|---|---|
| `01_database_settings.sql` | Yes | Enables snapshot isolation, async statistics, ADR, and Query Store |
| `02_create_schema.sql` | Yes | Creates the `cdm` schema |
| `03_create_tables.sql` | Yes | Creates all 20 `cdm` tables in FK-dependency order |
| `04_create_indexes.sql` | Yes | Creates all non-clustered indexes |
| `05_sample_queries.sql` | As needed | Reference queries Q1, Q2, Q3 and supplementary variants |
| `06_reset_data.sql` | Dev/test only | Deletes all `cdm` data and reseeds IDENTITY counters |
| `07_recovery_disk_full.sql` | Emergency | Procedures for recovering from a disk-full condition |
| `08_apply_concurrency_settings.sql` | Tuning | Applies additional concurrency optimisations |

---

## Docker Deployment

### Build

```bash
cd src/JsonIngestService
docker build -t json-ingest-service .
```

### Run

```bash
docker run -d \
  --name json-ingest \
  -e "ConnectionStrings__CdmDatabase=Server=myserver;Database=mydb;User Id=sa;Password=..." \
  -e "Ingest__MaxConcurrentFiles=4" \
  -v /host/ingest/drop:/data/ingest/drop \
  -v /host/ingest/processed:/data/ingest/processed \
  -v /host/ingest/error:/data/ingest/error \
  json-ingest-service
```

The container runs as a non-root user (`ingest`, UID 1001) for security hardening.

### Volumes

| Mount path | Purpose |
|---|---|
| `/data/ingest/drop` | Watch folder — place JSON files here for processing |
| `/data/ingest/processed` | Successfully ingested files are moved here |
| `/data/ingest/error` | Failed files are moved here |

### Restart for Reprocessing

The in-memory "seen files" set is cleared when the container restarts. To reprocess files after running `06_reset_data.sql`:

```bash
# Move processed files back to the drop folder
Move-Item "C:\ingest\processed\*.json" "C:\ingest\drop\"

# Restart the container
docker restart json-ingest
```

### Kubernetes

The service is designed to run as a Kubernetes `Deployment` or `Job`. Supply the connection string via a Kubernetes `Secret` mounted as an environment variable. Configure `Ingest__MaxConcurrentFiles` to tune parallelism per pod. Note that multiple pods can safely ingest to the same database concurrently — lock escalation is disabled on all high-volume tables.

---

## Volume / Load Testing

`scripts/Generate-VolumeTestFiles.ps1` generates multiple copies of existing processed files back into the drop folder for throughput testing.

```powershell
# One-time setup: copy processed files to a templates folder
New-Item -ItemType Directory "C:\ingest\templates" -Force
Copy-Item "C:\ingest\processed\*.json" "C:\ingest\templates\"

# Generate 500 files (100 copies × 5 templates), drop all at once
.\scripts\Generate-VolumeTestFiles.ps1 -CopiesPerFile 100

# Generate 5,000 files, dripped in batches of 50 to avoid overwhelming the service
.\scripts\Generate-VolumeTestFiles.ps1 -CopiesPerFile 1000 -BatchSize 50

# Use a custom source folder
.\scripts\Generate-VolumeTestFiles.ps1 -SourceDir "D:\samples" -CopiesPerFile 200
```

All generated copies keep the original `documentId` prefix so the FK to `dbo.Document` is satisfied. Duplicate `cdm` data is created intentionally — this script is for throughput testing only.

---

## Maintenance Scripts

### Reset data (`06_reset_data.sql`)

Deletes all rows from every `cdm` table in FK order (leaf-to-root) and reseeds all `IDENTITY` counters to 0. Use in development/test environments to allow reprocessing the same source files.

### Disk-full recovery (`07_recovery_disk_full.sql`)

Provides recovery procedures for scenarios where the SQL Server transaction log fills the disk mid-ingestion (error 9002). This can occur with large files or high concurrency. The script includes steps to identify open transactions, shrink log files, and resume normal operation.

### Concurrency settings (`08_apply_concurrency_settings.sql`)

Applies additional SQL Server concurrency tuning — recommended before running at full production load.

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Data.SqlClient` | 5.2.2 | SQL Server connectivity and `SqlBulkCopy` |
| `Microsoft.Extensions.Hosting` | 8.0.1 | `BackgroundService` / hosted worker |
| `Serilog.Extensions.Hosting` | 8.0.0 | Structured logging integration |
| `Serilog.Settings.Configuration` | 8.0.4 | Configure Serilog from `appsettings.json` |
| `Serilog.Sinks.Console` | 6.0.0 | Console log sink (suitable for Kubernetes log aggregation) |

**Target framework:** .NET 8.0 (Worker SDK)  
**Target OS:** Linux (Docker); also runs on Windows for local development  
**SQL Server version:** SQL Server 2022 (scripts reference SQL Server 2022 features; compatible with SQL Server 2019+)
