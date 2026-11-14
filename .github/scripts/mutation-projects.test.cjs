const { test } = require('node:test');
const assert = require('node:assert/strict');
const { testProjectsFor } = require('./mutation-projects.cjs');

// The solution filter lists paths relative to source/, with JSON-escaped backslashes.
const slnf = JSON.stringify({
  solution: {
    path: 'Coop.sln',
    projects: ['Common.Tests\\Common.Tests.csproj', 'Coop.Tests\\Coop.Tests.csproj', 'Coop.IntegrationTests\\Coop.IntegrationTests.csproj', 'Other.Tests\\Other.Tests.csproj'],
  },
});

const files = {
  'source/CoopUnitTests.slnf': slnf,
  'source/Common.Tests/Common.Tests.csproj': '<ProjectReference Include="..\\Common\\Common.csproj" />',
  'source/Coop.Tests/Coop.Tests.csproj': '<ProjectReference Include="../Common/Common.csproj">\n</ProjectReference>',
  'source/Coop.IntegrationTests/Coop.IntegrationTests.csproj': '<ProjectReference Include="..\\Common\\Common.csproj" />',
  'source/Other.Tests/Other.Tests.csproj': '<ProjectReference Include="..\\Common.Extra\\Common.Extra.csproj" />',
};

test('finds the test projects that reference the project with either slash, minus the excluded one', () => {
  assert.deepEqual(testProjectsFor('source/Common/Common.csproj', file => files[file]), [
    'source/Common.Tests/Common.Tests.csproj',
    'source/Coop.Tests/Coop.Tests.csproj',
  ]);
});
