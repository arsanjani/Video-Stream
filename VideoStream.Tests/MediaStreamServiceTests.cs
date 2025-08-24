using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Moq;
using FluentAssertions;
using VideoStream.Services;
using VideoStream.Models;
using System.Data.Common;
using System.Data;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;

namespace VideoStream.Tests;

public class MediaStreamServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IMemoryCache> _mockMemoryCache;
    private readonly Mock<ILogger<MediaStreamService>> _mockLogger;
    private readonly Mock<IFileSystemService> _mockFileSystemService;
    private readonly MediaStreamService _mediaStreamService;

    public MediaStreamServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockMemoryCache = new Mock<IMemoryCache>();
        _mockLogger = new Mock<ILogger<MediaStreamService>>();
        _mockFileSystemService = new Mock<IFileSystemService>();

        SetupConfiguration();
        SetupMemoryCache();
        SetupFileSystemService();

        _mediaStreamService = new MediaStreamService(
            _mockConfiguration.Object,
            _mockMemoryCache.Object,
            _mockLogger.Object,
            _mockFileSystemService.Object);
    }

    private void SetupConfiguration()
    {
        // Setup connection strings section
        var connectionStringsSection = new Mock<IConfigurationSection>();
        connectionStringsSection.Setup(s => s["Media"])
            .Returns("Data Source=TestServer;Initial Catalog=TestDB;Integrated Security=true;");
        
        _mockConfiguration.Setup(c => c.GetSection("ConnectionStrings"))
            .Returns(connectionStringsSection.Object);

        // Setup buffer paths section
        var bufferPathsSection = new Mock<IConfigurationSection>();
        var bufferPath0 = new Mock<IConfigurationSection>();
        bufferPath0.Setup(s => s.Value).Returns("C:\\TestBuffer1\\");
        var bufferPath1 = new Mock<IConfigurationSection>();
        bufferPath1.Setup(s => s.Value).Returns("C:\\TestBuffer2\\");
        
        bufferPathsSection.Setup(s => s.GetChildren())
            .Returns(new[] { bufferPath0.Object, bufferPath1.Object });
        
        _mockConfiguration.Setup(c => c.GetSection("VideoStream:BufferPaths"))
            .Returns(bufferPathsSection.Object);
        
        // Setup individual configuration values with proper sections for GetValue<T>()
        var readBufferSection = new Mock<IConfigurationSection>();
        readBufferSection.Setup(s => s.Value).Returns("65536");
        _mockConfiguration.Setup(c => c.GetSection("VideoStream:ReadStreamBufferSize"))
            .Returns(readBufferSection.Object);
        
        var cacheExpirationSection = new Mock<IConfigurationSection>();
        cacheExpirationSection.Setup(s => s.Value).Returns("60");
        _mockConfiguration.Setup(c => c.GetSection("VideoStream:CacheExpirationSeconds"))
            .Returns(cacheExpirationSection.Object);
    }

    private void SetupMemoryCache()
    {
        // Simple mock that doesn't use extension methods
        object? cachedValue = null;
        _mockMemoryCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out cachedValue))
            .Returns(false);
        
        _mockMemoryCache.Setup(c => c.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());
    }

    private void SetupFileSystemService()
    {
        // By default, files don't exist and can't be opened
        _mockFileSystemService.Setup(fs => fs.FileExists(It.IsAny<string>()))
            .Returns(false);
        _mockFileSystemService.Setup(fs => fs.CanOpenFile(It.IsAny<string>()))
            .Returns(false);
        _mockFileSystemService.Setup(fs => fs.OpenRead(It.IsAny<string>(), It.IsAny<int>()))
            .Throws(new FileNotFoundException());
    }

    [Fact]
    public void IsValidId_WithValidNumericId_ReturnsTrue()
    {
        // Arrange
        var validId = "12345";

        // Act
        var result = _mediaStreamService.IsValidId(validId);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("user_123")]
    [InlineData("video-id")]
    [InlineData("ABC")]
    [InlineData("test_video_123")]
    [InlineData("media-file-456")]
    public void IsValidId_WithValidStringId_ReturnsTrue(string validId)
    {
        // Act
        var result = _mediaStreamService.IsValidId(validId);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("id/path")]
    [InlineData("id\\path")]
    public void IsValidId_WithInvalidId_ReturnsFalse(string invalidId)
    {
        // Act
        var result = _mediaStreamService.IsValidId(invalidId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidId_WithNull_ReturnsFalse()
    {
        // Act
        var result = _mediaStreamService.IsValidId(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("999999999999999")]
    [InlineData("a")]
    [InlineData("Z")]
    [InlineData("a1")]
    [InlineData("1a")]
    [InlineData("a_b")]
    [InlineData("a-b")]
    [InlineData("very_long_but_valid_id_name_12345")]
    public void IsValidId_WithValidEdgeCases_ReturnsTrue(string validId)
    {
        // Act
        var result = _mediaStreamService.IsValidId(validId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetMediaStreamInfoAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var invalidId = "invalid";

        // Act
        var result = await _mediaStreamService.GetMediaStreamInfoAsync(invalidId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreatePartialContentAsync_WithInvalidId_ThrowsFileNotFoundException()
    {
        // Arrange
        var invalidId = "invalid";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _mediaStreamService.CreatePartialContentAsync(invalidId, 0, 100));
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(null!, _mockMemoryCache.Object, _mockLogger.Object, _mockFileSystemService.Object));
    }

    [Fact]
    public void Constructor_WithNullMemoryCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_mockConfiguration.Object, null!, _mockLogger.Object, _mockFileSystemService.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_mockConfiguration.Object, _mockMemoryCache.Object, null!, _mockFileSystemService.Object));
    }

    [Fact]
    public void Constructor_WithNullFileSystemService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MediaStreamService(_mockConfiguration.Object, _mockMemoryCache.Object, _mockLogger.Object, null!));
    }

    [Fact]
    public void IsValidId_WithMaxLengthId_ReturnsTrue()
    {
        // Arrange - Create a 50-character ID (maximum allowed length)
        var maxLengthId = new string('a', 50);

        // Act
        var result = _mediaStreamService.IsValidId(maxLengthId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidId_WithTooLongId_ReturnsFalse()
    {
        // Arrange - Create a 51-character ID (exceeds maximum allowed length)
        var tooLongId = new string('a', 51);

        // Act
        var result = _mediaStreamService.IsValidId(tooLongId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidId_WithLeadingZeros_ReturnsTrue()
    {
        // Arrange
        var idWithLeadingZeros = "00123";

        // Act
        var result = _mediaStreamService.IsValidId(idWithLeadingZeros);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("\tid")]
    [InlineData("id\n")]
    public void IsValidId_WithWhitespace_ReturnsFalse(string idWithWhitespace)
    {
        // Act
        var result = _mediaStreamService.IsValidId(idWithWhitespace);

        // Assert
        result.Should().BeFalse();
    }

    private bool PlatformExpectedIsValid(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        if (id != id.Trim())
            return false;
        if (id.Length > 50)
            return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var runtimeInvalid = Path.GetInvalidFileNameChars();
            var controlChars = Enumerable.Range(0, 32).Select(i => (char)i);
            var invalidChars = runtimeInvalid.Concat(controlChars).Distinct().ToArray();
            if (id.IndexOfAny(invalidChars) >= 0)
                return false;
            if (id.EndsWith('.'))
                return false;
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL" }
                .Concat(Enumerable.Range(1, 9).Select(i => "COM" + i))
                .Concat(Enumerable.Range(1, 9).Select(i => "LPT" + i))
                .ToArray();
            var firstPart = id.Split('.').FirstOrDefault()?.ToUpperInvariant() ?? string.Empty;
            if (reservedNames.Contains(firstPart))
                return false;
        }
        else
        {
            var invalidChars = new[] { '/' }.Concat(Enumerable.Range(0, 32).Select(i => (char)i)).Distinct().ToArray();
            if (id.IndexOfAny(invalidChars) >= 0)
                return false;
        }

        return true;
    }

    [Theory]
    // These IDs include characters that may be valid or invalid depending on platform.
    [InlineData("id*asterisk")]
    [InlineData("id?question")]
    [InlineData("id|pipe")]
    [InlineData("id<greater>")]
    [InlineData("id>less")]
    [InlineData("id\"quote")]
    [InlineData("id:colon")]
    [InlineData("id\\backslash")]
    [InlineData("id/slash")]
    [InlineData("123!@#")]
    [InlineData("id@domain.com")]
    [InlineData("id.extension")]
    [InlineData("id+plus")]
    [InlineData("id=equals")]
    [InlineData("id%percent")]
    [InlineData("id&ampersand")]
    [InlineData("id(parenthesis)")]
    [InlineData("id[bracket]")]
    [InlineData("id{brace}")]
    [InlineData("id;semicolon")]
    [InlineData("1 2 3")]
    [InlineData("id with spaces")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("LPT1")]
    [InlineData("nul")]
    [InlineData("name.")]
    public void IsValidId_SpecialCharacters_MatchPlatformRules(string id)
    {
        // Act
        var result = _mediaStreamService.IsValidId(id);

        // Assert
        var expected = PlatformExpectedIsValid(id);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("LPT1")]
    [InlineData("nul")]
    public void IsValidId_WithReservedDeviceNames_ReturnsFalse(string reservedName)
    {
        // Act
        var result = _mediaStreamService.IsValidId(reservedName);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidId_WithTrailingDot_ReturnsFalse()
    {
        var result = _mediaStreamService.IsValidId("name.");
        result.Should().BeFalse();
    }
} 