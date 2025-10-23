# Frontend Bundle Size Analysis

## Executive Summary

Comprehensive analysis of bundle sizes across three Oxpecker Solid.js example applications. All applications meet performance targets with efficient bundle sizes. Code splitting experiment on CRUD Frontend revealed important insights about when optimization provides real benefits.

**Key Finding:** Oxpecker + Solid.js + Fable produces highly efficient bundles with minimal framework overhead (3.12 KB baseline).

---

## Bundle Size Results

### 1. EmptySolid - Minimal Baseline

**Purpose:** Establish baseline overhead for Oxpecker.Solid stack

| Asset | Size | Gzipped | Notes |
|-------|------|---------|-------|
| index.html | 0.62 KB | 0.38 KB | |
| index.css | 0.36 KB | 0.22 KB | Minimal styles |
| index.js | 0.89 KB | 0.50 KB | App code |
| vendor-solid.js | 6.46 KB | 2.62 KB | Solid.js runtime |
| **Total JS** | **7.35 KB** | **3.12 KB** | ✅ Excellent |
| **Total Assets** | **8.33 KB** | **3.72 KB** | ✅ Excellent |

**Analysis:**
- Demonstrates minimal overhead of Fable + Solid.js stack
- 3.12 KB gzipped is exceptional for a reactive framework
- Excellent starting point for any application

---

### 2. TodoList - Medium Complexity with Routing

**Purpose:** Todo application with multiple routes and state management

| Asset | Size | Gzipped | Notes |
|-------|------|---------|-------|
| index.html | 0.66 KB | 0.41 KB | |
| index.css | 8.44 KB | 2.43 KB | Tailwind CSS |
| index.js | 5.55 KB | 2.57 KB | Main app code |
| About.js | 0.29 KB | 0.24 KB | 🎯 Code-split route |
| vendor-solid.js | 40.83 KB | 15.62 KB | Solid + Router |
| **Total JS** | **46.67 KB** | **18.43 KB** | ✅ Good |
| **Initial Load JS** | **46.38 KB** | **18.19 KB** | ✅ Excellent |
| **Total Assets** | **55.77 KB** | **21.27 KB** | ✅ Well under target |

**Analysis:**
- ✅ Effectively implements route-based code splitting
- About page (0.24 KB gzipped) is lazy-loaded separately
- Solid Router adds ~12.5 KB gzipped to vendor bundle
- Well under 50 KB gzipped target for initial load
- **Best practice example** for code splitting pattern

**Code Splitting Pattern:**
```fsharp
// In Program.fs
let LazyAbout() = lazy' (fun () -> importComponent "./About.jsx")

[<SolidComponent>]
let appRouter() =
    Router(root=Layout) {
        Route(path="/", component'=App)
        Route(path="/about", component'=LazyAbout)  // Lazy-loaded
    }
```

---

### 3. CRUD Frontend - High Complexity

**Purpose:** Full CRUD application with API integration and shared types

| Asset | Size | Gzipped | Notes |
|-------|------|---------|-------|
| index.html | 0.62 KB | 0.38 KB | |
| index.css | 9.01 KB | 2.57 KB | Tailwind CSS |
| index.js | 81.25 KB | 22.60 KB | Monolithic bundle |
| vendor-solid.js | 11.68 KB | 4.84 KB | Solid runtime |
| **Total JS** | **92.93 KB** | **27.44 KB** | ✅ Under target |
| **Total Assets** | **102.56 KB** | **30.39 KB** | ✅ Good |

**Analysis:**
- Single-page application (no routing)
- Largest bundle but still under 30 KB gzipped target
- Efficient compilation of shared types and API client code
- No immediate optimization needed for this use case

---

## Performance Targets Comparison

| Metric | EmptySolid | TodoList | CRUD | Target | Status |
|--------|-----------|----------|------|--------|--------|
| **Initial Bundle** | 3.12 KB | 18.19 KB | 27.44 KB | < 50 KB | ✅ All pass |
| **Lazy Chunks** | N/A | 0.24 KB | None | < 20 KB | ✅ Pass |
| **Total JS** | 3.12 KB | 18.43 KB | 27.44 KB | - | ✅ Excellent |

**All applications meet or exceed performance targets.**

---

## Code Splitting Experiment

### Hypothesis
Lazy-load CreateOrderButton component in CRUD Frontend to reduce initial bundle size.

### Implementation
```fsharp
// CreateOrderButton.fs - Added export
[<SolidComponent>]
[<ExportDefault>]
let CreateOrderButton() = ...

// Orders.fs - Added lazy loading
let LazyCreateOrderButton() = lazy' (fun () -> importComponent "./CreateOrderButton.jsx")

// Usage with Suspense
Suspense(fallback= div(class'="animate-pulse") { "Loading..." }) {
    LazyCreateOrderButton()
}
```

### Results

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| index.js (gzipped) | 22.60 KB | 22.36 KB | -0.24 KB ✅ |
| CreateOrderButton.js | - | 1.37 KB | +1.37 KB ❌ |
| vendor-solid.js (gzipped) | 4.84 KB | 5.98 KB | +1.14 KB ❌ |
| **Initial Load** | 27.44 KB | 28.34 KB | +0.90 KB ❌ |
| **Total JS** | 27.44 KB | 29.71 KB | +2.27 KB ❌ |

