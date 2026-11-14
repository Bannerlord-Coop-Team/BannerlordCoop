const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { parseReportArgument, summarizeProject, render } = require('./mutation-summary.cjs');

const root = path.resolve('/repo');

function mutant(status, line, replacement = 'a != b') {
  return { status, location: { start: { line } }, mutatorName: 'Logical mutation', replacement };
}

function report() {
  return {
    files: {
      [path.join(root, 'source', 'Common', 'Foo.cs')]: {
        mutants: [mutant('Survived', 5, 'a || `b`'), mutant('Killed', 7), mutant('Timeout', 8), mutant('Ignored', 9)],
      },
      [path.join(root, 'source', 'Common', 'Unscored.cs')]: { mutants: [mutant('CompileError', 1)] },
    },
  };
}

test('splits a report argument at the first =, and rejects one with no project name', () => {
  assert.deepEqual(parseReportArgument('Coop.Core=/r/a=b.json'), { project: 'Coop.Core', reportPath: '/r/a=b.json' });
  assert.throws(() => parseReportArgument('/r/mutation-report.json'), /expected <project>=/);
});

test('counts only scored mutants and keeps the survivors with their repo-relative location', () => {
  const result = summarizeProject(report(), root);

  assert.deepEqual(result.counts, { Killed: 1, Timeout: 1, Survived: 1 });
  assert.equal(result.fileCount, 1);
  assert.deepEqual(result.survivors.map(survivor => survivor.location), ['source/Common/Foo.cs:5']);
});

test('scores caught over scored and escapes pipes and backticks in the survivor row', () => {
  const lines = render([{ project: 'Common', ...summarizeProject(report(), root) }]).split('\n');

  assert.ok(lines.includes('| Common | 1 | 1 | 1 | 1 | 66.7% |'), lines.join('\n'));
  assert.ok(lines.includes("| `source/Common/Foo.cs:5` | Logical mutation: `a \\|\\| 'b'` |"), lines.join('\n'));
});
