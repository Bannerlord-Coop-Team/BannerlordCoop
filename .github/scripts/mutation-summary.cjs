// Turns the Stryker JSON reports under an output folder into a markdown summary of mutants
// that survived on changed lines. Usage: node mutation-summary.cjs <output-dir>
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const maxRows = 100;
const maxTestsPerRow = 3;
const scoredStatuses = ['Killed', 'Timeout', 'Survived', 'NoCoverage'];

// Runs git in the repo and returns its stdout.
function git(args) {
  return execFileSync('git', args, { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
}

// Returns the set of line numbers a file adds or changes since the merge base, working tree included.
function changedLines(mergeBase, file) {
  const lines = new Set();
  const diff = git(['diff', '-U0', mergeBase, '--', file]);
  for (const match of diff.matchAll(/^@@ -\S+ \+(\d+)(?:,(\d+))? @@/gm)) {
    const start = Number(match[1]);
    const count = match[2] === undefined ? 1 : Number(match[2]);
    for (let line = start; line < start + count; line++) lines.add(line);
  }
  return lines;
}

// Maps each test id in a report to a short Class.Method name.
function testNames(report) {
  const names = new Map();
  for (const testFile of Object.values(report.testFiles || {})) {
    for (const test of testFile.tests || []) {
      names.set(test.id, test.name.split('(')[0].split('.').slice(-2).join('.'));
    }
  }
  return names;
}

// Escapes text for a markdown table cell and caps its length.
function cell(text) {
  const flat = String(text).replace(/\s+/g, ' ').trim();
  const short = flat.length > 80 ? `${flat.slice(0, 77)}...` : flat;
  return short.replace(/\|/g, '\\|').replace(/`/g, "'");
}

// Describes which tests ran against a mutant, or that none did.
function coveringTests(mutant, names) {
  const ids = [...new Set(mutant.coveredBy || [])];
  if (ids.length === 0) return 'none';
  const shown = [...new Set(ids.map(id => names.get(id) || id))].slice(0, maxTestsPerRow);
  const rest = ids.length - shown.length;
  return cell(shown.join(', ')) + (rest > 0 ? ` and ${rest} more` : '');
}

// Reads one project's report and splits its scored mutants into changed-line misses and totals.
function summarizeProject(project, reportPath, mergeBase, repoRoot) {
  const report = JSON.parse(fs.readFileSync(reportPath, 'utf8'));
  const names = testNames(report);
  const counts = Object.fromEntries(scoredStatuses.map(status => [status, 0]));
  const misses = [];
  let otherMisses = 0;
  let fileCount = 0;

  for (const [filePath, file] of Object.entries(report.files)) {
    const scored = file.mutants.filter(mutant => scoredStatuses.includes(mutant.status));
    if (scored.length === 0) continue;
    fileCount++;
    const relative = path.relative(repoRoot, filePath).split(path.sep).join('/');
    const changed = changedLines(mergeBase, relative);
    for (const mutant of scored) {
      counts[mutant.status]++;
      if (mutant.status !== 'Survived' && mutant.status !== 'NoCoverage') continue;
      const line = mutant.location.start.line;
      if (!changed.has(line)) {
        otherMisses++;
        continue;
      }
      misses.push({ location: `${relative}:${line}`, mutant, tests: coveringTests(mutant, names) });
    }
  }
  return { project, counts, misses, otherMisses, fileCount };
}

// Formats a mutation score from status counts, or a dash when nothing was scored.
function score(counts) {
  const detected = counts.Killed + counts.Timeout;
  const total = detected + counts.Survived + counts.NoCoverage;
  return total === 0 ? '-' : `${(100 * detected / total).toFixed(1)}%`;
}

// Builds the markdown summary from every project's results.
function render(results) {
  const out = ['## Mutation testing (report only)', ''];
  if (results.length === 0) {
    out.push('No changed C# files in Common, Coop.Core or GameInterface, so nothing was mutated.');
    return out.join('\n');
  }
  out.push(
    'Stryker.NET broke the changed files in small ways and reran the unit tests that cover them. ' +
    'A mutant that survives on a changed line means no test fails when that line is broken, ' +
    'so a test should assert more, or the code is dead. No coverage means no test reached the line at all.',
    '',
    '| Project | Files | Killed | Timeout | Survived | No coverage | Score |',
    '| --- | --- | --- | --- | --- | --- | --- |');
  for (const { project, counts, fileCount } of results) {
    out.push(`| ${project} | ${fileCount} | ${counts.Killed} | ${counts.Timeout} | ${counts.Survived} | ${counts.NoCoverage} | ${score(counts)} |`);
  }

  // Survivors first, because a test ran and still passed, which is the weak-test signal.
  const misses = results.flatMap(result => result.misses)
    .sort((a, b) => (a.mutant.status === 'Survived' ? 0 : 1) - (b.mutant.status === 'Survived' ? 0 : 1));
  out.push('', `### Not caught on changed lines (${misses.length})`, '');
  if (misses.length === 0) out.push('None.');
  if (misses.length > 0) {
    out.push('| Location | Status | Mutation | Tests that ran |', '| --- | --- | --- | --- |');
    for (const { location, mutant, tests } of misses.slice(0, maxRows)) {
      out.push(`| \`${location}\` | ${mutant.status} | ${cell(mutant.mutatorName)}: \`${cell(mutant.replacement)}\` | ${tests} |`);
    }
    if (misses.length > maxRows) out.push('', `${misses.length - maxRows} more are in the HTML report.`);
  }

  const otherMisses = results.reduce((sum, result) => sum + result.otherMisses, 0);
  if (otherMisses > 0) {
    out.push('', `${otherMisses} more survived or had no coverage on unchanged lines of these files, see the HTML report.`);
  }
  return out.join('\n');
}

// Collects every project report under the output folder and prints the summary.
function main() {
  const outputDirectory = process.argv[2];
  if (!outputDirectory) throw new Error('usage: node mutation-summary.cjs <output-dir>');
  const repoRoot = git(['rev-parse', '--show-toplevel']).trim();
  const mergeBase = fs.readFileSync(path.join(outputDirectory, 'merge-base'), 'utf8').trim();
  const results = [];
  for (const project of fs.readdirSync(outputDirectory).sort()) {
    const reportPath = path.join(outputDirectory, project, 'reports', 'mutation-report.json');
    if (!fs.existsSync(reportPath)) continue;
    results.push(summarizeProject(project, reportPath, mergeBase, path.resolve(repoRoot)));
  }
  console.log(render(results));
}

main();
