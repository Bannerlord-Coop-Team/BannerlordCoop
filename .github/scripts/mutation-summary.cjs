// Turns Stryker JSON reports into a markdown summary of the mutants no unit test caught.
// Usage: node mutation-summary.cjs [<project>=<mutation-report.json>...]
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '../..');
const maxRows = 100;
const maxCellLength = 80;
const detectedStatuses = ['Killed', 'Timeout'];
const missedStatuses = ['Survived'];
const scoredStatuses = [...detectedStatuses, ...missedStatuses];

// Reads one parsed report into status counts and the mutants no test caught.
function summarizeProject(report, root = repoRoot) {
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
  return { counts, misses, fileCount };
}

// Reads each `project=report-path` argument into that project's summary.
function readResults(reportArguments) {
  return reportArguments.map(argument => {
    const separator = argument.indexOf('=');
    if (separator < 1) throw new Error(`expected <project>=<mutation-report.json>, got: ${argument}`);
    const report = JSON.parse(fs.readFileSync(argument.slice(separator + 1), 'utf8'));
    return { project: argument.slice(0, separator), ...summarizeProject(report) };
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

// Builds the per-project score table.
function renderScores(results) {
  const rows = results.map(({ project, counts, fileCount }) =>
    [project, fileCount, counts.Killed, counts.Timeout, counts.Survived, score(counts)]);
  return table(['Project', 'Files', 'Killed', 'Timeout', 'Survived', 'Score'], rows);
}

// Builds the table of mutants no test caught, capped at maxRows.
function renderMisses(results) {
  const misses = results.flatMap(result => result.misses);
  const heading = [`### Not caught (${misses.length})`, ''];
  if (misses.length === 0) return [...heading, 'None.'];

  const rows = misses.slice(0, maxRows).map(miss =>
    [`\`${miss.location}\``, miss.status, `${cell(miss.mutation)}: \`${cell(miss.replacement)}\``]);
  const lines = [...heading, ...table(['Location', 'Status', 'Mutation'], rows)];
  if (misses.length <= maxRows) return lines;
  return [...lines, '', `${misses.length - maxRows} more are in the HTML report.`];
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

// Prints the summary for the project reports given on the command line.
function main() {
  console.log(render(readResults(process.argv.slice(2))));
}

if (require.main === module) main();

module.exports = { summarizeProject, render };
