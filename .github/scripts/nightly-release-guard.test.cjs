const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {
  belongsToReleaseWindow,
  dueReleaseDate,
  findPreviousPublishedSha,
  findPublishedRun,
  isCutoffOpen,
  listRunsNewestFirst,
  resolveReleaseDate,
  shouldPublish,
} = require('./nightly-release-guard.cjs');

const repo = { owner: 'owner', repo: 'repo' };
const NOW = new Date('2026-11-03T13:29:00Z');
const noLog = () => {};

// Builds one workflow run in the shape the Actions API returns.
function run(id, overrides = {}) {
  return {
    id,
    event: 'workflow_dispatch',
    head_branch: 'development',
    head_sha: 'a'.repeat(40),
    status: 'completed',
    conclusion: 'success',
    created_at: '2026-11-03T10:00:10Z',
    updated_at: '2026-11-03T10:04:30Z',
    html_url: `https://example.test/runs/${id}`,
    ...overrides,
  };
}

// Fakes the Actions API so a status-filtered listing omits hiddenRunIds, as GitHub was seen doing.
function actionsApi(runs, { hiddenRunIds = [], jobs = {} } = {}) {
  const calls = [];
  return {
    calls,
    rest: {
      actions: {
        listWorkflowRuns: async (arguments_) => {
          calls.push(arguments_);
          const visible = arguments_.status
            ? runs.filter((item) => !hiddenRunIds.includes(item.id))
            : runs;
          const start = (arguments_.page - 1) * arguments_.per_page;
          return { data: { workflow_runs: visible.slice(start, start + arguments_.per_page) } };
        },
        listJobsForWorkflowRun: async ({ run_id: runId }) => ({ data: { jobs: jobs[runId] ?? [] } }),
      },
    },
  };
}

// Builds the sign-and-publish job a run reports, with its handoff step in the given state.
function publishJob(conclusion, stepConclusion) {
  return {
    name: 'sign-and-publish',
    conclusion,
    steps: [{ name: 'Publish dedicated-server client input', conclusion: stepConclusion }],
  };
}

test('the due date is the night whose cutoff most recently passed', () => {
  assert.equal(dueReleaseDate(new Date('2026-11-03T10:00:00Z')), '2026-11-02');
  assert.equal(dueReleaseDate(new Date('2026-11-04T05:56:00Z')), '2026-11-02');
  assert.equal(dueReleaseDate(new Date('2026-11-04T05:57:00Z')), '2026-11-03');
});

test('the due date stays correct across both DST transitions', () => {
  // Spring forward: subtracting 24 hours at 00:30 on Mar 9 would land on Mar 7.
  assert.equal(dueReleaseDate(new Date('2026-03-09T05:30:00Z')), '2026-03-08');
  assert.equal(dueReleaseDate(new Date('2026-03-08T07:30:00Z')), '2026-03-07');
  // Fall back: the cutoff moves from 04:57 UTC to 05:57 UTC.
  assert.equal(dueReleaseDate(new Date('2026-10-31T04:57:00Z')), '2026-10-30');
  assert.equal(dueReleaseDate(new Date('2026-11-01T04:57:00Z')), '2026-10-31');
  assert.equal(dueReleaseDate(new Date('2026-11-02T05:56:00Z')), '2026-10-31');
  assert.equal(dueReleaseDate(new Date('2026-11-02T05:57:00Z')), '2026-11-01');
});

test('the paired UTC cron entries open at the Central cutoff', () => {
  // The winter entry is one hour early while Central Standard Time is active.
  assert.equal(isCutoffOpen(new Date('2026-11-06T04:57:00Z'), '57 4 * * *'), false);
  assert.equal(isCutoffOpen(new Date('2026-11-06T05:57:00Z'), '57 5 * * *'), true);
  // The same two entries are both safe during Central Daylight Time.
  assert.equal(isCutoffOpen(new Date('2026-07-01T04:57:00Z'), '57 4 * * *'), true);
  assert.equal(isCutoffOpen(new Date('2026-07-01T05:57:00Z'), '57 5 * * *'), true);
});

test('a requested date must be well formed and currently due', () => {
  assert.equal(resolveReleaseDate(NOW, ''), '2026-11-02');
  assert.equal(resolveReleaseDate(NOW, '2026-11-02'), '2026-11-02');
  assert.throws(() => resolveReleaseDate(NOW, '11/02/2026'), /YYYY-MM-DD/);
  assert.throws(() => resolveReleaseDate(NOW, '2026-11-01'), /currently due/);
});

test('the release window follows the Central cutoff on each side of DST', () => {
  assert.equal(belongsToReleaseWindow('2026-11-03T10:00:10Z', '2026-11-02'), true);
  assert.equal(belongsToReleaseWindow('2026-11-03T05:56:59Z', '2026-11-02'), false);
  assert.equal(belongsToReleaseWindow('2026-11-03T05:57:00Z', '2026-11-02'), true);
  assert.equal(belongsToReleaseWindow('2026-11-04T05:56:59Z', '2026-11-02'), true);
  assert.equal(belongsToReleaseWindow('2026-11-04T05:57:00Z', '2026-11-02'), false);
  assert.equal(belongsToReleaseWindow('2026-03-08T05:57:00Z', '2026-03-07'), true);
  assert.equal(belongsToReleaseWindow('2026-03-09T04:57:00Z', '2026-03-07'), false);
  assert.equal(belongsToReleaseWindow('not a time', '2026-11-02'), false);
});

