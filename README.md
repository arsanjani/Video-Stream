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

3. **Configure video streaming settings**
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

4. **Build and run the application**
   ```bash
   cd src
   dotnet build
   dotnet run
   ```

5. **Access the API**
   - The API will be available at `https://localhost:5001` (or the configured port)
   - Stream endpoint: `GET /api/stream/{newsid}`

### SQL Server FileStream Configuration

Since SQL Server FileStream requires Windows Authentication, ensure:

1. **Database setup**: Your SQL Server has FileStream enabled
2. **Table structure**: Your `MediaStream` table should have:
   - `newsid` (identifier)
   - `FileSize` (bigint)
   - `FileType` (nvarchar)
   - `FileExt` (nvarchar)
   - `FileData` (varbinary(max) FILESTREAM)

3. **Authentication**: Configure Windows Authentication between your app and SQL Server

### API Endpoints

- `GET /api/stream/{newsid}` - Stream media file with optional Range header support
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

## License
This project is licensed under the MIT License.

