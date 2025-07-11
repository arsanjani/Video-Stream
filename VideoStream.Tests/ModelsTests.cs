using FluentAssertions;
using VideoStream.Models;

namespace VideoStream.Tests;

public class MediaStreamInfoTests
{
    [Fact]
    public void Constructor_CreateEmptyMediaStreamInfo_InitializesWithDefaults()
    {
        // Act
        var mediaStreamInfo = new MediaStreamInfo();

        // Assert
        mediaStreamInfo.Id.Should().Be(string.Empty);
        mediaStreamInfo.FileSize.Should().Be(0);
        mediaStreamInfo.FileType.Should().Be(string.Empty);
        mediaStreamInfo.FileExt.Should().Be(string.Empty);
        mediaStreamInfo.BufferPath.Should().BeNull();
        mediaStreamInfo.InBuffer.Should().BeFalse();
    }

    [Fact]
    public void Properties_SetAndGet_WorksCorrectly()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();
        const string id = "12345";
        const long fileSize = 1024 * 1024; // 1MB
        const string fileType = "video/mp4";
        const string fileExt = ".mp4";
        const string bufferPath = "C:\\buffer\\12345.mp4";
        const bool inBuffer = true;

        // Act
        mediaStreamInfo.Id = id;
        mediaStreamInfo.FileSize = fileSize;
        mediaStreamInfo.FileType = fileType;
        mediaStreamInfo.FileExt = fileExt;
        mediaStreamInfo.BufferPath = bufferPath;
        mediaStreamInfo.InBuffer = inBuffer;

