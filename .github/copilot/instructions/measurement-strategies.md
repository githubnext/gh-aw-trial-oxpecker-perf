# Performance Measurement Strategies

## Overview
This guide provides a framework for choosing appropriate performance measurement approaches based on your optimization target and time constraints.

## Measurement Philosophy

**Key Principle:** Choose the fastest measurement that provides reliable signal for your specific optimization target.

- **Synthetic benchmarks:** Fast, isolated, repeatable - ideal for algorithm optimization
- **Integration tests:** Medium speed, realistic interactions - good for feature performance
- **Load tests:** Slower, realistic load patterns - necessary for scalability validation
- **Production monitoring:** Continuous, real-world data - validates actual user impact

## Decision Tree: Which Measurement Approach?

### Are you optimizing...

#### 1. Algorithm or Data Structure Performance?
**Use:** Micro-benchmarks with BenchmarkDotNet

**Rationale:** Need isolated, precise measurements of CPU/memory characteristics

**Time:** 5-10 minutes per benchmark

**Example:**
```fsharp
[<MemoryDiagnoser>]
type CollectionBenchmark() =
    [<Benchmark>]
    member _.ListAppend() = [1..1000] |> List.append [1001..2000]

    [<Benchmark>]
    member _.ArrayConcat() = Array.append [|1..1000|] [|1001..2000|]
```

#### 2. HTTP Endpoint Performance?
**Use:** Load testing with bombardier/wrk + dotnet-counters

**Rationale:** Need realistic request/response cycle with concurrency

**Time:** 10-15 minutes per test

**Example:**
```bash
# Terminal 1: Monitor metrics
dotnet-counters monitor --process-id $(pgrep dotnet)

# Terminal 2: Load test
bombardier -c 125 -d 30s http://localhost:5000/api/users
```

#### 3. ViewEngine/HTML Rendering?
**Use:** Custom benchmarks + memory profiling

**Rationale:** Need to measure both speed and allocation patterns

**Time:** 10-20 minutes per test

**Example:**
```fsharp
[<MemoryDiagnoser>]
type ViewBenchmark() =
    let largeList = [1..1000]

    [<Benchmark>]
    member _.RenderTable() =
        html() {
            table() {
                for item in largeList do
                    tr() { td() { item } }
            }
        }
```

#### 4. Frontend User Experience?
**Use:** Lighthouse + Chrome DevTools Performance

**Rationale:** Need Core Web Vitals and user-centric metrics

**Time:** 15-30 minutes per scenario

**Example:**
```bash
# Quick audit
lighthouse http://localhost:3000 --only-categories=performance

# Detailed with tracing
lighthouse http://localhost:3000 --view --throttling.cpuSlowdownMultiplier=4
```

#### 5. Build/CI Performance?
**Use:** Time command + MSBuild binary logs

**Rationale:** Need to identify slow steps in build pipeline

**Time:** 5-10 minutes per measurement

**Example:**
```bash
# Measure clean build
time dotnet build /bl:clean-build.binlog

# Measure incremental build
touch src/Oxpecker/HttpHandler.fs
time dotnet build /bl:incremental-build.binlog
```

#### 6. Database Query Performance?
**Use:** SQL profiling + EF Core logging

**Rationale:** Need to identify N+1 queries and slow operations

**Time:** 10-20 minutes per scenario

**Example:**
```fsharp
// Enable detailed logging
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString)
           .EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine, LogLevel.Information))
```

## Measurement Workflow by Time Budget

### Ultra-Fast Check (< 5 minutes)
**Goal:** Quick smoke test for obvious regressions

**Approach:**
1. Run existing unit tests: `dotnet test --no-build`
2. Quick manual test of key user flow
3. Check for console errors

**When to use:** Every commit during active development

### Fast Validation (10-15 minutes)
**Goal:** Reliable signal on specific optimization

**Approach:**
1. Run targeted benchmark 3-5 times
2. Calculate mean and standard deviation
3. Compare against baseline (aim for >10% improvement)

**When to use:** Validating individual optimization attempts

### Thorough Analysis (30-60 minutes)
**Goal:** Understand performance characteristics in depth

