const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { summarizeProject, render } = require('./mutation-summary.cjs');

const root = path.resolve('/repo');

function mutant(status, line, replacement = 'a != b') {
  return { status, location: { start: { line } }, mutatorName: 'Logical mutation', replacement };
}

function report() {
  return {
    files: {
      [path.join(root, 'source', 'Common', 'Foo.cs')]: {
        mutants: [mutant('Survived', 5, 'a || `b`'), mutant('NoCoverage', 6), mutant('Killed', 7), mutant('Timeout', 8), mutant('Ignored', 9)],
      },
      [path.join(root, 'source', 'Common', 'Unscored.cs')]: { mutants: [mutant('CompileError', 1)] },
    },
  };
}

test('counts killed and timed-out mutants as caught, survived and uncovered ones as not caught, and skips the rest', () => {
  const result = summarizeProject(report(), root);

  assert.deepEqual(result.counts, { caught: 2, notCaught: 2 });
  assert.equal(result.fileCount, 1);
  assert.deepEqual(result.misses.map(miss => miss.location), ['source/Common/Foo.cs:5', 'source/Common/Foo.cs:6']);
});

test('scores caught over scored and escapes pipes and backticks in a not-caught row', () => {
  const lines = render([{ project: 'Common', ...summarizeProject(report(), root) }]).split('\n');

  assert.ok(lines.includes('| Common | 1 | 2 | 2 | 50.0% |'), lines.join('\n'));
  assert.ok(lines.includes("| `source/Common/Foo.cs:5` | Logical mutation: `a \\|\\| 'b'` |"), lines.join('\n'));
});
