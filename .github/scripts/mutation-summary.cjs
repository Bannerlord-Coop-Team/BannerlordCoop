// Turns Stryker JSON reports into a markdown summary of the mutants no unit test caught.
// Usage: node mutation-summary.cjs [<project>=<mutation-report.json>...]
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '../..');
const maxRows = 100;
const maxCellLength = 80;
const caughtStatuses = ['Killed', 'Timeout'];
// NoCoverage only appears when coverage analysis is on. Counting it as not caught matches Stryker's
// own score, so the summary stays right if source/stryker-config.json turns coverage analysis back on.
const notCaughtStatuses = ['Survived', 'NoCoverage'];
const scoredStatuses = [...caughtStatuses, ...notCaughtStatuses];

// Reads one parsed report into the caught count and the mutants no test caught.
function summarizeProject(report, root = repoRoot) {
  let caught = 0;
  const misses = [];
  let fileCount = 0;

  for (const [filePath, file] of Object.entries(report.files)) {
    const scored = file.mutants.filter(mutant => scoredStatuses.includes(mutant.status));
    if (scored.length === 0) continue;
    fileCount++;
    const relative = path.relative(root, filePath).split(path.sep).join('/');
    for (const mutant of scored) {
      if (caughtStatuses.includes(mutant.status)) {
        caught++;
        continue;
      }
      misses.push({
        location: `${relative}:${mutant.location.start.line}`,
        mutation: mutant.mutatorName,
        replacement: mutant.replacement,
      });
    }
  }
  return { caught, misses, fileCount };
}

// Reads each `<project>=<report-path>` argument, split at the first `=`, into that project's
// summary, loading the report through the given reader.
function readResults(reportArguments, readJson, root = repoRoot) {
  return reportArguments.map(argument => {
    const separator = argument.indexOf('=');
    const project = argument.slice(0, separator);
    const reportPath = argument.slice(separator + 1);
    return { project, ...summarizeProject(readJson(reportPath), root) };
  });
}

// Escapes text for a markdown table cell and caps its length. Backticks become quotes so a
// value can't close the code span it is shown in.
function cell(text) {
  let flat = String(text).replace(/\s+/g, ' ').trim();
  if (flat.length > maxCellLength) flat = `${flat.slice(0, maxCellLength - 3)}...`;
  return flat.replace(/\|/g, '\\|').replace(/`/g, "'");
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
function score(caught, notCaught) {
  const total = caught + notCaught;
  if (total === 0) return '-';
  return `${(100 * caught / total).toFixed(1)}%`;
}

// Builds the per-project score table.
function renderScores(results) {
  const rows = results.map(({ project, caught, misses, fileCount }) =>
    [project, fileCount, caught, misses.length, score(caught, misses.length)]);
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
  const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8'));
  console.log(render(readResults(process.argv.slice(2), readJson)));
}

if (require.main === module) main();

module.exports = { readResults, render };