**Approach:**
1. Run multiple benchmark scenarios (small/medium/large data)
2. Profile with dotnet-trace for hot paths
3. Analyze memory allocations
4. Load test with realistic concurrency

**When to use:** Major performance work, investigating bottlenecks

### Production Validation (2+ hours)
**Goal:** Confirm real-world impact

**Approach:**
1. Deploy to staging environment
2. Run synthetic load tests
3. Conduct realistic user scenario testing
4. Monitor resource utilization trends
5. Compare against production baseline metrics

**When to use:** Before merging significant performance changes

## Maximizing Measurement Efficiency

### 1. Leverage Incremental Builds
```bash
# Don't do this:
dotnet clean && dotnet build && dotnet test

# Do this instead:
dotnet build  # Incremental
dotnet test --no-build
```

### 2. Use Test Filters
```bash
# Don't run full suite for quick checks:
dotnet test --filter Category=Performance --no-build
```

### 3. Cache Dependencies
```bash
# First run installs tools
dotnet tool restore

# Subsequent runs are instant
dotnet tool list
```

### 4. Focus Measurements
Don't measure everything - focus on:
- The specific code path you changed
- The performance dimension you care about (CPU/memory/latency)
- Realistic data sizes

### 5. Establish Baselines Early
```bash
# Before optimization:
bombardier -c 125 -d 10s http://localhost:5000 > baseline.txt

# After optimization:
bombardier -c 125 -d 10s http://localhost:5000 > optimized.txt

# Compare:
diff baseline.txt optimized.txt
```

## Statistical Significance

### Minimum Improvement Thresholds
- **Micro-benchmarks:** >5% improvement (noise is low)
- **Integration tests:** >10% improvement (more variance)
- **Load tests:** >15% improvement (network/system variance)
- **User metrics:** >20% improvement (many confounding factors)

### Repetition Guidelines
- **Stable environment (local machine):** 3-5 runs
- **CI environment:** 5-10 runs
- **Production:** Continuous monitoring over days/weeks

### Dealing with Variance
```bash
# Run benchmark multiple times and calculate statistics
for i in {1..5}; do
    bombardier -c 125 -d 10s http://localhost:5000 | grep "Reqs/sec"
done
```

## Red Flags: Bad Measurements

❌ **Single run:** Variance makes results unreliable
❌ **Debug mode:** Not representative of production
❌ **Warm-up ignored:** JIT compilation affects results
❌ **Wrong scope:** Measuring entire system for localized change
❌ **Synthetic data only:** Real data may have different characteristics

## Common Pitfalls

### 1. Measuring in Debug Mode
```bash
# Wrong:
dotnet run

# Right:
dotnet run -c Release
```

### 2. Not Warming Up
```fsharp
// Wrong:
[<Benchmark>]
member _.Test() = expensiveOperation()

// Right:
[<GlobalSetup>]
member _.Setup() =
    // Warm up JIT
    for _ in 1..100 do expensiveOperation() |> ignore

[<Benchmark>]
member _.Test() = expensiveOperation()
```

### 3. Measuring Too Much
Focus on changed code, not entire system.

### 4. Ignoring System State
- Close other applications
- Disable Windows Defender/antivirus scanning
- Check CPU throttling settings
- Monitor background processes

## Tools Quick Reference

| Tool | Use Case | Install | Time |
|------|----------|---------|------|
| BenchmarkDotNet | Micro-benchmarks | `dotnet add package` | Fast |
| dotnet-trace | CPU profiling | `dotnet tool install -g` | Medium |
| dotnet-counters | Live metrics | `dotnet tool install -g` | Fast |
| bombardier | Load testing | `go install` or binary | Medium |
| Lighthouse | Frontend metrics | `npm install -g` | Medium |
| MSBuild Log Viewer | Build analysis | Download binary | Fast |

## Resources

- [BenchmarkDotNet Best Practices](https://benchmarkdotnet.org/articles/guides/good-practices.html)
- [.NET Performance Diagnostics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/performance)
- [Effective Load Testing](https://github.com/wg/wrk/wiki)
- [Web Performance Metrics](https://web.dev/metrics/)
