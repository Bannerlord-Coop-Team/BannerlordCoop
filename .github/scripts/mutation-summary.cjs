// Turns Stryker JSON reports into a markdown summary of the mutants no unit test caught.
// Usage: node mutation-summary.cjs [<project>=<mutation-report.json>...]
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '../..');
const maxRows = 100;
const maxCellLength = 80;
const scoredStatuses = ['Killed', 'Timeout', 'Survived'];

// Reads one parsed report into status counts and the mutants that survived.
function summarizeProject(report, root = repoRoot) {
  const counts = Object.fromEntries(scoredStatuses.map(status => [status, 0]));
  const survivors = [];
  let fileCount = 0;

  for (const [filePath, file] of Object.entries(report.files)) {
    const scored = file.mutants.filter(mutant => scoredStatuses.includes(mutant.status));
    if (scored.length === 0) continue;
    fileCount++;
    const relative = path.relative(root, filePath).split(path.sep).join('/');
    for (const mutant of scored) {
      counts[mutant.status]++;
      if (mutant.status !== 'Survived') continue;
      survivors.push({
        location: `${relative}:${mutant.location.start.line}`,
        mutation: mutant.mutatorName,
        replacement: mutant.replacement,
      });
    }
  }
  return { counts, survivors, fileCount };
}

// Splits a `<project>=<report-path>` argument, which needs a project name before the `=`.
function parseReportArgument(argument) {
  const separator = argument.indexOf('=');
  const hasProjectName = separator > 0;
  if (!hasProjectName) throw new Error(`expected <project>=<mutation-report.json>, got: ${argument}`);
  return { project: argument.slice(0, separator), reportPath: argument.slice(separator + 1) };
}

// Reads each report argument into that project's summary.
function readResults(reportArguments) {
  return reportArguments.map(parseReportArgument).map(({ project, reportPath }) => {
    const report = JSON.parse(fs.readFileSync(reportPath, 'utf8'));
    return { project, ...summarizeProject(report) };
  });
}

// Escapes text for a markdown table cell and caps its length. Backticks become quotes so a
// value can't close the code span it is shown in.
function cell(text) {
  const flat = String(text).replace(/\s+/g, ' ').trim();
  const short = flat.length > maxCellLength ? `${flat.slice(0, maxCellLength - 3)}...` : flat;
  return short.replace(/\|/g, '\\|').replace(/`/g, "'");
}

// Wraps text in a markdown code span.
function codeSpan(text) {
  return '`' + text + '`';
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
  const total = scoredStatuses.reduce((sum, status) => sum + counts[status], 0);
  if (total === 0) return '-';
  return `${(100 * (counts.Killed + counts.Timeout) / total).toFixed(1)}%`;
}

// Builds the per-project score table.
function renderScores(results) {
  const rows = results.map(({ project, counts, fileCount }) =>
    [project, fileCount, counts.Killed, counts.Timeout, counts.Survived, score(counts)]);
  return table(['Project', 'Files', 'Killed', 'Timeout', 'Survived', 'Score'], rows);
}

// Builds the table of surviving mutants, capped at maxRows.
function renderSurvivors(results) {
  const survivors = results.flatMap(result => result.survivors);
  const heading = [`### Survived (${survivors.length})`, ''];
  if (survivors.length === 0) return [...heading, 'None.'];

  const rows = survivors.slice(0, maxRows).map(survivor => {
    const location = codeSpan(survivor.location);
    const mutation = `${cell(survivor.mutation)}: ${codeSpan(cell(survivor.replacement))}`;
    return [location, mutation];
  });
  const lines = [...heading, ...table(['Location', 'Mutation'], rows)];
  if (survivors.length <= maxRows) return lines;
  return [...lines, '', `${survivors.length - maxRows} more are in the HTML report.`];
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
  return [...title, intro, '', ...renderScores(results), '', ...renderSurvivors(results)].join('\n');
}

// Prints the summary for the project reports given on the command line.
function main() {
  console.log(render(readResults(process.argv.slice(2))));
}

if (require.main === module) main();

module.exports = { parseReportArgument, summarizeProject, render };
