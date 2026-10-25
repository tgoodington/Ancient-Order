import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';

const layeringPatterns = [
  {
    group: ['**/ui/**', '**/render/**'],
    message: 'Engine, session, and persistence code must not import UI or renderer code.',
  },
];

export default tseslint.config(
  {
    files: ['src/**/*.{ts,tsx}', 'tests/**/*.ts'],
    extends: [tseslint.configs.recommended],
    languageOptions: {
      parserOptions: {
        ecmaVersion: 2022,
        sourceType: 'module',
      },
    },
    rules: {
      '@typescript-eslint/no-unused-vars': [
        'error',
        {
          argsIgnorePattern: '^_',
          varsIgnorePattern: '^_',
          caughtErrorsIgnorePattern: '^_',
        },
      ],
      '@typescript-eslint/explicit-function-return-type': 'off',
      '@typescript-eslint/no-explicit-any': 'warn',
    },
  },
  {
    files: ['src/ui/**/*.{ts,tsx}', 'src/render/**/*.{ts,tsx}'],
    extends: [reactHooks.configs.flat.recommended],
  },
  {
    files: [
      'src/combat/**',
      'src/narrative/**',
      'src/personality/**',
      'src/dialogue/**',
      'src/state/**',
      'src/types/**',
      'src/fixtures/**',
      'src/session/**',
      'src/persistence/**',
      'tests/**',
    ].flatMap((p) => (p.endsWith('/**') ? [`${p}/*.ts`] : [p])),
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [
            { name: 'react', message: 'Engine code must not depend on React.' },
            { name: 'react-dom', message: 'Engine code must not depend on React.' },
            { name: 'phaser', message: 'Engine code must not depend on the renderer.' },
            { name: 'pixi.js', message: 'Engine code must not depend on the renderer.' },
          ],
          patterns: layeringPatterns,
        },
      ],
    },
  }
);
