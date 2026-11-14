const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { shortTestName, summarizeProject, render } = require('./mutation-summary.cjs');

const root = path.resolve('/repo');

function mutant(status, line, coveredBy = []) {
  return { status, location: { start: { line } }, mutatorName: 'Equality mutation', replacement: 'a != b', coveredBy };
}

function report() {
  return {
    files: {
      [path.join(root, 'source', 'Common', 'Foo.cs')]: {
        mutants: [
          mutant('NoCoverage', 6),
          mutant('Survived', 5, ['t1', 't2']),
          mutant('Killed', 7, ['t1']),
          mutant('Timeout', 8, ['t1']),
          mutant('Ignored', 9),
        ],
      },
      [path.join(root, 'source', 'Common', 'Unscored.cs')]: { mutants: [mutant('CompileError', 1)] },
    },
    testFiles: {
      'FooTests.cs': { tests: [{ id: 't1', name: 'Common.Tests.FooTests.Adds(x: 1)' }, { id: 't2', name: 'Common.Tests.FooTests.Removes' }] },
    },
  };
}

test('shortens a full test name to Class.Method', () => {
  assert.equal(shortTestName('Ns.Sub.Class.Method(x: 1)'), 'Class.Method');
});

test('counts only scored mutants and keeps the ones no test caught, with the tests that ran', () => {
  const result = summarizeProject('Common', report(), root);

  assert.deepEqual(result.counts, { Killed: 1, Timeout: 1, Survived: 1, NoCoverage: 1 });
  assert.equal(result.fileCount, 1);
  assert.deepEqual(result.misses.map(miss => [miss.location, miss.status, miss.tests]), [
    ['source/Common/Foo.cs:6', 'NoCoverage', []],
    ['source/Common/Foo.cs:5', 'Survived', ['FooTests.Adds', 'FooTests.Removes']],
  ]);
});

test('lists survivors before no-coverage mutants and scores caught over scored', () => {
  const summary = render([summarizeProject('Common', report(), root)]);

  assert.match(summary, /\| Common \| 1 \| 1 \| 1 \| 1 \| 1 \| 50\.0% \|/);
  assert.ok(summary.indexOf('Foo.cs:5') < summary.indexOf('Foo.cs:6'));
  assert.match(summary, /### Not caught \(2\)/);
});

test('reports a project without a report as failed instead of as having nothing to mutate', () => {
  const summary = render([{ project: 'Coop.Core', finished: false }]);

  assert.match(summary, /\| Coop\.Core \|.*no report, Stryker failed or didn't finish/);
  assert.doesNotMatch(summary, /nothing was mutated/);
});

test('says nothing was mutated only when no project was scheduled', () => {
  assert.match(render([]), /nothing was mutated/);
});
