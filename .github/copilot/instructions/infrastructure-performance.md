# Infrastructure Performance Optimization Guide

## Overview
This guide covers production-ready performance optimizations for Oxpecker applications, focusing on Kestrel server configuration, response compression, caching strategies, and deployment optimization.

## 1. Kestrel Server Configuration

### Connection Limits and Threading

**Default Configuration (for reference):**
```fsharp
open Microsoft.AspNetCore.Server.Kestrel.Core

builder.WebHost.ConfigureKestrel(fun options ->
    // Connection limits
    options.Limits.MaxConcurrentConnections <- Nullable<int64>(100L)
    options.Limits.MaxConcurrentUpgradedConnections <- Nullable<int64>(100L)

    // Request body size
    options.Limits.MaxRequestBodySize <- Nullable<int64>(30_000_000L) // 30 MB

    // Timeouts
    options.Limits.KeepAliveTimeout <- TimeSpan.FromMinutes(2.0)
    options.Limits.RequestHeadersTimeout <- TimeSpan.FromSeconds(30.0)
)
```

**High-Traffic Configuration:**
```fsharp
builder.WebHost.ConfigureKestrel(fun options ->
    // Increase connection limits for high traffic
    options.Limits.MaxConcurrentConnections <- Nullable()  // No limit
    options.Limits.MaxConcurrentUpgradedConnections <- Nullable()

    // Optimize for many small requests
    options.Limits.Http2.MaxStreamsPerConnection <- 100
    options.Limits.Http2.InitialConnectionWindowSize <- 131072

    // Reduce timeouts for faster connection recycling
    options.Limits.KeepAliveTimeout <- TimeSpan.FromSeconds(30.0)
)
```

**Measurement Strategy:**
```bash
# Monitor connection metrics
dotnet-counters monitor --process-id <pid> \
    --counters System.Net.Sockets,Microsoft.AspNetCore.Server.Kestrel

# Load test different configurations
bombardier -c 500 -d 30s -l http://localhost:5000/api/test
```

### HTTP/2 and HTTP/3 Optimization

```fsharp
builder.WebHost.ConfigureKestrel(fun serverOptions ->
    serverOptions.ListenAnyIP(5001, fun listenOptions ->
        listenOptions.Protocols <- HttpProtocols.Http1AndHttp2AndHttp3
        listenOptions.UseHttps()
    )

    // HTTP/2 specific optimizations
    serverOptions.Limits.Http2.MaxFrameSize <- 16384
    serverOptions.Limits.Http2.MaxStreamsPerConnection <- 100
    serverOptions.Limits.Http2.HeaderTableSize <- 4096
)
```

## 2. Response Compression

### Configuration

**Basic Setup:**
```fsharp
open Microsoft.AspNetCore.ResponseCompression
open System.IO.Compression

// Add services
builder.Services.AddResponseCompression(fun options ->
    options.EnableForHttps <- true
    options.Providers.Add<BrotliCompressionProvider>()
    options.Providers.Add<GzipCompressionProvider>()
)

// Configure compression levels
builder.Services.Configure<BrotliCompressionProviderOptions>(fun options ->
    options.Level <- CompressionLevel.Fastest  // Balance speed vs compression
)
builder.Services.Configure<GzipCompressionProviderOptions>(fun options ->
    options.Level <- CompressionLevel.Fastest
)

// Use middleware (ORDER MATTERS - add early in pipeline)
app.UseResponseCompression()
```

**MIME Type Configuration:**
```fsharp
builder.Services.AddResponseCompression(fun options ->
    options.EnableForHttps <- true
    options.MimeTypes <- ResponseCompressionDefaults.MimeTypes
        |> Seq.append [
            "application/json"
            "text/html"
            "text/css"
            "application/javascript"
            "text/plain"
            "application/xml"
            "text/xml"
        ]
)
```

**Performance Impact:**
- Brotli: 20-30% better compression than Gzip, 10-20% slower
- Gzip (Fastest): 40-60% size reduction, minimal CPU overhead
- Typical API response (10KB JSON): Gzip reduces to ~2KB

**When NOT to Compress:**
- Responses < 1KB (overhead exceeds benefit)
- Already compressed content (images, videos, pre-gzipped assets)
- Real-time streaming responses
- High-frequency endpoints where CPU is bottleneck

### Custom Compression Provider

```fsharp
type SelectiveCompressionProvider() =
    interface ICompressionProvider with
        member _.SupportsFlush = true
        member _.EncodingName = "br"
        member _.CreateStream(outputStream: Stream) =
            // Only compress if response is large enough
            new BrotliStream(outputStream, CompressionLevel.Fastest) :> Stream

// Register
builder.Services.Configure<ResponseCompressionOptions>(fun options ->
    options.Providers.Add<SelectiveCompressionProvider>()
)
```

## 3. Response Caching

### In-Memory Caching

