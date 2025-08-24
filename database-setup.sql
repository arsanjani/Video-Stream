-- Video Stream Database Setup Script
-- This script creates the database and tables required for the Video Stream API
-- Requires SQL Server with FileStream enabled

USE master;
GO

-- Enable FileStream at the instance level (if not already enabled)
-- Note: This may require SQL Server restart and must be run by a system administrator
-- EXEC sp_configure 'filestream access level', 2;
-- RECONFIGURE;
-- GO

-- Create the database with FileStream support
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'dbMedia')
BEGIN
    CREATE DATABASE dbMedia
    ON 
    (
        NAME = 'dbMedia_Data',
        FILENAME = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\dbMedia.mdf',
        SIZE = 100MB,
        MAXSIZE = 1GB,
        FILEGROWTH = 10MB
    ),
    FILEGROUP FileStreamGroup CONTAINS FILESTREAM
    (
        NAME = 'dbMedia_FileStream',
        FILENAME = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\dbMedia_FileStream',
        MAXSIZE = 10GB
    )
    LOG ON
    (
        NAME = 'dbMedia_Log',
        FILENAME = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\dbMedia.ldf',
        SIZE = 10MB,
        MAXSIZE = 100MB,
        FILEGROWTH = 5MB
    );
    
    PRINT 'Database dbMedia created successfully with FileStream support.';
END
ELSE
BEGIN
    PRINT 'Database dbMedia already exists.';
END
GO

-- Switch to the new database
USE dbMedia;
GO

-- Create the MediaStream table with FileStream support
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MediaStream')
BEGIN
    CREATE TABLE MediaStream
    (
        id NVARCHAR(50) NOT NULL PRIMARY KEY,               -- Media identifier
        FileSize BIGINT NOT NULL,                           -- File size in bytes
        FileType NVARCHAR(100) NOT NULL,                    -- MIME type (e.g., 'video/mp4')
        FileExt NVARCHAR(10) NOT NULL,                      -- File extension (e.g., '.mp4')
        FileData VARBINARY(MAX) FILESTREAM NULL,        -- File content stored as FileStream
        RowGuid UNIQUEIDENTIFIER ROWGUIDCOL NOT NULL UNIQUE DEFAULT NEWSEQUENTIALID(), -- Required for FileStream
        CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE() -- Creation timestamp
    );
    
    -- Create indexes for better performance
    CREATE INDEX IX_MediaStream_FileType ON MediaStream(FileType);
    CREATE INDEX IX_MediaStream_FileExt ON MediaStream(FileExt);
    CREATE INDEX IX_MediaStream_CreatedDate ON MediaStream(CreatedDate);
    
    PRINT 'MediaStream table created successfully with FileStream support.';
END
ELSE
BEGIN
    PRINT 'MediaStream table already exists.';
END
GO





-- Insert sample data (optional - remove if not needed)
-- This section demonstrates how to insert media files
/*
-- Example: Insert a sample MP4 file (you would replace this with actual file data)
DECLARE @SampleData VARBINARY(MAX) = 0x000000; -- Replace with actual file binary data

INSERT INTO MediaStream (id, FileSize, FileType, FileExt, FileData)
VALUES ('123', 1048576, 'video/mp4', '.mp4', @SampleData);

INSERT INTO MediaStream (id, FileSize, FileType, FileExt, FileData)
VALUES ('456', 2097152, 'audio/mpeg', '.mp3', @SampleData);
*/

-- Display summary information
PRINT '';
PRINT '=== Database Setup Complete ===';
PRINT 'Database: dbMedia';
PRINT 'Tables: MediaStream';
PRINT 'Triggers: TR_MediaStream_UpdateModifiedDate';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Update your connection string in appsettings.json';
PRINT '2. Ensure your application has appropriate permissions to access the database';
PRINT '3. Test the connection by running your Video Stream API';
PRINT '';

-- Show current database information
SELECT 
    'Database Information' as Info,
    DB_NAME() as DatabaseName,
    SUSER_SNAME() as CurrentUser,
    GETUTCDATE() as SetupTime;

-- Show table structure
SELECT 
    'Table Structure' as Info,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'MediaStream'
ORDER BY ORDINAL_POSITION;

GO
