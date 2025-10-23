import { defineConfig } from 'vite'
import solidPlugin from 'vite-plugin-solid'
import tailwindcss from '@tailwindcss/vite'

// https://vitejs.dev/config/
export default defineConfig({
    clearScreen: false,
    server: {
        watch: {
            ignored: [
                "**/*.md" , // Don't watch markdown files
                "**/*.fs" , // Don't watch F# files
                "**/*.fsx"  // Don't watch F# script files
            ]
        }
    },
    plugins: [
        solidPlugin(),
        tailwindcss(),
    ],
    build: {
        // Enable source maps for better debugging while keeping bundle size reasonable
        sourcemap: false,
        // Optimize chunk size warnings
        chunkSizeWarningLimit: 600,
        rollupOptions: {
            output: {
                // Manual chunk splitting to separate vendor code from app code
                manualChunks: (id) => {
                    // Separate solid-js and related libraries into vendor chunk
                    if (id.includes('node_modules')) {
                        if (id.includes('solid-js') || id.includes('@solidjs')) {
                            return 'vendor-solid';
                        }
                        // Other node_modules go into a general vendor chunk
                        return 'vendor';
                    }
                },
                // Optimize chunk naming for better caching
                chunkFileNames: 'assets/[name]-[hash].js',
                entryFileNames: 'assets/[name]-[hash].js',
                assetFileNames: 'assets/[name]-[hash].[ext]'
            }
        },
        // Target modern browsers for smaller bundles
        target: 'es2020',
        // Enable minification optimizations
        minify: 'esbuild',
        // CSS code splitting
        cssCodeSplit: true
    }
})