### Analysis: Why Did This Make Things Worse?

1. **Component too small:** 1.37 KB gzipped is below the threshold for beneficial splitting
2. **Infrastructure overhead:** Vite's code splitting machinery added 1.14 KB to vendor bundle
3. **Module wrapper cost:** Each chunk has import/export overhead
4. **High usage likelihood:** Most CRUD app users will create records

### Key Learning: When Code Splitting Helps

Code splitting provides real benefits when:

| Criterion | Guideline | TodoList About | CRUD CreateButton |
|-----------|-----------|----------------|-------------------|
| **Chunk size** | > 10-20 KB gzipped | ❌ 0.24 KB | ❌ 1.37 KB |
| **Usage frequency** | < 30% of users | ✅ Infrequent | ❌ High usage |
| **Type** | Route/feature-based | ✅ Separate page | ❌ Inline component |
| **Infrastructure cost** | Worth the overhead | ✅ Yes | ❌ No |

**TodoList's About page** is ideal for code splitting:
- Separate route (natural boundary)
- Infrequent access
- Minimal overhead for router-based splitting

**CRUD CreateOrderButton** is not ideal:
- Inline component (requires Suspense infrastructure)
- High usage (core CRUD functionality)
- Too small to justify overhead

---

## Framework Efficiency Analysis

### Fable Compilation Quality

**EmptySolid demonstrates exceptional efficiency:**
- Application code: 0.50 KB gzipped
- Framework runtime: 2.62 KB gzipped
- **Total: 3.12 KB gzipped**

This is competitive with hand-written JavaScript frameworks and demonstrates that Fable produces highly optimized output.

### Vendor Bundle Analysis

| Example | Vendor Bundle (gzipped) | Contents |
|---------|------------------------|----------|
| EmptySolid | 2.62 KB | Solid.js core |
| TodoList | 15.62 KB | Solid.js + Router |
| CRUD | 4.84 KB | Solid.js + Suspense primitives |

**Observations:**
- Solid Router adds ~13 KB gzipped
- Suspense/lazy infrastructure adds ~2 KB gzipped
- Base Solid.js runtime is incredibly lean (2.6 KB)

---

## Recommendations

### ✅ Current State is Excellent

All three applications are well-optimized:
- EmptySolid: Perfect minimal baseline
- TodoList: Best practice example with route splitting
- CRUD Frontend: Appropriately sized for its functionality

### 📋 Best Practices for Future Development

#### When to Use Code Splitting

**✅ DO split:**
- Entire route pages (like TodoList's About)
- Admin/settings panels
- Large third-party libraries (charts, editors)
- Feature-flagged functionality
- Components > 20 KB gzipped

**❌ DON'T split:**
- Core functionality (like CRUD forms)
- Components < 10 KB gzipped
- Highly-used features
- Inline UI components

#### Route-Based Splitting Pattern (Recommended)

```fsharp
// Best practice: Split entire routes
let LazyAdminPanel() = lazy' (fun () -> importComponent "./AdminPanel.jsx")
let LazySettings() = lazy' (fun () -> importComponent "./Settings.jsx")

Router(root=Layout) {
    Route(path="/", component'=Home)
    Route(path="/admin", component'=LazyAdminPanel)  // Lazy route
    Route(path="/settings", component'=LazySettings) // Lazy route
}
```

### 🔍 Future Monitoring

Consider adding to CI:
```bash
# Bundle size tracking
npm run build
npx vite-bundle-visualizer
```

Set alerts for bundle size increases > 10% to catch regressions.

---

## Measurement Methodology

### Commands Used

```bash
# For each example application
cd examples/<app-name>
npm install
npm run build

# Analyze output
ls -lh dist/assets/
```

### Build Configuration

- **Fable:** 4.24.0 with `--extension .jsx`
- **Vite:** 6.2.2 - 6.4.1 (varies by example)
- **Mode:** Production with minification
- **Compression:** Gzip (default web standard)

### Reproducibility

All measurements were taken from production builds on:
- Platform: linux
- Node.js: (from npm environment)
- Date: 2025-10-23

To reproduce:
```bash
git checkout perf/frontend-bundle-analysis
cd examples/<app-name>
npm install && npm run build
```

---

## Conclusion

**Oxpecker.Solid produces exceptionally efficient bundles.** With a 3.12 KB baseline overhead and intelligent framework design, all example applications deliver excellent performance.

**Key Takeaway:** Focus optimization efforts on route-level splitting for pages > 20 KB. Component-level splitting for small inline components adds overhead without meaningful benefit.

**Best Practice Reference:** Use TodoList's About page pattern as the template for future code splitting needs.

---

## Appendix: Raw Build Logs

Build logs saved to:
- `/tmp/gh-aw/agent/emptysolid-build.log`
- `/tmp/gh-aw/agent/todolist-build.log`
- `/tmp/gh-aw/agent/crud-frontend-build.log`
- `/tmp/gh-aw/agent/crud-frontend-optimized-build.log` (experiment)
