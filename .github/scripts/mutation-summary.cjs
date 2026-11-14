// Turns the Stryker JSON reports under an output folder into a markdown summary of the mutants
// no unit test caught. Usage: node mutation-summary.cjs <output-dir>
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '../..');
const maxRows = 100;
const maxTestsPerRow = 3;
const maxCellLength = 80;
const detectedStatuses = ['Killed', 'Timeout'];
const missedStatuses = ['Survived', 'NoCoverage'];
const scoredStatuses = [...detectedStatuses, ...missedStatuses];

// Shortens a full test name to Class.Method, e.g. `Ns.Sub.Class.Method(x: 1)` becomes `Class.Method`.
function shortTestName(fullName) {
  const withoutArguments = fullName.split('(')[0];
  return withoutArguments.split('.').slice(-2).join('.');
}

// Maps each test id in a report to its short name. `testFiles`, `tests` and a mutant's `coveredBy`
// are optional in Stryker's report schema, hence the empty defaults here and in summarizeProject.
function testNames(report) {
  const names = new Map();
  for (const testFile of Object.values(report.testFiles || {})) {
    for (const test of testFile.tests || []) names.set(test.id, shortTestName(test.name));
  }
  return names;
}

// Reads one project's parsed report into status counts and the mutants no test caught.
function summarizeProject(project, report, root = repoRoot) {
  const names = testNames(report);
  const counts = Object.fromEntries(scoredStatuses.map(status => [status, 0]));
  const misses = [];
  let fileCount = 0;

  for (const [filePath, file] of Object.entries(report.files)) {
    const scored = file.mutants.filter(mutant => scoredStatuses.includes(mutant.status));
    if (scored.length === 0) continue;
    fileCount++;
    const relative = path.relative(root, filePath).split(path.sep).join('/');
    for (const mutant of scored) {
      counts[mutant.status]++;
      if (!missedStatuses.includes(mutant.status)) continue;
      misses.push({
        location: `${relative}:${mutant.location.start.line}`,
        status: mutant.status,
        mutation: mutant.mutatorName,
        replacement: mutant.replacement,
        tests: [...new Set((mutant.coveredBy || []).map(id => names.get(id) || id))],
      });
    }
  }
  return { project, finished: true, counts, misses, fileCount };
}

// Reads every project folder under the output folder. The runner creates a folder before it
// mutates a project, so a folder without a report is a project that failed or didn't finish.
function readResults(outputDirectory) {
  return fs.readdirSync(outputDirectory, { withFileTypes: true })
    .filter(entry => entry.isDirectory())
    .map(entry => entry.name)
    .sort()
    .map(project => {
      const reportPath = path.join(outputDirectory, project, 'reports', 'mutation-report.json');
      if (!fs.existsSync(reportPath)) return { project, finished: false };
      return summarizeProject(project, JSON.parse(fs.readFileSync(reportPath, 'utf8')));
    });
}

// Escapes text for a markdown table cell and caps its length.
function cell(text) {
  const flat = String(text).replace(/\s+/g, ' ').trim();
  const short = flat.length > maxCellLength ? `${flat.slice(0, maxCellLength - 3)}...` : flat;
  return short.replace(/\|/g, '\\|').replace(/`/g, "'");
}

// Formats a mutation score from status counts, or a dash when nothing was scored.
function score(counts) {
  const detected = detectedStatuses.reduce((sum, status) => sum + counts[status], 0);
  const total = scoredStatuses.reduce((sum, status) => sum + counts[status], 0);
  return total === 0 ? '-' : `${(100 * detected / total).toFixed(1)}%`;
}

// Lists the first few tests that ran against a mutant, or says none did.
function formatTests(tests) {
  if (tests.length === 0) return 'none';
  const rest = tests.length - maxTestsPerRow;
  return cell(tests.slice(0, maxTestsPerRow).join(', ')) + (rest > 0 ? ` and ${rest} more` : '');
}

// Builds the per-project score table, with a row for each project that has no report.
function renderScores(results) {
  const rows = results.map(result => {
    if (!result.finished) return `| ${result.project} | - | - | - | - | - | no report, Stryker failed or didn't finish, see the step log |`;
    const { project, counts, fileCount } = result;
    return `| ${project} | ${fileCount} | ${counts.Killed} | ${counts.Timeout} | ${counts.Survived} | ${counts.NoCoverage} | ${score(counts)} |`;
  });
  return ['| Project | Files | Killed | Timeout | Survived | No coverage | Score |', '| --- | --- | --- | --- | --- | --- | --- |', ...rows];
}

// Sorts survivors first, because a test ran and still passed, which is the weak-test signal.
const survivorsFirst = miss => (miss.status === 'Survived' ? 0 : 1);

// Builds the table of mutants no test caught, capped at maxRows.
function renderMisses(results) {
  const misses = results.filter(result => result.finished).flatMap(result => result.misses);
  misses.sort((a, b) => survivorsFirst(a) - survivorsFirst(b));
  const heading = [`### Not caught (${misses.length})`, ''];
  if (misses.length === 0) return [...heading, 'None.'];

  const rows = misses.slice(0, maxRows).map(miss =>
    `| \`${miss.location}\` | ${miss.status} | ${cell(miss.mutation)}: \`${cell(miss.replacement)}\` | ${formatTests(miss.tests)} |`);
  const overflow = misses.length > maxRows ? ['', `${misses.length - maxRows} more are in the HTML report.`] : [];
  return [...heading, '| Location | Status | Mutation | Tests that ran |', '| --- | --- | --- | --- |', ...rows, ...overflow];
}

// Builds the markdown summary from every project's results.
function render(results) {
  const title = ['## Mutation testing (report only)', ''];
  if (results.length === 0) {
    return [...title, 'No changed C# lines in a mutation-tested project, so nothing was mutated.'].join('\n');
  }
  const intro =
    'Stryker.NET broke the changed lines in small ways and reran the unit tests that cover them. ' +
    'A survivor means no test fails when that line is broken, so a test should assert more, or the code is dead. ' +
    'No coverage means no test reached the line at all.';
  return [...title, intro, '', ...renderScores(results), '', ...renderMisses(results)].join('\n');
}

// Prints the summary for the output folder given on the command line.
function main() {
  const outputDirectory = process.argv[2];
  if (!outputDirectory) throw new Error('usage: node mutation-summary.cjs <output-dir>');
  console.log(render(readResults(outputDirectory)));
}

if (require.main === module) main();

module.exports = { shortTestName, summarizeProject, render };
