// Prints a Stryker --mutate pattern per C# file in one project, covering only the lines a branch adds
// or changes. Paths are relative to the project folder, where Stryker runs.
// Usage: node mutation-spans.cjs <merge-base> <repo-relative-project-dir>
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const repoRoot = path.resolve(__dirname, '../..');

// Returns the lines each file adds or changes in a `git diff -U0`, keyed by its new repo-relative path.
// A hunk header with no count (`+12`) is one line, and `+12,0` is a pure deletion that adds none.
function parseChangedLines(diffText) {
  const changed = new Map();
  let lines = null;
  // Only a `+++ b/` row before a file's first hunk names the file. Inside a hunk the same text is
  // an added line that starts with `++ b/`.
  let inHeader = false;
  for (const row of diffText.split('\n')) {
    if (row.startsWith('diff --git ')) {
      inHeader = true;
      lines = null;
      continue;
    }
    if (inHeader && row.startsWith('+++ b/')) {
      lines = new Set();
      changed.set(row.slice('+++ b/'.length).trimEnd(), lines);
      continue;
    }
    const hunk = /^@@ -\S+ \+(\d+)(?:,(\d+))? @@/.exec(row);
    if (!hunk) continue;
    if (!lines) throw new Error(`git diff hunk without a "+++ b/" file header: ${row}`);
    inHeader = false;
    const start = Number(hunk[1]);
    const count = hunk[2] === undefined ? 1 : Number(hunk[2]);
    for (let line = start; line < start + count; line++) lines.add(line);
  }
  return changed;
}

// Returns where each line's text starts and the offset just past it, before its `\r\n` or `\n`.
function lineBounds(text) {
  const starts = [0];
  const ends = [];
  for (let index = 0; index < text.length; index++) {
    if (text[index] !== '\n') continue;
    ends.push(text[index - 1] === '\r' ? index - 1 : index);
    starts.push(index + 1);
  }
  ends.push(text.length);
  return { starts, ends };
}

// Turns 1-based line numbers into { start, end } character spans, end exclusive, merging runs of
// adjacent lines. Each span also keeps its last line so the next line can extend it.
function lineSpans(text, lines) {
  const { starts, ends } = lineBounds(text);
  const spans = [];
  for (const line of [...lines].sort((a, b) => a - b)) {
    const last = spans[spans.length - 1];
    if (last && last.lastLine === line - 1) {
      last.lastLine = line;
      last.end = ends[line - 1];
      continue;
    }
    spans.push({ lastLine: line, start: starts[line - 1], end: ends[line - 1] });
  }
  return spans;
}

// Builds the --mutate pattern for one file, or null when the branch only deletes lines from it.
// Stryker reads sources without their byte order mark, so offsets are counted without it too.
function mutatePattern(file, text, lines) {
  const spans = lineSpans(text.replace(/^\uFEFF/, ''), lines);
  if (spans.length === 0) return null;
  return file + spans.map(({ start, end }) => `{${start}..${end}}`).join('');
}

// Builds the patterns for every changed file in one project from its diff and the files' text.
function projectPatterns(projectDirectory, diffText, readText) {
  const patterns = [];
  for (const [file, lines] of parseChangedLines(diffText)) {
    const relative = path.posix.relative(projectDirectory, file);
    const pattern = mutatePattern(relative, readText(file), lines);
    if (pattern) patterns.push(pattern);
  }
  return patterns;
}

// Prints the patterns for the project, comparing the working tree with the merge base.
// Rename detection keeps a moved file to its changed lines instead of mutating all of it, and
// quotePath=false keeps non-ASCII paths unquoted so their `+++ b/` header still parses.
function main() {
  const [mergeBase, projectDirectory] = process.argv.slice(2);
  if (!projectDirectory) throw new Error('usage: node mutation-spans.cjs <merge-base> <repo-relative-project-dir>');
  const diffText = execFileSync(
    'git',
    ['-c', 'core.quotePath=false', 'diff', '-U0', '-M', '--diff-filter=AMR', mergeBase, '--', `${projectDirectory}/*.cs`],
    { cwd: repoRoot, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const readText = file => fs.readFileSync(path.join(repoRoot, file), 'utf8');
  for (const pattern of projectPatterns(projectDirectory, diffText, readText)) console.log(pattern);
}

if (require.main === module) main();

module.exports = { parseChangedLines, mutatePattern, projectPatterns };
