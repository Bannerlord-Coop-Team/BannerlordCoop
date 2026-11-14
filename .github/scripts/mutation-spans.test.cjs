const { test } = require('node:test');
const assert = require('node:assert/strict');
const { parseChangedLines, mutatePattern, projectPatterns } = require('./mutation-spans.cjs');

const diff = [
  'diff --git a/source/Common/A.cs b/source/Common/A.cs',
  'index 1111111..2222222 100644',
  '--- a/source/Common/A.cs',
  '+++ b/source/Common/A.cs',
  '@@ -3 +3 @@ class A',
  '-old',
  '+++ b/not-a-header',
  '@@ -5,2 +5,3 @@',
  '@@ -9,2 +11,0 @@',
  'diff --git a/source/Common/Old.cs b/source/Common/Sub/New.cs',
  'similarity index 90%',
  'rename from source/Common/Old.cs',
  'rename to source/Common/Sub/New.cs',
  '--- a/source/Common/Old.cs',
  '+++ b/source/Common/Sub/New.cs',
  '@@ -2,0 +3 @@',
  '+added',
].join('\n');

test('reads each file\'s added and changed lines, skipping pure deletions and keying renames by the new path', () => {
  const changed = parseChangedLines(diff);

  assert.deepEqual([...changed.keys()], ['source/Common/A.cs', 'source/Common/Sub/New.cs']);
  assert.deepEqual([...changed.get('source/Common/A.cs')], [3, 5, 6, 7]);
  assert.deepEqual([...changed.get('source/Common/Sub/New.cs')], [3]);
});

test('covers each changed line from its first character to before its line break, merging adjacent lines', () => {
  const text = 'a\r\nbb\r\nccc\r\ndddd\r\n';

  assert.equal(mutatePattern('x.cs', text, new Set([2, 3])), 'x.cs{3..10}');
  assert.equal(mutatePattern('x.cs', text, new Set([1, 4])), 'x.cs{0..1}{12..16}');
});

test('counts offsets without the byte order mark, as Stryker reads the source', () => {
  assert.equal(mutatePattern('x.cs', '\uFEFFa\nbb', new Set([2])), 'x.cs{2..4}');
});

test('gives paths relative to the project folder and no pattern for a file with only deletions', () => {
  const texts = { 'source/Common/A.cs': 'l1\nl2\nl3\nl4\nl5\nl6\nl7\n', 'source/Common/Sub/New.cs': 'x\ny\nz\n' };
  const deletionOnly = 'diff --git a/source/Common/D.cs b/source/Common/D.cs\n+++ b/source/Common/D.cs\n@@ -2,1 +1,0 @@\n';

  const patterns = projectPatterns('source/Common', `${diff}\n${deletionOnly}`, file => texts[file] ?? 'a\nb\n');

  assert.deepEqual(patterns, ['A.cs{6..8}{12..20}', 'Sub/New.cs{4..5}']);
});