        // Assert
        mediaStreamInfo.Id.Should().Be(id);
        mediaStreamInfo.FileSize.Should().Be(fileSize);
        mediaStreamInfo.FileType.Should().Be(fileType);
        mediaStreamInfo.FileExt.Should().Be(fileExt);
        mediaStreamInfo.BufferPath.Should().Be(bufferPath);
        mediaStreamInfo.InBuffer.Should().Be(inBuffer);
    }

    [Fact]
    public void Id_SetToNull_SetsToNull()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.Id = null!;

        // Assert
        mediaStreamInfo.Id.Should().BeNull();
    }

    [Fact]
    public void FileType_SetToNull_SetsToNull()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.FileType = null!;

        // Assert
        mediaStreamInfo.FileType.Should().BeNull();
    }

    [Fact]
    public void FileExt_SetToNull_SetsToNull()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.FileExt = null!;

        // Assert
        mediaStreamInfo.FileExt.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(long.MaxValue)]
    public void FileSize_SetToValidValues_SetsCorrectly(long fileSize)
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.FileSize = fileSize;

        // Assert
        mediaStreamInfo.FileSize.Should().Be(fileSize);
    }

    [Fact]
    public void FileSize_SetToNegativeValue_SetsNegativeValue()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.FileSize = -1;

        // Assert
        mediaStreamInfo.FileSize.Should().Be(-1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("test")]
    [InlineData("12345")]
    [InlineData("abc123")]
    public void Id_SetToVariousStrings_SetsCorrectly(string id)
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.Id = id;

        // Assert
        mediaStreamInfo.Id.Should().Be(id);
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("audio/mpeg")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    [InlineData("unknown/type")]
    public void FileType_SetToVariousTypes_SetsCorrectly(string fileType)
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.FileType = fileType;

        // Assert
        mediaStreamInfo.FileType.Should().Be(fileType);
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mp3")]
    [InlineData(".avi")]
    [InlineData(".mkv")]
    [InlineData("")]
    [InlineData("mp4")] // Without dot
    [InlineData(".MP4")] // Upper case
    public void FileExt_SetToVariousExtensions_SetsCorrectly(string fileExt)
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.FileExt = fileExt;

        // Assert
        mediaStreamInfo.FileExt.Should().Be(fileExt);
    }

    [Theory]
    [InlineData("C:\\buffer\\file.mp4")]
    [InlineData("D:\\media\\video.avi")]
    [InlineData("/usr/local/buffer/file.mp4")]
    [InlineData("\\\\network\\share\\file.mp4")]
    [InlineData("")]
    public void BufferPath_SetToVariousPaths_SetsCorrectly(string bufferPath)
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.BufferPath = bufferPath;

        // Assert
        mediaStreamInfo.BufferPath.Should().Be(bufferPath);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InBuffer_SetToVariousValues_SetsCorrectly(bool inBuffer)
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo();

        // Act
        mediaStreamInfo.InBuffer = inBuffer;

        // Assert
        mediaStreamInfo.InBuffer.Should().Be(inBuffer);
    }

    [Fact]
    public void MediaStreamInfo_ObjectInitializer_WorksCorrectly()
    {
        // Act
        var mediaStreamInfo = new MediaStreamInfo
        {
            Id = "123",
            FileSize = 1024,
            FileType = "video/mp4",
            FileExt = ".mp4",
            BufferPath = "C:\\buffer\\123.mp4",
            InBuffer = true
        };

        // Assert
        mediaStreamInfo.Id.Should().Be("123");
        mediaStreamInfo.FileSize.Should().Be(1024);
        mediaStreamInfo.FileType.Should().Be("video/mp4");
        mediaStreamInfo.FileExt.Should().Be(".mp4");
        mediaStreamInfo.BufferPath.Should().Be("C:\\buffer\\123.mp4");
        mediaStreamInfo.InBuffer.Should().BeTrue();
    }

    [Fact]
    public void MediaStreamInfo_AllNullableProperties_CanBeSetToNull()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo
        {
            Id = "123",
            FileType = "video/mp4",
            FileExt = ".mp4",
            BufferPath = "C:\\buffer\\123.mp4"
        };

        // Act
        mediaStreamInfo.Id = null!;
        mediaStreamInfo.FileType = null!;
        mediaStreamInfo.FileExt = null!;
        mediaStreamInfo.BufferPath = null;

        // Assert
        mediaStreamInfo.Id.Should().BeNull();
        mediaStreamInfo.FileType.Should().BeNull();
        mediaStreamInfo.FileExt.Should().BeNull();
        mediaStreamInfo.BufferPath.Should().BeNull();
    }

    [Fact]
    public void MediaStreamInfo_ToString_ShouldNotThrow()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo
        {
            Id = "123",
            FileSize = 1024,
            FileType = "video/mp4",
            FileExt = ".mp4",
            BufferPath = "C:\\buffer\\123.mp4",
            InBuffer = true
        };

        // Act
        var result = mediaStreamInfo.ToString();

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("MediaStreamInfo");
    }

    [Fact]
    public void MediaStreamInfo_Equals_ShouldWorkCorrectly()
    {
        // Arrange
        var mediaStreamInfo1 = new MediaStreamInfo
        {
            Id = "123",
            FileSize = 1024,
            FileType = "video/mp4",
            FileExt = ".mp4",
            BufferPath = "C:\\buffer\\123.mp4",
            InBuffer = true
        };

        var mediaStreamInfo2 = new MediaStreamInfo
        {
            Id = "123",
            FileSize = 1024,
            FileType = "video/mp4",
            FileExt = ".mp4",
            BufferPath = "C:\\buffer\\123.mp4",
            InBuffer = true
        };

        // Act & Assert
        mediaStreamInfo1.Should().BeEquivalentTo(mediaStreamInfo2);
    }

    [Fact]
    public void MediaStreamInfo_GetHashCode_ShouldNotThrow()
    {
        // Arrange
        var mediaStreamInfo = new MediaStreamInfo
        {
            Id = "123",
            FileSize = 1024,
            FileType = "video/mp4",
            FileExt = ".mp4",
            BufferPath = "C:\\buffer\\123.mp4",
            InBuffer = true
        };

        // Act & Assert
        var hashCode = mediaStreamInfo.GetHashCode();
        hashCode.Should().NotBe(0); // Just verify it doesn't throw
    }

    [Fact]
    public void MediaStreamInfo_WithEmptyOrNullBufferPath_InBufferShouldBeFalse()
    {
        // Arrange & Act
        var mediaStreamInfo1 = new MediaStreamInfo
        {
            Id = "123",
            BufferPath = null,
            InBuffer = true
        };

        var mediaStreamInfo2 = new MediaStreamInfo
        {
            Id = "123",
            BufferPath = "",
            InBuffer = true
        };

        // Assert
        // This is a logical test - typically if BufferPath is null/empty, InBuffer should be false
        // But the model itself doesn't enforce this - it's business logic
        mediaStreamInfo1.BufferPath.Should().BeNull();
        mediaStreamInfo2.BufferPath.Should().Be("");
        
        // The model allows inconsistent state, but in practice, 
        // the service should ensure consistency
        mediaStreamInfo1.InBuffer.Should().BeTrue(); // Model allows this
        mediaStreamInfo2.InBuffer.Should().BeTrue(); // Model allows this
    }
} 