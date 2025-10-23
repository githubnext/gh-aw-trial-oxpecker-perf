# Test Performance Guide

## Overview
This guide provides strategies for optimizing test execution performance in Oxpecker, including measurement techniques, optimization approaches, and best practices for maintaining fast test feedback loops.

## Current Performance Baseline

### Test Suite Metrics (as of 2025-10-23)
```
Total Tests: 161 (145 Oxpecker.Tests + 16 ViewEngine.Tests)
Total Runtime: 3.6 seconds
Average per Test: 22ms
Target: <30s (From performance plan)
Status: ✅ EXCELLENT - Already 8x faster than target
```

### Test Assemblies
1. **Oxpecker.Tests**: 145 tests, ~2.3s runtime
2. **Oxpecker.ViewEngine.Tests**: 16 tests, ~1.7s runtime

Both assemblies run in parallel during `dotnet test`, maximizing CPU utilization.

## Performance Characteristics

### Slowest Tests (Top 10)
```
226ms - Oxpecker.Tests.Streaming: HTTP GET middle part with range disabled
226ms - Oxpecker.Tests.Preconditional: If-Modified-Since with greater lastModified
173ms - Oxpecker.Tests.Routing: configureEndpoint inside subRoute
118ms - Oxpecker.Tests.ModelParser: Union case validation failure
68ms  - Oxpecker.Tests.Json: Non-chunked serializer
63ms  - Oxpecker.Tests.ModelValidation: Invalid model validation
54ms  - ViewEngine.Tests.Tools: Custom queue operations
29ms  - Oxpecker.Tests.ModelValidation: Empty model defaults
26ms  - Oxpecker.Tests.Streaming: Range processing enabled
24ms  - Oxpecker.Tests.Helpers: Composition operations
```

### Test Categories by Performance
- **Fast (<10ms)**: 80% of tests - Unit tests, pure functions
- **Medium (10-50ms)**: 15% of tests - Model parsing, JSON ops
- **Slow (>50ms)**: 5% of tests - Integration tests with TestServer

## Why Tests Are Already Fast

### 1. Parallel Test Assembly Execution
`dotnet test` runs multiple test assemblies concurrently:
- Oxpecker.Tests and ViewEngine.Tests run simultaneously
- Maximizes CPU utilization on multi-core systems
- No configuration needed - works out of the box

### 2. Fast Test Infrastructure
- **TestServer**: In-memory ASP.NET Core server (no network)
- **No database**: All tests use in-memory data structures
- **Minimal setup/teardown**: Tests are mostly stateless
- **Efficient F# code**: Fast compilation and execution

### 3. Small Test Scope
- Most tests are focused unit tests
- Integration tests are targeted, not end-to-end
- No external dependencies (no Docker, databases, etc.)

## Optimization Opportunities

### When Test Suite Grows Beyond 500 Tests

#### 1. Enable Parallel Test Execution Within Assemblies
Currently, tests within each assembly run sequentially. For larger suites, enable parallelism:

**Add `xunit.runner.json` to test project:**
```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 0
}
```

**Update `.fsproj` to include config:**
```xml
<ItemGroup>
  <Content Include="xunit.runner.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

**⚠️ Warning:** Only enable if tests are thread-safe:
- No shared mutable state between tests
- Each test creates its own TestServer instance
- No file system conflicts
- No port conflicts

Current Oxpecker tests appear safe for parallelization, but validate first!

#### 2. Test Categorization with Traits
Group tests by speed to enable selective execution:

```fsharp
[<Fact>]
[<Trait("Category", "Fast")>]
[<Trait("Category", "Unit")>]
let ``Fast unit test`` () =
    // Test implementation

[<Fact>]
[<Trait("Category", "Slow")>]
[<Trait("Category", "Integration")>]
let ``Integration test with TestServer`` () =
    // Test implementation
```

**Run specific categories:**
```bash
# Fast feedback loop during development
dotnet test --filter "Category=Fast" --no-build

# Full suite in CI
dotnet test --no-build
```

#### 3. Optimize Slow Integration Tests

For the 226ms streaming/preconditional tests:

**Option A: Reduce test file sizes**
```fsharp
// Instead of large file
let testFile = "streaming.txt" // 62 bytes

