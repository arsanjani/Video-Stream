using Microsoft.Extensions.Caching.Memory;
using VideoStream.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure memory cache
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = builder.Configuration.GetValue<long>("Caching:SizeLimit", 1024);
    options.CompactionPercentage = builder.Configuration.GetValue<double>("Caching:CompactionPercentage", 0.25);
});

// Add custom services
builder.Services.AddScoped<IFileSystemService, FileSystemService>();
builder.Services.AddScoped<IMediaStreamService, MediaStreamService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure HTTP client
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program class available for integration testing
public partial class Program { } 