const { createCjsPreset } = require('jest-preset-angular/presets');

/** @type {import('jest').Config} */
module.exports = {
  ...createCjsPreset(),
  setupFilesAfterEnv: ['<rootDir>/jest.setup.ts'],
  testMatch: ['<rootDir>/src/**/*.spec.ts'],
  testPathIgnorePatterns: ['/node_modules/', '/e2e/', '/dist/'],
  moduleNameMapper: {
    '^d3-(.*)$': '<rootDir>/node_modules/d3-$1/dist/d3-$1.min.js',
    '^rxjs/operators$': '<rootDir>/node_modules/rxjs/dist/cjs/operators/index.js',
  },
  transformIgnorePatterns: [
    'node_modules/(?!(.*\\.mjs$|@angular|@swimlane|d3-.*|internmap|delaunator|robust-predicates)/)'
  ],
};
