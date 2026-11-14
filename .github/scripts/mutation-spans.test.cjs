const { test } = require('node:test');
const assert = require('node:assert/strict');
const { parseChangedLines, mutatePattern } = require('./mutation-spans.cjs');

test('reads added and changed lines from hunk headers, skipping pure deletions', () => {
  const diff = [
    'diff --git a/x.cs b/x.cs',
    '@@ -3 +3 @@',
    '@@ -5,2 +5,3 @@',
    '@@ -9,2 +11,0 @@',
  ].join('\n');

  assert.deepEqual([...parseChangedLines(diff)], [3, 5, 6, 7]);
});

test('covers each changed line from its first character to before its line break, merging adjacent lines', () => {
  const text = 'a\r\nbb\r\nccc\r\ndddd\r\n';

  assert.equal(mutatePattern('x.cs', text, '@@ -2,2 +2,2 @@'), 'x.cs{3..10}');
  assert.equal(mutatePattern('x.cs', text, '@@ -1 +1 @@\n@@ -4 +4 @@'), 'x.cs{0..1}{12..16}');
});

test('counts offsets without the byte order mark, as Stryker reads the source', () => {
  assert.equal(mutatePattern('x.cs', '﻿a\nbb', '@@ -2 +2 @@'), 'x.cs{2..4}');
});

test('gives no pattern for a file the branch only deletes lines from, so Stryker never mutates the whole file', () => {
  assert.equal(mutatePattern('x.cs', 'a\nb\n', '@@ -2,1 +1,0 @@'), null);
});
