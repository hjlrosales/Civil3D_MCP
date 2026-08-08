import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    include: ['suites/**/*.test.ts'],
    environment: 'node',
    testTimeout: 30000,
    hookTimeout: 30000,
    // E2E tests spawn real server processes and use real pipes; keep files sequential.
    fileParallelism: false,
  },
});
