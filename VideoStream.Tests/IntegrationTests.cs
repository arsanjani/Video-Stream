using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using VideoStream.Services;
using VideoStream.Models;
using System.Net;
using System.Net.Http.Headers;
using Moq;

namespace VideoStream.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing MediaStreamService
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IMediaStreamService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add a mock MediaStreamService for testing
                var mockService = new Mock<IMediaStreamService>();
                SetupMockMediaStreamService(mockService);
                services.AddSingleton(mockService.Object);
            });
        });

        _client = _factory.CreateClient();
    }

    private void SetupMockMediaStreamService(Mock<IMediaStreamService> mockService)
    {
        // Setup for valid ID
        mockService.Setup(s => s.IsValidId("123")).Returns(true);
        mockService.Setup(s => s.IsValidId("456")).Returns(true);
        mockService.Setup(s => s.IsValidId("789")).Returns(true);
        mockService.Setup(s => s.IsValidId("video_123")).Returns(true);
        mockService.Setup(s => s.IsValidId("media-file")).Returns(true);
        mockService.Setup(s => s.IsValidId("invalid@id")).Returns(false);

        // Setup media stream info
        var mediaInfo = new MediaStreamInfo
        {
            Id = "123",
            FileSize = 1024,
            FileType = "video/mp4",
            FileExt = ".mp4",
            InBuffer = false
        };

        mockService.Setup(s => s.GetMediaStreamInfoAsync("123")).ReturnsAsync(mediaInfo);
        mockService.Setup(s => s.GetMediaStreamInfoAsync("456")).ReturnsAsync((MediaStreamInfo?)null);
        mockService.Setup(s => s.GetMediaStreamInfoAsync("789")).ReturnsAsync(new MediaStreamInfo
        {
            Id = "789",
            FileSize = 0,
            FileType = "video/mp4",
            FileExt = ".mp4"
        });

        // Setup stream creation
        var testData = Enumerable.Range(0, 1024).Select(i => (byte)(i % 256)).ToArray();
        mockService.Setup(s => s.CreatePartialContentAsync("123", It.IsAny<long>(), It.IsAny<long>()))
            .ReturnsAsync((string id, long start, long end) =>
            {
                var length = (int)(end - start + 1);
                var data = testData.Skip((int)start).Take(length).ToArray();
                return new MemoryStream(data);
            });
    }

    [Fact]
    public async Task Get_WithInvalidId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/stream/invalid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_WithValidIdButNoMedia_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/stream/456");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_WithZeroFileSize_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/stream/789");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_WithValidId_Returns200WithCorrectHeaders()
    {
        // Act
        var response = await _client.GetAsync("/api/stream/123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("video/mp4");
        response.Headers.AcceptRanges.Should().Contain("bytes");
        response.Content.Headers.ContentLength.Should().Be(1024);
    }

    [Fact]
    public async Task Get_WithValidId_ReturnsCorrectContent()
    {
        // Act
        var response = await _client.GetAsync("/api/stream/123");
        var content = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content.Length.Should().Be(1024);
        
        // Verify content matches expected pattern
        for (int i = 0; i < content.Length; i++)
        {
            content[i].Should().Be((byte)(i % 256));
        }
    }

    [Fact]
    public async Task Get_WithRangeHeader_Returns206WithPartialContent()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream/123");
        request.Headers.Range = new RangeHeaderValue(0, 99);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentType?.MediaType.Should().Be("video/mp4");
        response.Headers.AcceptRanges.Should().Contain("bytes");
        response.Content.Headers.ContentLength.Should().Be(100);
        response.Content.Headers.ContentRange?.Unit.Should().Be("bytes");
        response.Content.Headers.ContentRange?.From.Should().Be(0);
        response.Content.Headers.ContentRange?.To.Should().Be(99);
        response.Content.Headers.ContentRange?.Length.Should().Be(1024);
    }

    [Fact]
    public async Task Get_WithRangeHeader_ReturnsCorrectPartialContent()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream/123");
        request.Headers.Range = new RangeHeaderValue(100, 199);

        // Act
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        content.Should().NotBeNull();
        content.Length.Should().Be(100);
        
        // Verify content matches expected pattern starting from byte 100
        for (int i = 0; i < content.Length; i++)
        {
            content[i].Should().Be((byte)((100 + i) % 256));
        }
    }

    [Fact]
    public async Task Get_WithMultipleRanges_Returns416()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream/123");
        request.Headers.Range = new RangeHeaderValue();
        request.Headers.Range.Ranges.Add(new RangeItemHeaderValue(0, 99));
        request.Headers.Range.Ranges.Add(new RangeItemHeaderValue(100, 199));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.RequestedRangeNotSatisfiable);
        response.Content.Headers.ContentRange?.Unit.Should().Be("bytes");
        response.Content.Headers.ContentRange?.Length.Should().Be(1024);
    }

    [Fact]
    public async Task Get_WithInvalidRangeUnit_Returns416()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream/123");
        request.Headers.Add("Range", "items=0-99");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.RequestedRangeNotSatisfiable);
    }

    [Fact]
    public async Task Get_WithLastBytesRange_Returns206()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream/123");
        request.Headers.Range = new RangeHeaderValue(null, 100); // Last 100 bytes

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentLength.Should().Be(100);
        response.Content.Headers.ContentRange?.From.Should().Be(924); // 1024 - 100
        response.Content.Headers.ContentRange?.To.Should().Be(1023);
        response.Content.Headers.ContentRange?.Length.Should().Be(1024);
    }

    [Fact]
    public async Task Get_WithFromBytesRange_Returns206()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream/123");
        request.Headers.Range = new RangeHeaderValue(900, null); // From byte 900 to end

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentLength.Should().Be(124); // 1024 - 900
        response.Content.Headers.ContentRange?.From.Should().Be(900);
        response.Content.Headers.ContentRange?.To.Should().Be(1023);
        response.Content.Headers.ContentRange?.Length.Should().Be(1024);
    }

    [Fact]
    public async Task Get_WithRangeExceedingFileSize_Returns416()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stream/123");
        request.Headers.Range = new RangeHeaderValue(1000, 2000); // Beyond file size

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.RequestedRangeNotSatisfiable);
    }

    [Fact]
    public async Task Get_WithHeadRequest_ReturnsHeaders()
    {
        // Act
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/stream/123"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("video/mp4");
        response.Headers.AcceptRanges.Should().Contain("bytes");
        response.Content.Headers.ContentLength.Should().Be(1024);
    }

    [Theory]
    [InlineData("/api/stream/123", "video/mp4")]
    [InlineData("/API/STREAM/123", "video/mp4")] // Case insensitive routing
    public async Task Get_WithDifferentCasing_Works(string url, string expectedContentType)
    {
        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be(expectedContentType);
    }

    [Fact]
    public async Task Get_WithLongId_ProcessesCorrectly()
    {
        // Arrange
        var longId = "1234567890123456789";
        
        // Create a new factory with additional mock setup
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IMediaStreamService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var mockService = new Mock<IMediaStreamService>();
                SetupMockMediaStreamService(mockService);
                
                // Add specific setup for long ID
                mockService.Setup(s => s.IsValidId(longId)).Returns(true);
                mockService.Setup(s => s.GetMediaStreamInfoAsync(longId)).ReturnsAsync(new MediaStreamInfo
                {
                    Id = longId,
                    FileSize = 512,
                    FileType = "video/mp4",
                    FileExt = ".mp4"
                });
                mockService.Setup(s => s.CreatePartialContentAsync(longId, 0, 511))
                    .ReturnsAsync(new MemoryStream(new byte[512]));

                services.AddSingleton(mockService.Object);
            });
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/stream/{longId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentLength.Should().Be(512);
    }

    [Fact]
    public async Task Get_ConcurrentRequests_HandlesCorrectly()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();
        const int concurrentRequests = 10;

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_client.GetAsync("/api/stream/123"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(concurrentRequests);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        responses.Should().OnlyContain(r => r.Content.Headers.ContentLength == 1024);
    }

    [Fact]
    public async Task Get_WithCancellationToken_CancelsGracefully()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _client.GetAsync("/api/stream/123", cancellationTokenSource.Token));
    }

    [Fact]
    public async Task Get_WithSpecialCharactersInId_HandlesCorrectly()
    {
        // Arrange
        var specialId = "123@invalid";
        
        // The special characters should be invalid according to the IsValidId logic
        // which only allows alphanumeric, hyphens, and underscores

        // Act
        var response = await _client.GetAsync($"/api/stream/{specialId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_WithValidStringId_Returns200()
    {
        // Arrange
        var stringId = "video_123";
        
        // Create a new factory with additional mock setup for string ID
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IMediaStreamService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var mockService = new Mock<IMediaStreamService>();
                SetupMockMediaStreamService(mockService);
                
                // Add specific setup for string ID
                mockService.Setup(s => s.IsValidId(stringId)).Returns(true);
                mockService.Setup(s => s.GetMediaStreamInfoAsync(stringId)).ReturnsAsync(new MediaStreamInfo
                {
                    Id = stringId,
                    FileSize = 1024,
                    FileType = "video/mp4",
                    FileExt = ".mp4"
                });
                mockService.Setup(s => s.CreatePartialContentAsync(stringId, 0, 1023))
                    .ReturnsAsync(new MemoryStream(new byte[1024]));

                services.AddSingleton(mockService.Object);
            });
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/stream/{stringId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentLength.Should().Be(1024);
    }

    [Fact]
    public async Task Get_WithOptionsRequest_ReturnsCorrectCorsHeaders()
    {
        // Act
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/api/stream/123"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
        response.Headers.Should().ContainKey("Access-Control-Allow-Headers");
    }
} 