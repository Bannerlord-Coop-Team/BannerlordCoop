// Turns the Stryker JSON reports under an output folder into a markdown summary of the mutants
// no unit test caught. Usage: node mutation-summary.cjs <output-dir> [project...]
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '../..');
const maxRows = 100;
const maxCellLength = 80;
const detectedStatuses = ['Killed', 'Timeout'];
// NoCoverage only appears with coverage analysis on, which source/stryker-config.json turns off.
// It stays here so the score is still right if coverage analysis is turned back on.
const missedStatuses = ['Survived', 'NoCoverage'];
const scoredStatuses = [...detectedStatuses, ...missedStatuses];

// Reads one project's parsed report into status counts and the mutants no test caught.
function summarizeProject(project, report, root = repoRoot) {
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
      });
    }
  }
  return { project, finished: true, counts, misses, fileCount };
}

// Summarizes a scheduled project's report text, or marks the project unfinished when Stryker left
// no report because it failed or never got to it.
function projectResult(project, reportText) {
  if (reportText === null) return { project, finished: false };
  return summarizeProject(project, JSON.parse(reportText));
}

// Reads the report of each project the runner scheduled.
function readResults(outputDirectory, projects) {
  return projects.map(project => {
    const reportPath = path.join(outputDirectory, project, 'reports', 'mutation-report.json');
    if (!fs.existsSync(reportPath)) return projectResult(project, null);
    return projectResult(project, fs.readFileSync(reportPath, 'utf8'));
  });
}

// Escapes text for a markdown table cell and caps its length.
function cell(text) {
  const flat = String(text).replace(/\s+/g, ' ').trim();
  const short = flat.length > maxCellLength ? `${flat.slice(0, maxCellLength - 3)}...` : flat;
  return short.replace(/\|/g, '\\|').replace(/`/g, "'");
}

// Joins cells into one markdown table row.
function tableRow(cells) {
  return `| ${cells.join(' | ')} |`;
}

// Builds a markdown table from its column headers and rows of cells.
function table(headers, rows) {
  return [tableRow(headers), tableRow(headers.map(() => '---')), ...rows.map(tableRow)];
}

// Formats a mutation score from status counts, or a dash when nothing was scored.
function score(counts) {
  const detected = detectedStatuses.reduce((sum, status) => sum + counts[status], 0);
  const total = scoredStatuses.reduce((sum, status) => sum + counts[status], 0);
  return total === 0 ? '-' : `${(100 * detected / total).toFixed(1)}%`;
}

// Builds the per-project score table, with a row for each project that has no report.
function renderScores(results) {
  const rows = results.map(result => {
    if (!result.finished) return [result.project, '-', '-', '-', '-', "no report, Stryker failed or didn't finish, see the step log"];
    const { project, counts, fileCount } = result;
    return [project, fileCount, counts.Killed, counts.Timeout, counts.Survived, score(counts)];
  });
  return table(['Project', 'Files', 'Killed', 'Timeout', 'Survived', 'Score'], rows);
}

// Builds the table of mutants no test caught, capped at maxRows.
function renderMisses(results) {
  const misses = results.filter(result => result.finished).flatMap(result => result.misses);
  const heading = [`### Not caught (${misses.length})`, ''];
  if (misses.length === 0) return [...heading, 'None.'];

  const rows = misses.slice(0, maxRows).map(miss =>
    [`\`${miss.location}\``, miss.status, `${cell(miss.mutation)}: \`${cell(miss.replacement)}\``]);
  const overflow = misses.length > maxRows ? ['', `${misses.length - maxRows} more are in the HTML report.`] : [];
  return [...heading, ...table(['Location', 'Status', 'Mutation'], rows), ...overflow];
}

// Builds the markdown summary from every project's results.
function render(results) {
  const title = ['## Mutation testing (report only)', ''];
  if (results.length === 0) {
    return [...title, 'No changed C# lines in a mutation-tested project, so nothing was mutated.'].join('\n');
  }
  const intro =
    'Stryker.NET broke the changed lines in small ways and reran the unit tests that cover the project. ' +
    'A survivor means no test fails when that line is broken, so a test should assert more, or the code is dead.';
  return [...title, intro, '', ...renderScores(results), '', ...renderMisses(results)].join('\n');
}

// Prints the summary for the output folder and scheduled projects given on the command line.
function main() {
  const [outputDirectory, ...projects] = process.argv.slice(2);
  if (!outputDirectory) throw new Error('usage: node mutation-summary.cjs <output-dir> [project...]');
  console.log(render(readResults(outputDirectory, projects)));
}

if (require.main === module) main();

module.exports = { projectResult, summarizeProject, render };
