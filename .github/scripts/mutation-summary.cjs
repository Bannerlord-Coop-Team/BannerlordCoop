// Turns Stryker JSON reports into a markdown summary of the mutants no unit test caught.
// Usage: node mutation-summary.cjs [<project>=<mutation-report.json>...]
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '../..');
const maxRows = 100;
const maxCellLength = 80;
const caughtStatuses = ['Killed', 'Timeout'];
// NoCoverage only appears when coverage analysis is on. Counting it as not caught keeps the score
// equal to Stryker's own whatever source/stryker-config.json sets.
const notCaughtStatuses = ['Survived', 'NoCoverage'];

// Reads one parsed report into caught and not-caught counts and the mutants no test caught.
function summarizeProject(report, root = repoRoot) {
  const counts = { caught: 0, notCaught: 0 };
  const misses = [];
  let fileCount = 0;

  for (const [filePath, file] of Object.entries(report.files)) {
    const scored = file.mutants.filter(mutant => [...caughtStatuses, ...notCaughtStatuses].includes(mutant.status));
    if (scored.length === 0) continue;
    fileCount++;
    const relative = path.relative(root, filePath).split(path.sep).join('/');
    for (const mutant of scored) {
      if (caughtStatuses.includes(mutant.status)) {
        counts.caught++;
        continue;
      }
      counts.notCaught++;
      misses.push({
        location: `${relative}:${mutant.location.start.line}`,
        mutation: mutant.mutatorName,
        replacement: mutant.replacement,
      });
    }
  }
  return { counts, misses, fileCount };
}

// Reads each `<project>=<report-path>` argument into that project's summary.
function readResults(reportArguments) {
  return reportArguments.map(argument => {
    const separator = argument.indexOf('=');
    const report = JSON.parse(fs.readFileSync(argument.slice(separator + 1), 'utf8'));
    return { project: argument.slice(0, separator), ...summarizeProject(report) };
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

// Formats a mutation score from the caught and not-caught counts, or a dash when nothing was scored.
function score(counts) {
  const total = counts.caught + counts.notCaught;
  if (total === 0) return '-';
  return `${(100 * counts.caught / total).toFixed(1)}%`;
}

// Builds the per-project score table.
function renderScores(results) {
  const rows = results.map(({ project, counts, fileCount }) =>
    [project, fileCount, counts.caught, counts.notCaught, score(counts)]);
  return table(['Project', 'Files', 'Caught', 'Not caught', 'Score'], rows);
}

// Builds the table of mutants no test caught, capped at maxRows.
function renderMisses(results) {
  const misses = results.flatMap(result => result.misses);
  const heading = [`### Not caught (${misses.length})`, ''];
  if (misses.length === 0) return [...heading, 'None.'];

  const rows = misses.slice(0, maxRows).map(miss => {
    const location = codeSpan(miss.location);
    const mutation = `${cell(miss.mutation)}: ${codeSpan(cell(miss.replacement))}`;
    return [location, mutation];
  });
  const lines = [...heading, ...table(['Location', 'Mutation'], rows)];
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
    'A mutant no test caught means no test fails when that line is broken, so a test should assert more, or the code is dead.';
  return [...title, intro, '', ...renderScores(results), '', ...renderMisses(results)].join('\n');
}

// Prints the summary for the project reports given on the command line.
function main() {
  console.log(render(readResults(process.argv.slice(2))));
}

if (require.main === module) main();

module.exports = { summarizeProject, render };
