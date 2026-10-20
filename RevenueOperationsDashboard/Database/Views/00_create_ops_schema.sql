-- ====================================================================
-- Script: Create [ops] Schema
-- Description: Creates isolated [ops] schema for operational views,
--              leaving existing [dbo] views completely intact.
-- ====================================================================

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'ops')
BEGIN
    EXEC('CREATE SCHEMA [ops] AUTHORIZATION [dbo];');
END
GO
