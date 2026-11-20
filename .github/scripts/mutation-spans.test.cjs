const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { parseChangedLines, mutatePattern, projectPatterns } = require('./mutation-spans.cjs');

test('scopes a real git diff to changed lines, renames, files moved in and untracked files', t => {
  const repo = fs.mkdtempSync(path.join(os.tmpdir(), 'mutation-spans-'));
  t.after(() => fs.rmSync(repo, { recursive: true, force: true }));
  const git = (...args) => execFileSync('git', args, { cwd: repo, encoding: 'utf8' });
  const write = (file, text) => {
    fs.mkdirSync(path.dirname(path.join(repo, file)), { recursive: true });
    fs.writeFileSync(path.join(repo, file), text);
  };
  git('init', '-q');
  git('config', 'core.autocrlf', 'false');
  git('config', 'user.name', 'test');
  git('config', 'user.email', 'test@example.com');
  write('source/Common/Edited.cs', 'a\nb\nc\n');
  write('source/Common/Old.cs', 'one\ntwo\nthree\nfour\nfive\n');
  write('source/Other/Moved.cs', 'p\nq\nr\ns\nt\n');
  write('source/Common/Trimmed.cs', 'x\ny\nz\n');
  git('add', '.');
  git('commit', '-qm', 'base');
  const base = git('rev-parse', 'HEAD').trim();

  write('source/Common/Edited.cs', 'a\nB\nc\n');
  fs.mkdirSync(path.join(repo, 'source/Common/Sub'));
  git('mv', 'source/Common/Old.cs', 'source/Common/Sub/Renamed.cs');
  write('source/Common/Sub/Renamed.cs', 'one\ntwo\nthree\nFOUR\nfive\n');
  git('mv', 'source/Other/Moved.cs', 'source/Common/Moved.cs');
  write('source/Common/Moved.cs', 'p\nQ\nr\ns\nt\n');
  write('source/Common/Trimmed.cs', 'x\nz\n');
  write('source/Common/New.cs', 'n1\nn2\n');

  const output = execFileSync('node', [path.join(__dirname, 'mutation-spans.cjs'), base, 'source/Common'], { cwd: repo, encoding: 'utf8' });

  // Each edit covers only its changed line, the move from source/Other keeps to its one changed line,
  // Trimmed.cs only lost a line so it has no pattern, and untracked New.cs is covered whole.
  assert.deepEqual(output.trim().split('\n').sort(), [
    'Edited.cs{2..3}',
    'Moved.cs{2..3}',
    'New.cs{0..5}',
    'Sub/Renamed.cs{14..18}',
  ]);
});

// Hunk bodies are mostly left out, because only the file and hunk headers are parsed.
const diff = [
  'diff --git a/source/Common/A.cs b/source/Common/A.cs',
  'index 1111111..2222222 100644',
  '--- a/source/Common/A.cs',
  '+++ b/source/Common/A.cs',
  '@@ -3 +3 @@ class A',
  '-old',
  '+++ b/not-a-header', // an added line inside a hunk that looks like a file header, not one
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
  const texts = {
    'source/Common/A.cs': 'l1\nl2\nl3\nl4\nl5\nl6\nl7\n',
    'source/Common/Sub/New.cs': 'x\ny\nz\n',
    'source/Common/D.cs': 'a\nb\n',
  };
  const deletionOnly = 'diff --git a/source/Common/D.cs b/source/Common/D.cs\n+++ b/source/Common/D.cs\n@@ -2,1 +1,0 @@\n';

  const patterns = projectPatterns('source/Common', `${diff}\n${deletionOnly}`, [], file => texts[file]);

  // A.cs: {6..8} is line 3 and {12..20} is lines 5 to 7. New.cs: {4..5} is line 3.
  assert.deepEqual(patterns, ['A.cs{6..8}{12..20}', 'Sub/New.cs{4..5}']);
});
