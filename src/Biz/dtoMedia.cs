﻿namespace VideoStream.DTOs;

public class MediaDto
{
    public long FileSize { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string FileExt { get; set; } = string.Empty;
    public bool InBuffer { get; set; }
    public string? BufferPath { get; set; }
}