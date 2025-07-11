namespace VideoStream.Models;

public class PartialStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _start;
    private readonly long _end;
    private long _position;
    private bool _disposed = false;

    public PartialStream(Stream baseStream, long start, long end)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _start = start;
        _end = end;
        _position = start;
        
        if (start > end)
            throw new ArgumentException("Start position cannot be greater than end position");
            
        // Set the base stream position to the start position
        if (_baseStream.CanSeek)
            _baseStream.Position = _start;
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _end - _start + 1;

    public override long Position
    {
        get => _position - _start;
        set
        {
            if (value < 0 || value > Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            
            _position = _start + value;
            if (_baseStream.CanSeek)
                _baseStream.Position = _position;
        }
    }

    public override void Flush()
    {
        _baseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remainingBytes = _end - _position + 1;
        var bytesToRead = Math.Min(count, (int)remainingBytes);
        
        if (bytesToRead <= 0)
            return 0;

        // Ensure base stream is positioned correctly
        if (_baseStream.CanSeek && _baseStream.Position != _position)
            _baseStream.Position = _position;

        var bytesRead = _baseStream.Read(buffer, offset, bytesToRead);
        _position += bytesRead;
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var remainingBytes = _end - _position + 1;
        var bytesToRead = Math.Min(count, (int)remainingBytes);
        
        if (bytesToRead <= 0)
            return 0;

        // Ensure base stream is positioned correctly
        if (_baseStream.CanSeek && _baseStream.Position != _position)
            _baseStream.Position = _position;

        var bytesRead = await _baseStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
        _position += bytesRead;
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => _start + offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _end + 1 + offset, // End is inclusive, so we add 1
            _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
        };

        if (newPosition < _start || newPosition > _end)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _position = newPosition;
        if (_baseStream.CanSeek)
            _baseStream.Position = _position;
        
        return _position - _start;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("Cannot set length on a partial stream");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Cannot write to a partial stream");
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _baseStream?.Dispose();
            _disposed = true;
        }
        base.Dispose(disposing);
    }
} 