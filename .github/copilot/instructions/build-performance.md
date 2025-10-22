# Build and CI Performance Optimization Guide

## Overview
This guide focuses on optimizing build times, test execution, and CI pipeline performance to enable rapid performance engineering iteration.

## Key Performance Areas

### 1. .NET Build Performance

**Common Bottlenecks:**
- Full rebuilds instead of incremental builds
- Unnecessary project dependencies
- Large solution files
- Slow package restore

**Measurement Strategies:**
- Use MSBuild binary logs for detailed analysis
- Measure clean vs. incremental build times
- Profile with dotnet build-server shutdown between runs

**Quick Measurement Commands:**
```bash
# Clean build with timing
time dotnet clean && time dotnet build

# Incremental build timing
time dotnet build --no-restore

# Generate detailed build log
dotnet build /bl:build.binlog
# Analyze with: https://msbuildlog.com/

# Check build cache effectiveness
dotnet build --no-incremental
```

**Optimization Techniques:**
- Use `--no-restore` when packages haven't changed
- Split large solutions into smaller ones when appropriate
- **Use explicit `/m` flag for parallel builds** ⭐ (9.4% faster than implicit default)
- Use local NuGet cache effectively
- Minimize project cross-references

**Recommended build command:**
```bash
dotnet build Oxpecker.sln --no-restore /m
```

**Performance comparison (Oxpecker.sln):**
- Single-threaded (`/m:1`): 47.95s
- Default (implicit parallel): 45.93s
- Explicit parallel (`/m`): 43.41s ✅ **FASTEST** (9.4% improvement)

### 2. Test Performance

**Common Bottlenecks:**
- Slow integration tests without parallelization
- Unnecessary test fixtures setup
- Database/network calls in unit tests
- Running full suite when subset would suffice

**Measurement Strategies:**
- Use `dotnet test` with `--logger "console;verbosity=detailed"`
- Profile individual test methods
- Measure test suite time with different filters

**Quick Commands:**
```bash
# Run tests with timing
time dotnet test --no-build

# Run specific test category
dotnet test --filter Category=Unit

# Parallel test execution
dotnet test --parallel

# Verbose test output with timing
dotnet test --logger "console;verbosity=detailed"
```

**Optimization Techniques:**
- Run unit tests in parallel (xUnit does this by default)
- Use test categories to run subset of tests
- Mock external dependencies properly
- Use shared test contexts efficiently
- Consider test data builders for faster setup

### 3. Fable Compilation Performance

**Common Bottlenecks:**
- Compiling entire project when only small changes made
- No caching between builds
- Large F# projects

**Measurement Strategies:**
- Time Fable compilation separately from Vite build
- Compare cache vs. no-cache compilation times
- Monitor file watch responsiveness

**Quick Commands:**
```bash
# Time Fable compilation
time dotnet fable --exclude Oxpecker.Solid.FablePlugin

# With cache (default)
time dotnet fable

# Without cache for clean measurement
time dotnet fable --noCache

# Watch mode for iteration
dotnet fable watch --run vite
```

**Optimization Techniques:**
- Use Fable caching (avoid --noCache in development)
- Exclude unnecessary projects with --exclude
- Keep F# projects modular
- Use watch mode for rapid iteration

### 4. CI Pipeline Performance

**Common Bottlenecks:**
- No caching of dependencies
- Sequential jobs that could be parallel
- Redundant builds across jobs
- Large artifacts

**Measurement Strategies:**
- Review GitHub Actions workflow timing
- Compare with/without caching
- Analyze job dependencies

**Optimization Techniques:**
- Cache NuGet packages between runs
- Cache node_modules for frontend builds
- Run independent jobs in parallel
- Use matrix builds for multiple configurations
- Minimize artifact sizes
- Skip redundant checks on docs-only changes

**Example Cache Configuration:**
```yaml
- uses: actions/cache@v3
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.fsproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```

## Focused Measurement Workflow

### Quick Build Check (< 2 minutes)
```bash
# Incremental build from clean state
dotnet build --no-restore
dotnet test --no-build --filter Category=Unit
```

### Detailed Build Analysis (10-15 minutes)
```bash
# Clean build with detailed logging
dotnet clean
dotnet build /bl:build.binlog
# Analyze build.binlog with MSBuild Structured Log Viewer

# Full test suite with timing
dotnet test --logger "console;verbosity=detailed"

# Fable compilation analysis
time dotnet fable --noCache
time dotnet fable  # With cache
```

### CI Optimization Testing (30+ minutes)
1. Create test branch with CI changes
2. Run workflow and measure total time
3. Compare against baseline
4. Identify slowest jobs/steps
5. Iterate on improvements

## Success Metrics

- **Clean Build Time:** < 2 minutes for Oxpecker.sln
- **Incremental Build Time:** < 30 seconds
- **Unit Test Execution:** < 30 seconds
- **Full Test Suite:** < 2 minutes
- **Fable Compilation (cached):** < 10 seconds
- **CI Pipeline Total Time:** < 5 minutes

## Common Trade-offs

- **Parallelization vs. Resource Usage:** Parallel builds use more memory/CPU
- **Caching vs. Correctness:** Aggressive caching can hide build issues
- **Test Coverage vs. Speed:** More tests = slower feedback
- **Build Isolation vs. Speed:** Isolated builds are slower but more reliable

## Developer Experience Impact

Fast builds enable:
- Rapid performance experiment iteration
- Quick validation of optimizations
- Efficient TDD workflows
- Faster CI feedback loops
- More frequent performance testing

## Example Build Performance Test Script

```bash
#!/bin/bash
# build-perf-test.sh

echo "=== Clean Build Test ==="
dotnet clean > /dev/null 2>&1
time dotnet build

echo ""
echo "=== Incremental Build Test ==="
touch src/Oxpecker/Oxpecker.fsproj
time dotnet build

echo ""
echo "=== Test Performance ==="
time dotnet test --no-build

echo ""
echo "=== Fable Compilation Test ==="
cd examples/TodoList
time dotnet fable --noCache > /dev/null 2>&1
time dotnet fable > /dev/null 2>&1
```

## Resources

- [MSBuild Performance](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-performance)
- [MSBuild Structured Log Viewer](https://msbuildlog.com/)
- [GitHub Actions Cache](https://docs.github.com/en/actions/using-workflows/caching-dependencies)
- [xUnit Performance](https://xunit.net/docs/running-tests-in-parallel)
