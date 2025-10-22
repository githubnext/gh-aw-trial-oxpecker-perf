# Frontend Performance Optimization Guide

## Overview
This guide covers performance optimization for Oxpecker's frontend applications, particularly those using Solid.js and the ViewEngine for HTMX applications.

## Key Performance Areas

### 1. Solid.js Application Performance

**Common Bottlenecks:**
- Unnecessary re-renders and reactivity triggers
- Large component trees without proper splitting
- Inefficient list rendering
- Bundle size issues

**Measurement Strategies:**
- Use Chrome DevTools Performance profiler
- Lighthouse for Core Web Vitals
- Bundle analyzer for size analysis
- React DevTools Profiler (compatible with Solid)

**Quick Measurement Commands:**
```bash
# Build and analyze bundle size
npm run build
npx vite-bundle-visualizer

# Lighthouse CI
npm install -g @lhci/cli
lhci autorun --collect.url=http://localhost:3000

# Performance profiling in browser
# Open DevTools > Performance > Record
```

**Optimization Techniques:**
- Use `createMemo` to cache expensive computations
- Implement proper list keying with `<For>` component
- Use `lazy` for code splitting large components
- Minimize reactivity scope with `untrack` and `batch`
- Use `createResource` for async data fetching

### 2. HTMX Application Performance

**Common Bottlenecks:**
- Large HTML responses from server
- Frequent full-page swaps instead of targeted updates
- Synchronous requests blocking UI
- Missing cache headers

**Measurement Strategies:**
- Monitor network tab for request/response sizes
- Use Chrome DevTools Performance for interaction latency
- Measure Time to Interactive (TTI)
- Profile server-side render time

**Optimization Techniques:**
- Use targeted swaps (hx-target, hx-swap)
- Implement proper caching with ETag headers
- Use hx-boost for progressive enhancement
- Minimize HTML payload size
- Implement request debouncing/throttling

### 3. Asset Optimization

**Common Bottlenecks:**
- Unoptimized images
- Large JavaScript bundles
- Missing compression
- No CDN caching

**Measurement Strategies:**
- Bundle size analysis with vite-bundle-visualizer
- Network waterfall analysis in DevTools
- WebPageTest for real-world performance
- Lighthouse audit for asset optimization

**Optimization Techniques:**
- Implement image lazy loading
- Use modern image formats (WebP, AVIF)
- Enable Brotli/gzip compression
- Split code with dynamic imports
- Tree-shake unused dependencies
- Implement aggressive caching strategies

### 4. Fable Compilation Performance

**Common Bottlenecks:**
- Large generated JavaScript output
- Inefficient F# to JS translation
- Unnecessary dependencies in bundle

**Measurement Strategies:**
- Compare bundle sizes before/after changes
- Analyze generated .js files for optimization opportunities
- Profile compilation time with `--verbose` flag

**Quick Commands:**
```bash
# Build with analysis
dotnet fable --extension .jsx --run vite build

# Watch mode for fast iteration
dotnet fable watch --extension .jsx --noCache --run vite
```

**Optimization Techniques:**
- Use `[<Emit>]` for performance-critical JS interop
- Minimize use of F# collections in hot paths
- Prefer native JS APIs when appropriate
- Remove unused dependencies

## Core Web Vitals

### Key Metrics to Track:
- **LCP (Largest Contentful Paint):** < 2.5s
- **FID (First Input Delay):** < 100ms
- **CLS (Cumulative Layout Shift):** < 0.1
- **TTFB (Time to First Byte):** < 600ms

### Measurement:
```bash
# Using Lighthouse
lighthouse http://localhost:3000 --view

# Using Web Vitals library
npm install web-vitals
```

## Focused Measurement Workflow

### Quick Performance Check (< 5 minutes)
1. Build production bundle: `npm run build`
2. Check bundle size in dist/ directory
3. Run Lighthouse quick audit
4. Verify no console errors/warnings

### Detailed Performance Analysis (15-30 minutes)
1. Record Chrome Performance profile during key user flows
2. Analyze bundle with vite-bundle-visualizer
3. Test on throttled network (Slow 3G)
4. Profile memory usage for leaks

### Production-Ready Testing (1+ hour)
1. Run full Lighthouse audit suite
2. Test on real devices (mobile/desktop)
3. WebPageTest from multiple locations
4. Monitor Real User Monitoring (RUM) metrics

## Success Metrics

- **Bundle Size:** < 200KB gzipped for main bundle
- **LCP:** < 2.5s on 4G connection
- **Time to Interactive:** < 3.5s on mobile
- **Frame Rate:** Consistent 60fps during interactions
- **First Load JS:** < 100KB for critical path

## Common Trade-offs

- **Bundle Size vs. Features:** More features = larger bundles
- **SSR vs. CSR:** SSR improves initial load but adds complexity
- **Reactivity Granularity:** Fine-grained reactivity = more overhead
- **Code Splitting:** Better caching vs. more requests

## Example Performance Test

```typescript
// web-vitals-reporter.js
import {onLCP, onFID, onCLS} from 'web-vitals';

function sendToAnalytics(metric) {
  console.log(metric);
  // Send to your analytics endpoint
}

onCLS(sendToAnalytics);
onFID(sendToAnalytics);
onLCP(sendToAnalytics);
```

## Resources

- [Solid.js Performance Guide](https://www.solidjs.com/guides/performance)
- [HTMX Performance](https://htmx.org/docs/#performance)
- [Web.dev Performance](https://web.dev/performance/)
- [Vite Performance](https://vitejs.dev/guide/performance.html)
