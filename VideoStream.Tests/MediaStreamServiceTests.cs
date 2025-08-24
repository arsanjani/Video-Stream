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

    [Theory]
    // Characters invalid for Windows file names must be rejected
    [InlineData("id*asterisk")]
    [InlineData("id?question")]
    [InlineData("id|pipe")]
    [InlineData("id<greater>")]
    [InlineData("id>less")]
    [InlineData("id\"quote")]
    [InlineData("id:colon")]
    [InlineData("id\\backslash")]
    [InlineData("id/slash")]
    public void IsValidId_WithInvalidWindowsChars_ReturnsFalse(string idWithSpecialChars)
    {
        // Act
        var result = _mediaStreamService.IsValidId(idWithSpecialChars);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    // These special characters are allowed in Windows file names and should be accepted
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
    public void IsValidId_WithAllowedSpecialCharacters_ReturnsTrue(string idWithSpecialChars)
    {
        // Act
        var result = _mediaStreamService.IsValidId(idWithSpecialChars);

        // Assert
        result.Should().BeTrue();
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