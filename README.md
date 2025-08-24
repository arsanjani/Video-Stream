# Video Streaming Web API (.NET 9)
Fast HTTP Media Streaming Web API built with ASP.NET Core 9 | C#    

## Features
- **Customizable chunk size** - Configurable buffer size for streaming
- **HTTP byte range support** - Full support for HTTP Range requests
- **Video player compatibility** - Works with almost every modern video player
- **SQL Server FileStream backend** - Efficient streaming from SQL Server FileStream
- **Local buffer fallback** - Automatic fallback to local file system when available
- **Modern .NET 9 architecture** - Built with latest ASP.NET Core practices
- **Dependency injection** - Fully configured with DI container
- **Async/await pattern** - Non-blocking I/O operations
- **Logging support** - Comprehensive logging with structured logging
- **Memory caching** - Intelligent caching of media metadata

## Technology Stack
- **.NET 9** - Latest .NET runtime
- **ASP.NET Core 9** - Modern web framework
- **SQL Server** - Database with FileStream support
- **Microsoft.Data.SqlClient** - Modern SQL Server data provider
- **IMemoryCache** - Built-in memory caching

## How to Use

### Prerequisites
- .NET 9 SDK or later
- SQL Server with FileStream enabled
- Windows OS (for SQL Server FileStream support)

### Setup Instructions

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd VideoStream
   ```

2. **Configure the connection string**
   Edit the connection string in [appsettings.json](src/appsettings.json):
   ```json
   {
     "ConnectionStrings": {
       "Media": "integrated security=true;Persist Security Info=true;Initial Catalog=dbMedia;Data Source=localhost;TrustServerCertificate=true;"
     }
   }
   ```

3. **Set up the database**
   Run the SQL script to create the database and tables:
   ```bash
   # Using SQL Server Management Studio (SSMS):
   # 1. Open SSMS and connect to your SQL Server instance
   # 2. Open the database-setup.sql file
   # 3. Execute the script
   
   # Or using sqlcmd command line:
   sqlcmd -S localhost -E -i database-setup.sql
   ```

4. **Configure video streaming settings**
   Customize settings in [appsettings.json](src/appsettings.json):
   ```json
   {
     "VideoStream": {
       "BufferPaths": ["B:\\", "F:\\"],
       "ReadStreamBufferSize": 262144,
       "CacheExpirationSeconds": 30,
       "ExecutionTimeoutSeconds": 600
     }
   }
   ```

5. **Build and run the application**
   ```bash
   cd src
   dotnet build
   dotnet run
   ```

6. **Access the API**
   - The API will be available at `https://localhost:5001` (or the configured port)
   - Stream endpoint: `GET /api/stream/{id}`

### SQL Server FileStream Configuration

Since SQL Server FileStream requires Windows Authentication, ensure:

1. **Database setup**: Your SQL Server has FileStream enabled
2. **Table structure**: Your `MediaStream` table should have:
   - `id` (identifier)
   - `FileSize` (bigint)
   - `FileType` (nvarchar)
   - `FileExt` (nvarchar)
   - `FileData` (varbinary(max) FILESTREAM)

3. **Authentication**: Configure Windows Authentication between your app and SQL Server
4. **Database Creation**: Use the provided SQL script `database-setup.sql` to create the database and tables

### Database Files

The repository includes:
- `database-setup.sql` - Complete database setup script that creates:
  - Database `dbMedia` with FileStream support
  - Table `MediaStream` with proper indexes
  - Trigger for automatic timestamp updates

### API Endpoints

- `GET /api/stream/{id}` - Stream media file with optional Range header support
- Supports HTTP Range requests for partial content delivery
- Automatically handles MIME type detection based on file extension

### Supported Media Types
- MP3 (audio/mpeg)
- MP4 (video/mp4)
- OGG (application/ogg)
- OGV (video/ogg)
- OGA (audio/ogg)
- WAV (audio/x-wav)
- WebM (video/webm)

## Architecture Changes from .NET Framework

### Key Improvements
- **Modern project structure** - SDK-style project file
- **Dependency injection** - Service-based architecture
- **Configuration system** - JSON-based configuration
- **Async patterns** - Full async/await support
- **Memory management** - Improved memory usage and garbage collection
- **Cross-platform** - Can run on Windows, Linux, and macOS (except for SQL FileStream)

### Breaking Changes
- Moved from `System.Web` to `Microsoft.AspNetCore`
- Replaced `MemoryCache.Default` with `IMemoryCache`
- Updated from `WebConfigurationManager` to `IConfiguration`
- Replaced `ApiController` with `ControllerBase`
- Updated HTTP client patterns

## Performance Optimizations
- **Streaming architecture** - Direct stream-to-stream copying
- **Buffer management** - Configurable buffer sizes
- **Caching strategy** - Metadata caching with TTL
- **Async I/O** - Non-blocking file operations
- **Memory efficiency** - Minimal memory footprint for large files

## Troubleshooting

### Common Issues
1. **SQL Server FileStream not working**: Ensure FileStream is enabled in SQL Server configuration
2. **Authentication errors**: Verify Windows Authentication is properly configured
3. **Path not found**: Check buffer paths exist and are accessible
4. **Performance issues**: Adjust buffer size and caching settings

### Logging
The application uses structured logging. Check logs for detailed error information and performance metrics.

### Storage workflow and rules

Follow these storage rules to ensure consistent and performant media delivery:

- **Primary metadata**: Use SQL Server (FileStream) for storing all media file metadata and as the final persistent layer for file contents after buffers. The SQL table is the authoritative source of truth for file metadata.
- **Buffer priority order**: Configure buffers from fastest to slowest (for example: in-memory cache -> local SSD -> network-attached storage -> archival). When searching for media, the service must check buffers in that priority order and return the first match found.
- **Fetching strategy**: If media is found in any buffer layer, serve it from the buffer. Only when buffers do not contain the media should the service fetch the file from the SQL FileStream layer.
- **FileSize field semantics**: Store the actual file size in bytes in the SQL table `FileSize`. Do not store on-disk allocation size (which varies due to fragmentation, cluster size, or sparse files); use the exact byte length of the logical file content.
- **Error symptoms for size mismatch**: If you see `ERR_HTTP2_PROTOCOL_ERROR` in browsers or HTTP 500 responses from the API while streaming media, a common cause is that the `FileSize` value in the database does not match the actual number of bytes returned by the stream. Verify that `FileSize` equals the true byte length of the file.

Practical checklist:

- Ensure `FileSize` is computed from the file byte length before inserting/updating metadata in SQL.
- Maintain buffer path ordering in `appsettings.json` under `VideoStream:BufferPaths` from fastest to slowest.
- Use memory caching for metadata and small files, and persistent buffers for larger files.
- Add validation in upload/ingest workflows to compare computed file bytes vs stored `FileSize` and log mismatches as errors.


## License
This project is licensed under the MIT License.

