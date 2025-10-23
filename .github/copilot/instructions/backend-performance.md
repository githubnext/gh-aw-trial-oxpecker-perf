# Backend Performance Optimization Guide

## Overview
This guide covers performance optimization for Oxpecker's ASP.NET Core backend, focusing on endpoint handlers, middleware, and server-side rendering.

## Key Performance Areas

### 1. Endpoint Handler Performance

**Common Bottlenecks:**
- Synchronous I/O operations blocking request threads
- Inefficient database queries and N+1 problems
- Large object allocations in hot paths
- Unnecessary serialization/deserialization

**Measurement Strategies:**
- Use BenchmarkDotNet for micro-benchmarks of handlers
- Profile with dotnet-trace for production workload analysis
- Monitor allocation rates with dotnet-counters
- Load test with tools like wrk, bombardier, or k6

**Quick Measurement Commands:**
```bash
# Install BenchmarkDotNet
dotnet add package BenchmarkDotNet

# Profile allocations
dotnet-counters monitor --process-id <pid> --counters System.Runtime

# CPU profiling
dotnet-trace collect --process-id <pid> --profile cpu-sampling

# Load testing with bombardier
bombardier -c 125 -n 10000 http://localhost:5000/api/endpoint
```

**Optimization Techniques:**
- Use ValueTask for async operations that often complete synchronously
- Implement response caching for expensive operations
- Use HttpContext.Response.BodyWriter for efficient streaming
- Minimize allocations in hot paths (use ArrayPool, ValueTask, etc.)
- Leverage compiled expressions for repeated operations

### 2. ViewEngine Performance

**Oxpecker ViewEngine is highly optimized out-of-the-box:**
- Current benchmarks: **653.5 ns** rendering time, **928 B** allocation
- 77% faster than Giraffe (1,153 ns, 11,000 B)
- 118% faster than Falco (1,422 ns, 2,432 B)
- 91% less memory than Giraffe, 62% less than Falco

**Optimization Techniques Already Implemented:**
- StringBuilder pooling (Microsoft.Extensions.ObjectPool)
- CustomQueue linked list for zero-copy element traversal
- SIMD-optimized HTML encoding with SearchValues
- Span<char> for zero-allocation string slicing
- Aggressive inlining of builder methods
- ArrayPool for UTF8 conversion

**When to Optimize Further:**
- Profile first - current performance is excellent for most use cases
- For very large documents (>100KB HTML), consider streaming with `Render.toStreamAsync`
- For repeated renders of same content, implement fragment caching at application level
- For high-throughput scenarios, measure actual bottlenecks before optimizing

**Measurement Example:**
```bash
# Run ViewEngine benchmarks
cd tests/PerfTest
dotnet run -c Release -- --filter "*ViewEngine*"
```

**When NOT to Optimize:**
- Don't optimize ViewEngine internals without profiling
- Element construction allocations (classes, CustomQueueItem) are intentional design trade-offs
- Further allocation reduction would require significant complexity with minimal gains

### 3. Middleware Performance

**Common Bottlenecks:**
- Middleware chain too deep
- Synchronous operations in async pipeline
- Excessive logging in hot paths

**Measurement Strategies:**
- Profile middleware execution order and timing
- Use Application Insights or similar APM tools
- Benchmark middleware chains in isolation

**Optimization Techniques:**
- Order middleware by frequency of short-circuiting
- Avoid expensive operations before authorization checks
- Use conditional middleware registration
- Implement efficient logging with LoggerMessage

### 4. JSON Serialization

**Common Bottlenecks:**
- Large object graphs
- Reflection-based serialization
- Unnecessary property serialization

**Measurement Strategies:**
- Benchmark serialization with realistic data
- Profile with dotnet-trace focusing on serialization methods

**Optimization Techniques:**
- Use System.Text.Json with source generators
- Implement custom converters for hot types
- Use JsonIgnore for unnecessary properties
- Consider MessagePack for high-throughput scenarios

## Focused Measurement Workflow

### Quick Performance Check (< 5 minutes)
1. Run existing test suite with `dotnet test --no-build`
2. Run quick benchmark: `dotnet run -c Release -- benchmark`
3. Check for obvious regressions in key metrics

### Detailed Performance Analysis (15-30 minutes)
1. Set up BenchmarkDotNet for specific endpoint/handler
2. Run with different data sizes and scenarios
3. Analyze allocation and CPU profiles
4. Compare against baseline measurements

### Production-Ready Testing (1+ hour)
1. Deploy to staging environment
2. Run load tests simulating realistic traffic
3. Monitor resource utilization (CPU, memory, network)
4. Analyze distributed traces for bottlenecks

## Success Metrics

- **Throughput:** Requests per second for key endpoints
- **Latency:** P50, P95, P99 response times
- **Allocations:** Bytes allocated per request
- **CPU Usage:** Percentage under typical load
- **Memory:** Working set and GC pressure

## Common Trade-offs

- **Caching vs. Memory:** Aggressive caching improves speed but increases memory usage
- **Async vs. Sync:** Async improves scalability but adds overhead for CPU-bound work
- **Allocation vs. Computation:** Object pooling reduces GC pressure but adds complexity
- **Readability vs. Performance:** Micro-optimizations can harm maintainability

## Example Benchmark Template

```fsharp
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

[<MemoryDiagnoser>]
type EndpointBenchmark() =

    [<Benchmark>]
    member _.TestEndpoint() =
        // Your endpoint logic here
        ()

[<EntryPoint>]
let main args =
    BenchmarkRunner.Run<EndpointBenchmark>() |> ignore
    0
```

## Resources

- [.NET Performance Documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/performance)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [TechEmpower Benchmarks](https://www.techempower.com/benchmarks/) - Oxpecker's reference
