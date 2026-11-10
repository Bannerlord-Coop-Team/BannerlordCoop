const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { test } = require('node:test');
const vm = require('node:vm');

const workflow = fs.readFileSync(path.join(__dirname, '../workflows/nightly-release.yml'), 'utf8').replace(/\r\n/g, '\n');
const published = {
  id: 41615577213, status: 'completed', conclusion: 'success',
  created_at: '2026-11-03T10:00:10Z', updated_at: '2026-11-03T10:04:30Z',
  head_sha: '2d6d2cb72ff85f87ded657dbf20c720d4d815262', event: 'workflow_dispatch',
};

function step(name) {
  const body = workflow.split(`      - name: ${name}\n`)[1]?.split(/\n      - name:|\n  [a-z]/)[0];
  assert.ok(body, `missing workflow step ${name}`);
  return body;
}

async function runScript(name, values = {}) {
  const script = step(name).match(/          script: \|\n([\s\S]*)/);
  assert.ok(script, `missing script in ${name}`);
  const outputs = {};
  const result = await vm.runInNewContext(`(async () => {\n${script[1]}\n})()`, {
    core: { setOutput: (key, value) => { outputs[key] = value; }, info() {} },
    process: { env: {} },
    context: { eventName: 'schedule', runId: 41644779460, repo: { owner: 'owner', repo: 'repo' } },
    ...values,
  });
  return { outputs, result };
}

function actionsApi(runs) {
  const calls = [];
  const jobIds = [];
  return { calls, jobIds, rest: { actions: {
    // The real status=success response omits this successful dispatch.
    listWorkflowRuns: async (args) => {
      calls.push(args);
      return { data: { workflow_runs: args.status ? runs.filter((run) => run.id !== published.id) : runs } };
    },
    listJobsForWorkflowRun: async ({ run_id: id }) => {
      jobIds.push(id);
      return { data: { jobs: [{ name: 'sign-and-publish', conclusion: 'success', steps: [
        { name: 'Publish client archive to private Patron R2', conclusion: 'success' },
      ] }] } };
    },
  } } };
}

test('the late 11-03 run does not republish over dispatch 41615577213', async () => {
  const github = actionsApi([published]);
  const { outputs } = await runScript('Keep a late scheduled nightly from overwriting a published one', {
    github, process: { env: { RELEASE_DATE: '2026-11-02' } },
  });
  assert.equal(outputs.should_publish, 'false');
  assert.equal(github.calls[0].status, undefined);
});

test('both listings ignore incomplete or unsuccessful runs locally', async () => {
  const runs = [
    { ...published, id: 1, status: 'in_progress', conclusion: null },
    { ...published, id: 2, conclusion: 'failure' },
    { ...published, id: 3, conclusion: 'cancelled' },
  ];
  const github = actionsApi(runs);
  const { outputs } = await runScript('Keep a late scheduled nightly from overwriting a published one', {
    github, process: { env: { RELEASE_DATE: '2026-11-02' } },
  });
  assert.equal(outputs.should_publish, 'true');
  const previous = await runScript('Find previous nightly commit', { github });
  assert.equal(previous.result, '');
  assert.deepEqual(github.jobIds, []);
});

test('the previous client lookup includes the successful dispatch omitted by status=success', async () => {
  const github = actionsApi([published]);
  const { result } = await runScript('Find previous nightly commit', { github });
  assert.equal(result, published.head_sha);
  assert.equal(github.calls[0].status, undefined);
  assert.deepEqual(github.jobIds, [published.id]);
});

async function resolveDate(now, requested = '') {
  class Clock extends Date {
    constructor(...args) { super(...(args.length ? args : [now])); }
  }
  return runScript('Resolve due nightly date', {
    Date: Clock, process: { env: { REQUESTED_RELEASE_DATE: requested } },
  });
}

test('the guard resolves calendar dates across spring forward and the Central cutoff', async () => {
  for (const [now, expected] of [
    ['2026-03-09T05:30:00Z', '2026-03-08'],
    ['2026-11-06T05:56:59Z', '2026-11-04'],
    ['2026-11-06T05:57:00Z', '2026-11-05'],
  ]) {
    const { outputs } = await resolveDate(now);
    assert.equal(outputs.release_date, expected, now);
  }
  assert.equal((await resolveDate('2026-03-09T05:30:00Z', '2026-03-08')).outputs.release_date, '2026-03-08');
  await assert.rejects(resolveDate('2026-03-09T05:30:00Z', '2026-03-07'), /currently due nightly/);
  await assert.rejects(resolveDate('2026-03-09T05:30:00Z', '03/08/2026'), /YYYY-MM-DD/);
});

test('build metadata uses the guard date even if the build crosses another cutoff', async () => {
  const { outputs } = await resolveDate('2026-03-09T05:30:00Z');
  assert.match(workflow, /release_date: \$\{\{ steps\.due\.outputs\.release_date \}\}/);
  const build = step('Build release metadata');
  assert.match(build, /RELEASE_DATE: \$\{\{ needs\.guard\.outputs\.release_date \}\}/);
  const shell = build.split('        run: |\n')[1].replace(/^ {10}/gm, '');
  const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'nightly-date-'));
  const output = path.join(temp, 'output');
  const bash = process.platform === 'win32' ? path.join(process.env.ProgramFiles, 'Git/bin/bash.exe') : 'bash';
  try {
    const result = spawnSync(bash, ['-c', `date() { echo 'build must not resolve the date again' >&2; return 1; }\ngit() { printf '%s\\n' "$GITHUB_SHA"; }\n${shell}`], {
      encoding: 'utf8', windowsHide: true,
      env: { ...process.env, RELEASE_DATE: outputs.release_date, PREVIOUS_SHA: '',
        GITHUB_SHA: published.head_sha, GITHUB_OUTPUT: output.replaceAll('\\', '/') },
    });
    assert.equal(result.status, 0, result.stderr || String(result.error));
    const values = Object.fromEntries(fs.readFileSync(output, 'utf8').trim().split('\n').map((line) => line.split('=')));
    assert.equal(values.release_date, '2026-03-08');
    assert.equal(values.file_name, 'Coop 03-08-2026.7z');
  } finally {
    fs.rmSync(temp, { recursive: true, force: true });
  }
});
