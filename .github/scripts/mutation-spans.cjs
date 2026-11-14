// Prints a Stryker --mutate pattern per file that covers only the lines a branch adds or changes.
// Usage: node mutation-spans.cjs <merge-base> <repo-relative-file>...
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const repoRoot = path.resolve(__dirname, '../..');

// Returns the line numbers a `git diff -U0` adds or changes, read from its hunk headers.
// A header with no count (`+12`) is one line, and `+12,0` is a pure deletion that adds none.
function parseChangedLines(diffText) {
  const lines = new Set();
  for (const match of diffText.matchAll(/^@@ -\S+ \+(\d+)(?:,(\d+))? @@/gm)) {
    const start = Number(match[1]);
    const count = match[2] === undefined ? 1 : Number(match[2]);
    for (let line = start; line < start + count; line++) lines.add(line);
  }
  return lines;
}

// Returns the character offset where each line starts, then the offset just past each line's text.
function lineBounds(text) {
  const starts = [0];
  for (let index = 0; index < text.length; index++) {
    if (text[index] === '\n') starts.push(index + 1);
  }
  const ends = starts.map((start, line) => {
    const next = line + 1 < starts.length ? starts[line + 1] - 1 : text.length;
    return text[next - 1] === '\r' ? next - 1 : next;
  });
  return { starts, ends };
}

// Turns 1-based line numbers into [start, end) character spans, merging runs of adjacent lines.
function lineSpans(text, lines) {
  const { starts, ends } = lineBounds(text);
  const sorted = [...lines].sort((a, b) => a - b);
  const spans = [];
  for (const line of sorted) {
    const last = spans[spans.length - 1];
    if (last && last.lastLine === line - 1) {
      last.lastLine = line;
      last.end = ends[line - 1];
      continue;
    }
    spans.push({ lastLine: line, start: starts[line - 1], end: ends[line - 1] });
  }
  return spans.map(({ start, end }) => [start, end]);
}

// Builds the --mutate pattern for one file, or null when the branch only deletes lines from it.
// Stryker reads sources without their byte order mark, so offsets are counted without it too.
function mutatePattern(file, text, diffText) {
  const spans = lineSpans(text.replace(/^﻿/, ''), parseChangedLines(diffText));
  if (spans.length === 0) return null;
  return file + spans.map(([start, end]) => `{${start}..${end}}`).join('');
}

// Prints the pattern for each file, comparing the working tree with the merge base.
function main() {
  const [mergeBase, ...files] = process.argv.slice(2);
  if (!mergeBase) throw new Error('usage: node mutation-spans.cjs <merge-base> <repo-relative-file>...');
  for (const file of files) {
    const diffText = execFileSync('git', ['diff', '-U0', mergeBase, '--', file], { cwd: repoRoot, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    const text = fs.readFileSync(path.join(repoRoot, file), 'utf8');
    const pattern = mutatePattern(file, text, diffText);
    if (pattern) console.log(pattern);
  }
}

if (require.main === module) main();

module.exports = { parseChangedLines, lineSpans, mutatePattern };