// Consider smaller files for basic tests
let smallTestFile = "0123456789" // 10 bytes
```

**Option B: Shared TestServer instances**
```fsharp
// Current: Each test creates new server (226ms)
[<Fact>]
let ``Test 1`` () =
    use server = new TestServer(webHostBuilder)
    // Test logic

// Optimized: Shared server across test class
type StreamingTests() =
    let server = new TestServer(webHostBuilder)

    interface IDisposable with
        member _.Dispose() = server.Dispose()

    [<Fact>]
    member _.``Test 1``() =
        // Test logic using shared server
```

**Trade-off:** Shared servers are faster but tests are no longer isolated. Only use for read-only tests.

## Measurement Strategies

### Quick Smoke Test (<5s)
```bash
# Run tests without rebuild (fastest feedback)
dotnet test --no-build
```

### Category-Specific Testing (10-20s)
```bash
# Only fast unit tests during active development
dotnet test --filter "Category=Fast" --no-build

# Only integration tests before committing
dotnet test --filter "Category=Integration" --no-build
```

### Full Test Suite with Profiling (30-60s)
```bash
# Detailed timing with verbosity
time dotnet test --no-restore --no-build --logger "console;verbosity=detailed"

# Identify slowest tests
dotnet test --logger "console;verbosity=detailed" 2>&1 | \
    grep "Passed.*\\[.*ms\\]" | \
    awk -F'[\\[\\]]' '{print $2 "\t" $1}' | \
    sort -rn | head -20
```

### CI Performance Monitoring
Track test execution time trends in CI:
```bash
# In CI pipeline, log test duration
START_TIME=$(date +%s)
dotnet test --no-restore --no-build
END_TIME=$(date +%s)
TEST_DURATION=$((END_TIME - START_TIME))
echo "Test suite completed in ${TEST_DURATION}s"

# Alert if tests slow down significantly
if [ $TEST_DURATION -gt 30 ]; then
    echo "⚠️ Warning: Tests took longer than 30s"
fi
```

## Best Practices

### 1. Keep Tests Fast by Design
- ✅ Use in-memory data structures
- ✅ Mock external dependencies
- ✅ Minimize I/O operations
- ✅ Avoid Thread.Sleep() - use deterministic waiting
- ✅ Use TestServer instead of real HTTP listeners

### 2. Optimize Test Setup/Teardown
```fsharp
// ❌ Slow: Create server in every test
[<Fact>]
let test1 () =
    use server = createServer()
    // test logic

[<Fact>]
let test2 () =
    use server = createServer()
    // test logic

// ✅ Fast: Shared fixture for related tests
type ServerFixture() =
    let server = createServer()
    member _.Server = server
    interface IDisposable with
        member _.Dispose() = server.Dispose()

type MyTests(fixture: ServerFixture) =
    [<Fact>]
    member _.test1 () =
        let server = fixture.Server
        // test logic
```

### 3. Prioritize Test Execution Order
```bash
# Run fast tests first for quicker feedback
dotnet test --filter "Category=Fast"

# If fast tests pass, run integration tests
dotnet test --filter "Category=Integration"
```

### 4. Use `--no-build` Aggressively
```bash
# Don't do this during development:
dotnet build && dotnet test

# Do this instead:
dotnet build  # Once
dotnet test --no-build  # Many times during development
dotnet test --no-build --filter "Category=Fast"  # Even faster
```

## Advanced Optimization Techniques

### 1. Test Result Caching
For expensive test operations, consider caching results:
```fsharp
module TestCache =
    let mutable private expensiveSetupResult = None

    let getOrCreateExpensiveSetup() =
        match expensiveSetupResult with
        | Some result -> result
        | None ->
            let result = performExpensiveSetup()
            expensiveSetupResult <- Some result
            result
```

### 2. Lazy Test Data Generation
```fsharp
// ❌ Generate data upfront for all tests
let testData = generateLargeDataSet()

[<Theory>]
[<InlineData(0)>]
let test index =
    let data = testData[index]
    // test logic

// ✅ Generate data only when needed
[<Theory>]
[<InlineData(0)>]
let test index =
    let data = generateTestData(index)  // Lazy generation
    // test logic