```fsharp
open Microsoft.Extensions.Caching.Memory
open Microsoft.AspNetCore.Http

// Add caching services
builder.Services.AddMemoryCache(fun options ->
    options.SizeLimit <- Nullable<int64>(100L)  // Limit cache size
)

// Example caching endpoint
let getCachedData (cache: IMemoryCache) : HttpHandler =
    fun ctx ->
        task {
            let cacheKey = "expensive-operation"

            match cache.TryGetValue<string>(cacheKey) with
            | true, value ->
                return! ctx.WriteTextAsync(value)
            | false, _ ->
                let! result = expensiveOperation()  // Your expensive logic

                let cacheOptions = MemoryCacheEntryOptions()
                cacheOptions.AbsoluteExpirationRelativeToNow <- TimeSpan.FromMinutes(5.0)
                cacheOptions.Size <- Nullable<int64>(1L)

                cache.Set(cacheKey, result, cacheOptions) |> ignore
                return! ctx.WriteTextAsync(result)
        }
```

### Response Caching Middleware

```fsharp
open Microsoft.AspNetCore.ResponseCaching

// Add services
builder.Services.AddResponseCaching(fun options ->
    options.MaximumBodySize <- 1024L * 1024L  // 1 MB
    options.UseCaseSensitivePaths <- false
)

// Apply middleware (MUST be before routing)
app.UseResponseCaching()

// Mark cacheable endpoints
let cacheableEndpoint : HttpHandler =
    fun ctx ->
        ctx.Response.Headers.CacheControl <- "public, max-age=300"
        task { return! ctx.WriteTextAsync("Cached response") }
```

**Cache Strategy Guidelines:**
- **Static content:** 1 day - 1 year
- **API responses (stable):** 5-30 minutes
- **User-specific data:** Private cache, short duration
- **Real-time data:** No cache

### Distributed Caching (Redis/SQL Server)

```fsharp
// For multi-instance deployments
builder.Services.AddStackExchangeRedisCache(fun options ->
    options.Configuration <- "localhost:6379"
    options.InstanceName <- "OxpeckerApp:"
)

// Usage
open Microsoft.Extensions.Caching.Distributed

let getDistributedCachedData (cache: IDistributedCache) : HttpHandler =
    fun ctx ->
        task {
            let cacheKey = "shared-data"
            let! cachedBytes = cache.GetAsync(cacheKey)

            match cachedBytes with
            | null ->
                let! data = fetchData()
                let bytes = System.Text.Encoding.UTF8.GetBytes(data)

                let options = DistributedCacheEntryOptions()
                options.AbsoluteExpirationRelativeToNow <- TimeSpan.FromMinutes(10.0)

                do! cache.SetAsync(cacheKey, bytes, options)
                return! ctx.WriteTextAsync(data)
            | bytes ->
                let data = System.Text.Encoding.UTF8.GetString(bytes)
                return! ctx.WriteTextAsync(data)
        }
```

## 4. Database Connection Pooling

### ADO.NET Connection String Optimization

```fsharp
let connectionString =
    "Server=myserver;Database=mydb;User Id=user;Password=pass;" +
    "Min Pool Size=10;" +           // Minimum connections kept alive
    "Max Pool Size=100;" +           // Maximum connections
    "Connection Lifetime=300;" +     // Recycle connections (seconds)
    "Connection Timeout=15;" +       // Connection timeout (seconds)
    "Pooling=true"
```

