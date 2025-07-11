using FluentAssertions;
using Moq;
using Moq.Protected;
using VideoStream.Models;

namespace VideoStream.Tests;

public class PartialStreamTests : IDisposable
{
    private MemoryStream _baseStream;
    private readonly byte[] _testData;

    public PartialStreamTests()
    {
        _testData = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        _baseStream = new MemoryStream(_testData);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesPartialStream()
    {
        // Act
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Assert
        partialStream.Should().NotBeNull();
        partialStream.Length.Should().Be(11); // 20 - 10 + 1
        partialStream.Position.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullBaseStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PartialStream(null!, 0, 10));
    }

    [Fact]
    public void Constructor_WithStartGreaterThanEnd_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PartialStream(_baseStream, 20, 10));
    }

    [Fact]
    public void Constructor_WithEqualStartAndEnd_CreatesValidStream()
    {
        // Act
        var partialStream = new PartialStream(_baseStream, 10, 10);

        // Assert
        partialStream.Length.Should().Be(1);
        partialStream.Position.Should().Be(0);
    }

    [Fact]
    public void CanRead_ReturnsBaseStreamCanRead()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 0, 10);

        // Act & Assert
        partialStream.CanRead.Should().Be(_baseStream.CanRead);
    }

    [Fact]
    public void CanSeek_ReturnsBaseStreamCanSeek()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 0, 10);

        // Act & Assert
        partialStream.CanSeek.Should().Be(_baseStream.CanSeek);
    }

    [Fact]
    public void CanWrite_ReturnsFalse()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 0, 10);

        // Act & Assert
        partialStream.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Length_ReturnsCorrectLength()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act & Assert
        partialStream.Length.Should().Be(11); // 20 - 10 + 1
    }

    [Fact]
    public void Position_SetWithValidValue_UpdatesPosition()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act
        partialStream.Position = 5;

        // Assert
        partialStream.Position.Should().Be(5);
    }

    [Fact]
    public void Position_SetWithNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => partialStream.Position = -1);
    }

    [Fact]
    public void Position_SetWithValueGreaterThanLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => partialStream.Position = 12);
    }

    [Fact]
    public void Read_WithValidBuffer_ReturnsCorrectData()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        var buffer = new byte[5];

        // Act
        var bytesRead = partialStream.Read(buffer, 0, 5);

        // Assert
        bytesRead.Should().Be(5);
        buffer.Should().BeEquivalentTo(new byte[] { 10, 11, 12, 13, 14 });
        partialStream.Position.Should().Be(5);
    }

    [Fact]
    public void Read_AtEndOfStream_ReturnsZero()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        partialStream.Position = 11; // End of stream
        var buffer = new byte[5];

        // Act
        var bytesRead = partialStream.Read(buffer, 0, 5);

        // Assert
        bytesRead.Should().Be(0);
    }

    [Fact]
    public void Read_WithCountGreaterThanRemainingBytes_ReturnsRemainingBytes()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        partialStream.Position = 8; // 3 bytes remaining
        var buffer = new byte[10];

        // Act
        var bytesRead = partialStream.Read(buffer, 0, 10);

        // Assert
        bytesRead.Should().Be(3);
        buffer.Take(3).Should().BeEquivalentTo(new byte[] { 18, 19, 20 });
    }

    [Fact]
    public async Task ReadAsync_WithValidBuffer_ReturnsCorrectData()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        var buffer = new byte[5];

        // Act
        var bytesRead = await partialStream.ReadAsync(buffer, 0, 5, CancellationToken.None);

        // Assert
        bytesRead.Should().Be(5);
        buffer.Should().BeEquivalentTo(new byte[] { 10, 11, 12, 13, 14 });
        partialStream.Position.Should().Be(5);
    }

    [Fact]
    public async Task ReadAsync_AtEndOfStream_ReturnsZero()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        partialStream.Position = 11; // End of stream
        var buffer = new byte[5];

        // Act
        var bytesRead = await partialStream.ReadAsync(buffer, 0, 5, CancellationToken.None);

        // Assert
        bytesRead.Should().Be(0);
    }

    [Fact]
    public async Task ReadAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        var buffer = new byte[5];
        var cancellationToken = new CancellationToken(true);

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            partialStream.ReadAsync(buffer, 0, 5, cancellationToken));
        
        // TaskCanceledException inherits from OperationCanceledException, so either is acceptable
        exception.Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public void Seek_WithBeginOrigin_SetsPositionFromStart()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act
        var newPosition = partialStream.Seek(5, SeekOrigin.Begin);

        // Assert
        newPosition.Should().Be(5);
        partialStream.Position.Should().Be(5);
    }

    [Fact]
    public void Seek_WithCurrentOrigin_SetsPositionFromCurrent()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        partialStream.Position = 5;

        // Act
        var newPosition = partialStream.Seek(3, SeekOrigin.Current);

        // Assert
        newPosition.Should().Be(8);
        partialStream.Position.Should().Be(8);
    }

    [Fact]
    public void Seek_WithEndOrigin_SetsPositionFromEnd()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act
        var newPosition = partialStream.Seek(-2, SeekOrigin.End);

        // Assert
        newPosition.Should().Be(9);
        partialStream.Position.Should().Be(9);
    }

    [Fact]
    public void Seek_WithInvalidOrigin_ThrowsArgumentException()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => partialStream.Seek(0, (SeekOrigin)999));
    }

    [Fact]
    public void Seek_WithOffsetOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => partialStream.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<ArgumentOutOfRangeException>(() => partialStream.Seek(12, SeekOrigin.Begin));
    }

    [Fact]
    public void SetLength_ThrowsNotSupportedException()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => partialStream.SetLength(100));
    }

    [Fact]
    public void Write_ThrowsNotSupportedException()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        var buffer = new byte[5];

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => partialStream.Write(buffer, 0, 5));
    }

    [Fact]
    public void Flush_CallsBaseStreamFlush()
    {
        // Arrange
        var mockBaseStream = new Mock<Stream>();
        mockBaseStream.Setup(s => s.CanRead).Returns(true);
        mockBaseStream.Setup(s => s.CanSeek).Returns(true);
        var partialStream = new PartialStream(mockBaseStream.Object, 0, 10);

        // Act
        partialStream.Flush();

        // Assert
        mockBaseStream.Verify(s => s.Flush(), Times.Once);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 10)]
    [InlineData(50, 60)]
    [InlineData(90, 99)]
    public void Constructor_WithValidRanges_CreatesCorrectStream(long start, long end)
    {
        // Act
        var partialStream = new PartialStream(_baseStream, start, end);

        // Assert
        partialStream.Length.Should().Be(end - start + 1);
        partialStream.Position.Should().Be(0);
    }

    [Fact]
    public void Read_WithMultipleReads_ReturnsCorrectSequentialData()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        var buffer1 = new byte[5];
        var buffer2 = new byte[6];

        // Act
        var bytesRead1 = partialStream.Read(buffer1, 0, 5);
        var bytesRead2 = partialStream.Read(buffer2, 0, 6);

        // Assert
        bytesRead1.Should().Be(5);
        bytesRead2.Should().Be(6);
        buffer1.Should().BeEquivalentTo(new byte[] { 10, 11, 12, 13, 14 });
        buffer2.Should().BeEquivalentTo(new byte[] { 15, 16, 17, 18, 19, 20 });
        partialStream.Position.Should().Be(11);
    }

    [Fact]
    public void Read_WithZeroCount_ReturnsZero()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        var buffer = new byte[5];

        // Act
        var bytesRead = partialStream.Read(buffer, 0, 0);

        // Assert
        bytesRead.Should().Be(0);
        partialStream.Position.Should().Be(0);
    }

    [Fact]
    public void Position_WithSeekableBaseStream_UpdatesBaseStreamPosition()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act
        partialStream.Position = 5;

        // Assert
        _baseStream.Position.Should().Be(15); // 10 + 5
    }

    [Fact]
    public void Dispose_DisposesBaseStream()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var partialStream = new PartialStream(baseStream, 0, 10);

        // Act
        partialStream.Dispose();

        // Assert
        baseStream.CanRead.Should().BeFalse();
    }

    [Fact]
    public void Dispose_MultipleDisposeCalls_DoesNotThrow()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var partialStream = new PartialStream(baseStream, 10, 20);

        // Act & Assert
        partialStream.Dispose();
        Action act = () => partialStream.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithZeroLengthRange_CreatesValidStream()
    {
        // Act
        var partialStream = new PartialStream(_baseStream, 50, 50);

        // Assert
        partialStream.Length.Should().Be(1);
        partialStream.Position.Should().Be(0);
    }

    [Fact]
    public void Read_WithPartialBuffer_ReturnsCorrectData()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        var buffer = new byte[10];

        // Act
        var bytesRead = partialStream.Read(buffer, 2, 5); // Read 5 bytes starting at offset 2

        // Assert
        bytesRead.Should().Be(5);
        buffer.Skip(2).Take(5).Should().BeEquivalentTo(new byte[] { 10, 11, 12, 13, 14 });
        partialStream.Position.Should().Be(5);
    }

    [Fact]
    public void Seek_WithNegativeCurrentPosition_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);
        partialStream.Position = 5;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => partialStream.Seek(-10, SeekOrigin.Current));
    }

    [Fact]
    public void Seek_WithEndOriginAndPositiveOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var partialStream = new PartialStream(_baseStream, 10, 20);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => partialStream.Seek(1, SeekOrigin.End));
    }

    public void Dispose()
    {
        _baseStream?.Dispose();
    }
} 