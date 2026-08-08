// @ts-check
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: ['dist/', 'node_modules/', 'coverage/'],
  },
  ...tseslint.configs.recommended,
  {
    rules: {
      // pino-style structured logging intentionally uses untyped variadic args.
      '@typescript-eslint/no-explicit-any': 'off',
    },
  },
);
