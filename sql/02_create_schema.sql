-- =============================================================================
-- 02_create_schema.sql
-- Creates the cdm (Communications Data Management) schema.
-- All 20 ingest tables live in this schema, isolated from existing tables.
-- =============================================================================

USE [YourDatabaseName]; -- << replace with your database name
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'cdm')
BEGIN
    EXEC sp_executesql N'CREATE SCHEMA cdm AUTHORIZATION dbo';
    PRINT 'Schema cdm created.';
END
ELSE
    PRINT 'Schema cdm already exists — skipped.';
GO