**Pool Sizing Guidelines:**
- Min Pool Size: 5-10 per app instance
- Max Pool Size: (# CPU cores) * 2 + effective_spindle_count
- For cloud: Start conservative (20-50), tune based on monitoring

### Monitoring Connection Pool Exhaustion

```bash
# Monitor active connections
dotnet-counters monitor --process-id <pid> \
    --counters System.Data.SqlClient

# Watch for "NumberOfPooledConnections" reaching Max Pool Size
```

## 5. Static File Serving

### Efficient Static File Configuration

```fsharp
open Microsoft.AspNetCore.StaticFiles
open Microsoft.Extensions.FileProviders

let staticFileOptions = StaticFileOptions()
staticFileOptions.OnPrepareResponse <- fun ctx ->
    // Aggressive caching for static assets
    let headers = ctx.Context.Response.GetTypedHeaders()
    headers.CacheControl <-
        Microsoft.Net.Http.Headers.CacheControlHeaderValue(
            Public = true,
            MaxAge = TimeSpan.FromDays(365.0)
        )

app.UseStaticFiles(staticFileOptions)

// Use file provider for embedded resources
let provider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly())
app.UseStaticFiles(StaticFileOptions(FileProvider = provider))
```

### Content Delivery Network (CDN) Integration

```fsharp
// Offload static assets to CDN
let cdnPrefix = "https://cdn.example.com"

let assetUrl (path: string) =
    if app.Environment.IsProduction() then
        $"{cdnPrefix}/{path}"
    else
        $"/{path}"

// In views
img [ src (assetUrl "images/logo.png") ]
```

## 6. Logging Performance

### High-Performance Logging

```fsharp
open Microsoft.Extensions.Logging

// Use source-generated logging for zero-allocation
type MyService(logger: ILogger<MyService>) =

    [<LoggerMessage(EventId = 1, Level = LogLevel.Information,
                    Message = "Processing request for {UserId}")>]
    static member LogProcessingRequest(logger: ILogger, userId: string) = ()

    member this.ProcessRequest(userId: string) =
        MyService.LogProcessingRequest(logger, userId)
        // ... process logic
```

**Logging Level Recommendations:**
- **Production:** Warning and Error only
- **Staging:** Information for debugging
- **Development:** Debug or Trace

### Conditional Logging

```fsharp
// Avoid expensive string formatting when not needed
if logger.IsEnabled(LogLevel.Debug) then
    logger.LogDebug($"Complex debug info: {expensiveToStringOperation()}")
```

## 7. Deployment Optimization

### ReadyToRun (R2R) Compilation

```xml
<PropertyGroup>
  <!-- Ahead-of-time compilation for faster startup -->
  <PublishReadyToRun>true</PublishReadyToRun>

  <!-- Trim unused assemblies (careful with reflection) -->
  <PublishTrimmed>false</PublishTrimmed>

  <!-- Single-file deployment -->
  <PublishSingleFile>true</PublishSingleFile>
</PropertyGroup>
```

**Benefits:**
- 30-50% faster startup time
- Reduced JIT overhead
- Trade-off: Larger binary size (~20-30% increase)

### Tiered Compilation

```fsharp
// Default in .NET 9, but can configure
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(fun options ->
    options.AddServerHeader <- false  // Remove server header for security
)

// Environment variable for tuning
// DOTNET_TieredCompilation=1 (default in .NET 9)
// DOTNET_TieredCompilation_QuickJitForLoops=1
```

### Container Optimization

```dockerfile
# Use Alpine for smaller images
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Build with ReadyToRun
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish \
    --runtime linux-musl-x64 \
    --self-contained false \
    /p:PublishReadyToRun=true

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

## 8. Measurement and Monitoring

### Key Performance Indicators (KPIs)

```bash
# Monitor runtime performance
dotnet-counters monitor --process-id <pid> \
    --counters System.Runtime,Microsoft.AspNetCore.Hosting

# Key metrics to watch:
# - cpu-usage (%)
# - working-set (MB)
# - gc-heap-size (MB)
# - gen-0-gc-count, gen-1-gc-count, gen-2-gc-count
# - threadpool-thread-count
# - requests-per-second
# - total-requests
# - failed-requests
```

### Performance Testing Checklist

1. **Baseline Measurements:**
   ```bash
   # Throughput test
   bombardier -c 125 -d 60s http://localhost:5000/api/endpoint

   # Latency percentiles
   wrk -t 4 -c 100 -d 30s --latency http://localhost:5000/api/endpoint
   ```

2. **Configuration Changes:**
   - Test one change at a time
   - Document baseline vs optimized metrics
   - Verify no regressions in functionality

3. **Production Validation:**
   - Gradual rollout (canary/blue-green deployment)
   - Monitor error rates and latencies
   - Have rollback plan ready

## 9. Common Performance Anti-Patterns

### What NOT to Do

1. **Premature Optimization:**
   - Measure first, optimize second
   - Focus on actual bottlenecks, not theoretical concerns

2. **Excessive Caching:**
   - Don't cache everything
   - Consider cache invalidation complexity
   - Monitor memory usage

3. **Synchronous I/O:**
   ```fsharp
   // BAD: Blocking call
   let data = File.ReadAllText("file.txt")

   // GOOD: Async I/O
   let! data = File.ReadAllTextAsync("file.txt")
   ```

4. **N+1 Query Problems:**
   ```fsharp
   // BAD: One query per item
   for item in items do
       let! details = db.GetDetails(item.Id)

   // GOOD: Single batch query
   let ids = items |> List.map (fun item -> item.Id)
   let! allDetails = db.GetDetailsBatch(ids)
   ```

5. **Memory Leaks:**
   - Dispose IDisposable resources
   - Unsubscribe from events
   - Use `use` bindings in F#

## 10. Performance Tuning Workflow

### Systematic Optimization Process

1. **Identify Bottleneck:**
   - Profile with dotnet-trace
   - Use Application Performance Monitoring (APM) tools
   - Review slow query logs

2. **Measure Baseline:**
   - Document current performance
   - Establish success criteria

3. **Implement Optimization:**
   - Make targeted changes
   - Keep changes isolated

4. **Verify Improvement:**
   - Re-run benchmarks
   - Validate in staging environment
   - Check for regressions

5. **Monitor in Production:**
   - Gradual rollout
   - Real-world validation
   - Iterate if needed

## Resources

- [Kestrel Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)
- [Response Compression Middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression)
- [Memory Management and GC](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/)
- [.NET Performance Profiling Tools](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/)
