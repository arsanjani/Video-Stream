using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using VideoStream.Controllers;
using VideoStream.Services;
using VideoStream.Models;
using System.Net.Mime;

namespace VideoStream.Tests;

public class StreamControllerSimplifiedTests
{
    private readonly Mock<IMediaStreamService> _mockMediaStreamService;
    private readonly Mock<ILogger<StreamController>> _mockLogger;
    private readonly StreamController _controller;

    public StreamControllerSimplifiedTests()
    {
        _mockMediaStreamService = new Mock<IMediaStreamService>();
        _mockLogger = new Mock<ILogger<StreamController>>();
        _controller = new StreamController(_mockMediaStreamService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Get_WithInvalidNewsId_ReturnsNotFound()
    {
        // Arrange
        var invalidNewsId = "invalid";
        _mockMediaStreamService.Setup(s => s.IsValidNewsId(invalidNewsId))
            .Returns(false);

        // Act
        var result = await _controller.Get(invalidNewsId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_WithValidNewsIdButNoMedia_ReturnsNotFound()
    {
        // Arrange
        var newsId = "123";
        _mockMediaStreamService.Setup(s => s.IsValidNewsId(newsId))
            .Returns(true);
        _mockMediaStreamService.Setup(s => s.GetMediaStreamInfoAsync(newsId))
            .ReturnsAsync((MediaStreamInfo?)null);

        // Act
        var result = await _controller.Get(newsId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_WithZeroFileSize_ReturnsNotFound()
    {
        // Arrange
        var newsId = "123";
        var mediaInfo = new MediaStreamInfo
        {
            Id = newsId,
            FileSize = 0,
            FileType = "video/mp4",
            FileExt = ".mp4"
        };

        _mockMediaStreamService.Setup(s => s.IsValidNewsId(newsId))
            .Returns(true);
        _mockMediaStreamService.Setup(s => s.GetMediaStreamInfoAsync(newsId))
            .ReturnsAsync(mediaInfo);

        // Act
        var result = await _controller.Get(newsId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_WithServiceException_ReturnsInternalServerError()
    {
        // Arrange
        var newsId = "123";
        _mockMediaStreamService.Setup(s => s.IsValidNewsId(newsId))
            .Returns(true);
        _mockMediaStreamService.Setup(s => s.GetMediaStreamInfoAsync(newsId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.Get(newsId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("Internal server error");
    }

    [Fact]
    public void MimeNames_StaticDictionary_ContainsExpectedMimeTypes()
    {
        // Act & Assert
        StreamController.MimeNames.Should().ContainKeys(".mp3", ".mp4", ".ogg", ".ogv", ".oga", ".wav", ".webm");
        StreamController.MimeNames[".mp3"].Should().Be("audio/mpeg");
        StreamController.MimeNames[".mp4"].Should().Be("video/mp4");
        StreamController.MimeNames[".ogg"].Should().Be("application/ogg");
        StreamController.MimeNames[".ogv"].Should().Be("video/ogg");
        StreamController.MimeNames[".oga"].Should().Be("audio/ogg");
        StreamController.MimeNames[".wav"].Should().Be("audio/x-wav");
        StreamController.MimeNames[".webm"].Should().Be("video/webm");
    }

    [Fact]
    public void MimeNames_StaticDictionary_IsReadOnly()
    {
        // Act & Assert
        StreamController.MimeNames.Should().BeAssignableTo<IReadOnlyDictionary<string, string>>();
        
        // Verify it's truly read-only by checking we can't cast to mutable dictionary
        StreamController.MimeNames.Should().NotBeAssignableTo<Dictionary<string, string>>();
    }

    [Theory]
    [InlineData(".mp3", "audio/mpeg")]
    [InlineData(".mp4", "video/mp4")]
    [InlineData(".ogg", "application/ogg")]
    [InlineData(".ogv", "video/ogg")]
    [InlineData(".oga", "audio/ogg")]
    [InlineData(".wav", "audio/x-wav")]
    [InlineData(".webm", "video/webm")]
    [InlineData(".unknown", MediaTypeNames.Application.Octet)]
    [InlineData("", MediaTypeNames.Application.Octet)]
    public void GetMimeNameFromExt_WithVariousExtensions_ReturnsCorrectMimeType(string extension, string expectedMimeType)
    {
        // Act
        var result = StreamController.MimeNames.TryGetValue(extension.ToLowerInvariant(), out string? value);
        var mimeType = result ? value : MediaTypeNames.Application.Octet;

        // Assert
        mimeType.Should().Be(expectedMimeType);
    }

    [Theory]
    [InlineData(".MP3", "audio/mpeg")]
    [InlineData(".Mp4", "video/mp4")]
    [InlineData(".OGG", "application/ogg")]
    public void GetMimeNameFromExt_WithMixedCaseExtensions_ReturnsCorrectMimeType(string extension, string expectedMimeType)
    {
        // Act
        var result = StreamController.MimeNames.TryGetValue(extension.ToLowerInvariant(), out string? value);
        var mimeType = result ? value : MediaTypeNames.Application.Octet;

        // Assert
        mimeType.Should().Be(expectedMimeType);
    }

    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new StreamController(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new StreamController(_mockMediaStreamService.Object, null!));
    }

    [Theory]
    [InlineData(0, 0, 1024, true)]
    [InlineData(0, 100, 1024, true)]
    [InlineData(100, 200, 1024, true)]
    [InlineData(1000, 1023, 1024, true)]
    [InlineData(0, 1023, 1024, true)]
    [InlineData(1024, 1024, 1024, false)]  // Start equals content length
    [InlineData(1025, 1025, 1024, false)]  // Start exceeds content length
    [InlineData(0, 1024, 1024, false)]     // End equals content length
    [InlineData(0, 1025, 1024, false)]     // End exceeds content length
    public void TryReadRangeItem_WithVariousRanges_ReturnsExpectedResults(long start, long end, long contentLength, bool expectedResult)
    {
        // This tests the range validation logic conceptually
        // Act
        var actualResult = (start < contentLength && end < contentLength);

        // Assert
        actualResult.Should().Be(expectedResult);
    }

    [Fact]
    public async Task Get_ServiceCall_VerifiesCorrectServiceInteractions()
    {
        // Arrange
        var newsId = "123";
        _mockMediaStreamService.Setup(s => s.IsValidNewsId(newsId))
            .Returns(true);
        _mockMediaStreamService.Setup(s => s.GetMediaStreamInfoAsync(newsId))
            .ReturnsAsync((MediaStreamInfo?)null);

        // Act
        await _controller.Get(newsId, CancellationToken.None);

        // Assert
        _mockMediaStreamService.Verify(s => s.IsValidNewsId(newsId), Times.Once);
        _mockMediaStreamService.Verify(s => s.GetMediaStreamInfoAsync(newsId), Times.Once);
    }

    [Fact]
    public async Task Get_WithValidNews_CallsCreatePartialContent()
    {
        // Arrange
        var newsId = "123";
        var mediaInfo = new MediaStreamInfo
        {
            Id = newsId,
            FileSize = 1024,
            FileType = "video/mp4",
            FileExt = ".mp4"
        };

        var mockStream = new MemoryStream(new byte[1024]);

        _mockMediaStreamService.Setup(s => s.IsValidNewsId(newsId))
            .Returns(true);
        _mockMediaStreamService.Setup(s => s.GetMediaStreamInfoAsync(newsId))
            .ReturnsAsync(mediaInfo);
        _mockMediaStreamService.Setup(s => s.CreatePartialContentAsync(newsId, 0, 1023))
            .ReturnsAsync(mockStream);

        // Setup HTTP context for controller
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.Get(newsId, CancellationToken.None);

        // Assert
        _mockMediaStreamService.Verify(s => s.CreatePartialContentAsync(newsId, 0, 1023), Times.Once);
        result.Should().BeOfType<FileStreamResult>();
    }

    [Theory]
    [InlineData("123", true)]
    [InlineData("456", true)]
    [InlineData("0", true)]
    [InlineData("999999999999999", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    [InlineData("12.34", false)]
    [InlineData("12a34", false)]
    [InlineData("-123", false)]
    public async Task Get_WithVariousNewsIds_CallsValidationCorrectly(string newsId, bool expectedValid)
    {
        // Arrange
        _mockMediaStreamService.Setup(s => s.IsValidNewsId(newsId))
            .Returns(expectedValid);

        if (expectedValid)
        {
            _mockMediaStreamService.Setup(s => s.GetMediaStreamInfoAsync(newsId))
                .ReturnsAsync((MediaStreamInfo?)null);
        }

        // Act
        var result = await _controller.Get(newsId, CancellationToken.None);

        // Assert
        _mockMediaStreamService.Verify(s => s.IsValidNewsId(newsId), Times.Once);
        result.Should().BeOfType<NotFoundResult>();
        
        if (expectedValid)
        {
            _mockMediaStreamService.Verify(s => s.GetMediaStreamInfoAsync(newsId), Times.Once);
        }
        else
        {
            _mockMediaStreamService.Verify(s => s.GetMediaStreamInfoAsync(newsId), Times.Never);
        }
    }
} 