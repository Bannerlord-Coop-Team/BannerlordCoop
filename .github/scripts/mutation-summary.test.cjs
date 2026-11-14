const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { summarizeProject, render } = require('./mutation-summary.cjs');

const root = path.resolve('/repo');

function mutant(status, line) {
  return { status, location: { start: { line } }, mutatorName: 'Equality mutation', replacement: 'a != b' };
}

function report() {
  return {
    files: {
      [path.join(root, 'source', 'Common', 'Foo.cs')]: {
        mutants: [mutant('Survived', 5), mutant('Killed', 7), mutant('Timeout', 8), mutant('Ignored', 9)],
      },
      [path.join(root, 'source', 'Common', 'Unscored.cs')]: { mutants: [mutant('CompileError', 1)] },
    },
  };
}

test('counts only scored mutants and keeps the survivors with their repo-relative location', () => {
  const result = summarizeProject(report(), root);

  assert.deepEqual(result.counts, { Killed: 1, Timeout: 1, Survived: 1 });
  assert.equal(result.fileCount, 1);
  assert.deepEqual(result.misses.map(miss => [miss.location, miss.status]), [['source/Common/Foo.cs:5', 'Survived']]);
});

test('scores caught over scored and lists each survivor with its mutation', () => {
  const summary = render([{ project: 'Common', ...summarizeProject(report(), root) }]);

  assert.match(summary, /^\| Common \| 1 \| 1 \| 1 \| 1 \| 66\.7% \|$/m);
  assert.match(summary, /^\| `source\/Common\/Foo\.cs:5` \| Survived \| Equality mutation: `a != b` \|$/m);
});
