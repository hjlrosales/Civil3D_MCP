import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    include: ['bench/**/*.bench.ts'],
    environment: 'node',
    testTimeout: 120000,
    hookTimeout: 120000,
    // Benchmarks spawn real processes and share pipes; keep files sequential.
    fileParallelism: false,
  },
});