test('a late cron sees a success that the status filter would have omitted', async () => {
  const github = actionsApi([run(2, { event: 'schedule' }), run(1)], { hiddenRunIds: [1] });
  const logged = [];
  const publish = await shouldPublish({
    github,
    repo,
    eventName: 'schedule',
    schedule: '57 5 * * *',
    currentRunId: 2,
    releaseDate: '2026-11-02',
    now: new Date('2026-11-03T06:00:00Z'),
    log: (line) => logged.push(line),
  });
  assert.equal(publish, false);
  assert.match(logged[0], /runs\/1/);
  assert.ok(github.calls.every((call) => call.status === undefined && call.branch === undefined));
});

test('a cron with no successful run in its window builds', async () => {
  const failed = run(1, { conclusion: 'failure' });
  const inProgress = run(2, { status: 'in_progress', conclusion: null });
  const otherBranch = run(3, { head_branch: 'feature' });
  const yesterday = run(4, { created_at: '2026-11-02T10:00:10Z', updated_at: '2026-11-02T10:04:00Z' });
  const publish = await shouldPublish({
    github: actionsApi([run(5, { event: 'schedule' }), failed, inProgress, otherBranch, yesterday]),
    repo,
    eventName: 'schedule',
    schedule: '57 5 * * *',
    currentRunId: 5,
    releaseDate: '2026-11-02',
    now: new Date('2026-11-03T06:00:00Z'),
    log: noLog,
  });
  assert.equal(publish, true);
});

test('the early DST fallback cron waits instead of publishing before cutoff', async () => {
  const calls = [];
  const logged = [];
  const publish = await shouldPublish({
    github: actionsApi([run(1, { event: 'schedule' })]),
    repo,
    eventName: 'schedule',
    schedule: '57 4 * * *',
    currentRunId: 2,
    releaseDate: '2026-11-05',
    now: new Date('2026-11-06T04:57:00Z'),
    log: (line) => { logged.push(line); calls.push(line); },
  });
  assert.equal(publish, false);
  assert.match(logged[0], /before.*cutoff/);
});

test('a dispatch always builds and never lists runs', async () => {
  const github = actionsApi([run(1)]);
  const publish = await shouldPublish({
    github,
    repo,
    eventName: 'workflow_dispatch',
    currentRunId: 2,
    releaseDate: '2026-11-02',
    log: noLog,
  });
  assert.equal(publish, true);
  assert.equal(github.calls.length, 0);
});

test('a re-run counts through updated_at even though it was created long ago', () => {
  const rerun = run(1, { created_at: '2026-10-20T10:00:00Z', updated_at: '2026-11-03T10:00:00Z' });
  assert.equal(findPublishedRun([rerun], '2026-11-02', 2), rerun);
});

test('the current run never counts as already published', () => {
  assert.equal(findPublishedRun([run(1)], '2026-11-02', 1), undefined);
});

test('listing pages past a full page until runs are older than the bound', async () => {
  const runs = Array.from({ length: 150 }, (_, index) => run(150 - index));
  const github = actionsApi(runs);
  const listed = await listRunsNewestFirst(github, repo, new Date('2026-10-01T00:00:00Z'));
  assert.equal(listed.length, 150);
  assert.deepEqual(github.calls.map((call) => call.page), [1, 2]);
});

test('listing stops once a full page reaches runs older than the bound', async () => {
  const runs = Array.from({ length: 300 }, (_, index) =>
    run(300 - index, { created_at: '2026-01-01T00:00:00Z' }));
  const github = actionsApi(runs);
  const listed = await listRunsNewestFirst(github, repo, new Date('2026-10-01T00:00:00Z'));
  assert.equal(listed.length, 100);
});

test('listing refuses a partial history rather than silently missing runs', async () => {
  const runs = Array.from({ length: 600 }, (_, index) => run(600 - index));
  await assert.rejects(
    listRunsNewestFirst(actionsApi(runs), repo, new Date('2026-10-01T00:00:00Z')),
    /page bound/,
  );
});

test('the previous commit comes from a run that truly published', async () => {
  const skipped = run(4, { head_sha: 'b'.repeat(40) });
  const cleanupFailed = run(3, { head_sha: 'c'.repeat(40), conclusion: 'failure' });
  const older = run(2, { head_sha: 'd'.repeat(40) });
  const github = actionsApi([run(5), skipped, cleanupFailed, older], {
    jobs: {
      4: [publishJob('success', 'skipped')],
      3: [publishJob('success', 'success')],
      2: [publishJob('success', 'success')],
    },
  });
  const sha = await findPreviousPublishedSha({ github, repo, currentRunId: 5, now: NOW, log: noLog });
  assert.equal(sha, 'c'.repeat(40));
});

test('no published run leaves the previous commit empty', async () => {
  const logged = [];
  const github = actionsApi([run(2), run(1)], { jobs: { 1: [publishJob('failure', 'skipped')] } });
  const sha = await findPreviousPublishedSha({
    github,
    repo,
    currentRunId: 2,
    now: NOW,
    log: (line) => logged.push(line),
  });
  assert.equal(sha, '');
  assert.equal(logged.length, 1);
});

test('the workflow uses UTC cron entries and carries the guard date into the build', () => {
  const workflow = fs.readFileSync(path.join(__dirname, '../workflows/nightly-release.yml'), 'utf8');
  assert.doesNotMatch(workflow, /status:\s*'success'/);
  assert.match(workflow, /cron: '57 4 \* \* \*'/);
  assert.match(workflow, /cron: '57 5 \* \* \*'/);
  assert.doesNotMatch(workflow, /timezone:\s*America\/Chicago/);
  assert.match(workflow, /release_date: \$\{\{ steps\.decide\.outputs\.release_date \}\}/);
  assert.match(workflow, /RELEASE_DATE: \$\{\{ needs\.guard\.outputs\.release_date \}\}/);
});
