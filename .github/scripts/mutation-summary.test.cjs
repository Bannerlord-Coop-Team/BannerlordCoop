const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { readResults, render } = require('./mutation-summary.cjs');

const root = path.resolve('/repo');

function mutant(status, line, replacement = 'a != b') {
  return { status, location: { start: { line } }, mutatorName: 'Logical mutation', replacement };
}

const reports = {
  '/out/Common/reports/mutation-report.json': {
    files: {
      [path.join(root, 'source', 'Common', 'Foo.cs')]: {
        mutants: [mutant('Survived', 5, 'a || `b`'), mutant('NoCoverage', 6), mutant('Killed', 7), mutant('Timeout', 8), mutant('Ignored', 9)],
      },
      [path.join(root, 'source', 'Common', 'Unscored.cs')]: { mutants: [mutant('CompileError', 1)] },
    },
  },
};

// Reads the runner's `<project>=<report-path>` arguments against the in-memory reports.
function results() {
  return readResults(['Common=/out/Common/reports/mutation-report.json'], file => reports[file], root);
}

test('counts killed and timed-out mutants as caught, survived and uncovered ones as not caught, and skips the rest', () => {
  const [result] = results();

  assert.equal(result.project, 'Common');
  assert.equal(result.caught, 2);
  assert.equal(result.fileCount, 1);
  assert.deepEqual(result.misses.map(miss => miss.location), ['source/Common/Foo.cs:5', 'source/Common/Foo.cs:6']);
});

test('scores caught over scored and escapes pipes and backticks in a not-caught row', () => {
  const lines = render(results()).split('\n');

  assert.ok(lines.includes('| Common | 1 | 2 | 2 | 50.0% |'), lines.join('\n'));
  assert.ok(lines.includes("| `source/Common/Foo.cs:5` | Logical mutation: `a \\|\\| 'b'` |"), lines.join('\n'));
});
