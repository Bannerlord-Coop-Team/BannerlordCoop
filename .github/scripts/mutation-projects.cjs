// Prints the unit test projects that reference one mutated project, as repo-relative csproj paths.
// Usage: node mutation-projects.cjs <repo-relative-csproj>
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '../..');
const solutionFilter = 'source/CoopUnitTests.slnf';
// Starts full in-process sessions, so running it once per mutant would take hours.
const excludedTestProjects = ['Coop.IntegrationTests'];

// Turns a Windows or POSIX relative path into a normalized POSIX path under the given folder.
function resolveUnder(directory, relativePath) {
  return path.posix.normalize(path.posix.join(directory, relativePath.replace(/\\/g, '/')));
}

// Returns the repo-relative paths a csproj's ProjectReference entries point at.
function projectReferences(csproj, csprojText) {
  const directory = path.posix.dirname(csproj);
  return [...csprojText.matchAll(/<ProjectReference\s+Include="([^"]+)"/g)]
    .map(match => resolveUnder(directory, match[1]));
}

// Returns the solution filter's test projects that reference the target csproj directly, minus the
// excluded ones. Throws when none does, because Stryker would otherwise run with no tests.
function testProjectsFor(target, readText) {
  const sourceDirectory = path.posix.dirname(solutionFilter);
  const testProjects = JSON.parse(readText(solutionFilter)).solution.projects
    .map(entry => resolveUnder(sourceDirectory, entry))
    .filter(csproj => !excludedTestProjects.includes(path.posix.basename(csproj, '.csproj')))
    .filter(csproj => projectReferences(csproj, readText(csproj)).includes(target));
  if (testProjects.length === 0) throw new Error(`No unit test project in ${solutionFilter} references ${target}`);
  return testProjects;
}

// Prints the test projects for the csproj given on the command line.
function main() {
  const [target] = process.argv.slice(2);
  if (!target) throw new Error('usage: node mutation-projects.cjs <repo-relative-csproj>');
  const readText = file => fs.readFileSync(path.join(repoRoot, file), 'utf8');
  for (const csproj of testProjectsFor(target, readText)) console.log(csproj);
}

if (require.main === module) main();

module.exports = { testProjectsFor };