```

### 3. Test Parallelization Groups
For tests that share resources, use collection fixtures:
```fsharp
[<Collection("Database")>]
type DatabaseTests() =
    // These tests won't run in parallel with each other
    // but will run in parallel with other test collections
```

## Common Anti-Patterns

### ❌ Don't: Run Full Rebuild Every Test
```bash
# This wastes time
dotnet clean && dotnet build && dotnet test
```

### ❌ Don't: Use Arbitrary Delays
```fsharp
// Slow and flaky
do! Task.Delay(1000)  // Hope operation completes
```

### ✅ Do: Use Deterministic Waiting
```fsharp
// Fast and reliable
let! result = operation() |> Async.AwaitTask
```

### ❌ Don't: Test Everything in Integration Tests
```fsharp
// Slow integration test for simple logic
[<Fact>]
let ``Adding two numbers works`` () =
    use server = new TestServer(webHostBuilder)
    let client = server.CreateClient()
    let! response = client.GetStringAsync("/api/add?a=2&b=3")
    response |> should equal "5"
```

### ✅ Do: Unit Test Pure Logic, Integration Test Integration
```fsharp
// Fast unit test
[<Fact>]
let ``Adding two numbers works`` () =
    add 2 3 |> should equal 5

// Integration test only for HTTP concerns
[<Fact>]
let ``API endpoint returns correct content type`` () =
    use server = new TestServer(webHostBuilder)
    // Test HTTP-specific concerns
```

## Benchmarking Test Performance

### Establish Baseline
```bash
# Run 5 times and record results
for i in {1..5}; do
    echo "Run $i:"
    time dotnet test --no-build 2>&1 | grep "Total time"
done
```

### Track Performance Over Time
```bash
# In CI, store metrics
TEST_TIME=$(dotnet test --no-build 2>&1 | grep "Total time" | awk '{print $3}')
echo "test_duration_seconds=$TEST_TIME" >> metrics.log
```

### Identify Regressions
```bash
# Compare current vs baseline
BASELINE=3.6  # seconds
CURRENT=$(dotnet test --no-build 2>&1 | grep -oP 'Total time: \K[\d.]+')

if (( $(echo "$CURRENT > $BASELINE * 1.2" | bc -l) )); then
    echo "⚠️ Test performance degraded: ${CURRENT}s (was ${BASELINE}s)"
fi
```

## CI Integration

### Optimal CI Test Strategy
```yaml
# .github/workflows/CI.yml
- name: Run tests with timing
  run: |
    time dotnet test --no-build --logger "console;verbosity=normal"

- name: Check test performance
  run: |
    TEST_TIME=$(grep "Total time" test.log | awk '{sum += $3} END {print sum}')
    if (( $(echo "$TEST_TIME > 30" | bc -l) )); then
      echo "::warning::Tests took ${TEST_TIME}s (target: <30s)"
    fi
```

### Parallel CI Jobs
For very large test suites, split across CI jobs:
```yaml
strategy:
  matrix:
    test-category: [Fast, Integration, E2E]
steps:
  - name: Run ${{ matrix.test-category }} tests
    run: dotnet test --filter "Category=${{ matrix.test-category }}"
```

## Success Metrics

### Current Achievement
- ✅ Total test time: 3.6s (8x faster than target)
- ✅ Average per test: 22ms
- ✅ Parallel assembly execution: Enabled
- ✅ Fast feedback loop: <5s with `--no-build`

### Future Targets (if suite grows 10x to 1600 tests)
- 🎯 Keep total time <30s
- 🎯 Enable intra-assembly parallelization
- 🎯 Categorize tests by speed
- 🎯 Maintain <50ms average per test

## Resources

- [xUnit Parallelization](https://xunit.net/docs/running-tests-in-parallel)
- [.NET Test Performance](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#test-performance)
- [TestServer Documentation](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

## Conclusion

Oxpecker's test suite is already exceptionally fast. The current 3.6s execution time provides excellent developer feedback loops. Future optimization should only be considered if:
1. Test count grows significantly (>500 tests)
2. CI feedback time becomes a bottleneck (>5 minutes)
3. Individual slow tests are identified (>1 second per test)

For now, maintaining the current excellent performance is more important than premature optimization.
