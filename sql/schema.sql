-- =============================================================================
-- AgriStream IoT - Azure SQL Database Schema & Seed Script (Task b & d)
-- Note: AgriStream.Web also runs EnsureCreatedAsync() automatically on startup,
--       or you can run this script directly in Azure Portal Query Editor / SSMS.
-- =============================================================================

IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles (
        Id   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        Name NVARCHAR(50)      NOT NULL CONSTRAINT UQ_Roles_Name UNIQUE
    );
END;

IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Username     NVARCHAR(50)      NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
        PasswordHash NVARCHAR(256)     NOT NULL,
        RoleId       INT               NOT NULL CONSTRAINT FK_Users_Roles FOREIGN KEY REFERENCES dbo.Roles(Id),
        CreatedAt    DATETIME2         NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END;

IF OBJECT_ID('dbo.Devices', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Devices (
        Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Devices PRIMARY KEY,
        DeviceId         NVARCHAR(50)      NOT NULL CONSTRAINT UQ_Devices_DeviceId UNIQUE,
        Name             NVARCHAR(100)     NOT NULL,
        FieldName        NVARCHAR(100)     NOT NULL,
        Status           NVARCHAR(30)      NOT NULL CONSTRAINT DF_Devices_Status DEFAULT (N'Online'),
        LastIrrigatedAt  DATETIME2         NULL,
        IrrigationEndsAt DATETIME2         NULL,
        LastCommandBy    NVARCHAR(50)      NULL,
        UpdatedAt        DATETIME2         NOT NULL CONSTRAINT DF_Devices_UpdatedAt DEFAULT (SYSUTCDATETIME())
    );
END;

-- Seed system roles: Farmer, Agronomist, Admin
IF NOT EXISTS (SELECT 1 FROM dbo.Roles)
BEGIN
    SET IDENTITY_INSERT dbo.Roles ON;
    INSERT INTO dbo.Roles (Id, Name) VALUES (1, N'Farmer'), (2, N'Agronomist'), (3, N'Admin');
    SET IDENTITY_INSERT dbo.Roles OFF;
END;

-- Seed master device registry
IF NOT EXISTS (SELECT 1 FROM dbo.Devices)
BEGIN
    INSERT INTO dbo.Devices (DeviceId, Name, FieldName, Status) VALUES
        (N'SENSOR-001', N'North Maize Probe',     N'North Field',    N'Online'),
        (N'SENSOR-002', N'Sunflower Ridge Probe', N'East Ridge',     N'Online'),
        (N'SENSOR-003', N'Orchard Drip Sensor',   N'Citrus Orchard', N'Online'),
        (N'SENSOR-004', N'Wheat Valley Probe',    N'South Valley',   N'Online'),
        (N'SENSOR-005', N'Greenhouse Monitor',    N'Greenhouse A',   N'Online'),
        (N'SENSOR-006', N'Vineyard Probe',        N'West Vineyard',  N'Maintenance');
END;

SELECT * FROM dbo.Roles;
SELECT Id, Username, RoleId, CreatedAt FROM dbo.Users;
SELECT * FROM dbo.Devices;
