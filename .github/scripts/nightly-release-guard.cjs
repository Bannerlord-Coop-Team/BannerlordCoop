// Decision logic for nightly-release.yml. Run listings stay unfiltered by status because
// the status filter can silently omit a completed run; conclusions are checked here instead.

const TIME_ZONE = 'America/Chicago';
const CUTOFF_MINUTES = (23 * 60) + 57;
const WORKFLOW_ID = 'nightly-release.yml';
const BRANCH = 'development';
const PUBLISH_JOB = 'sign-and-publish';
const PUBLISH_STEP = 'Publish dedicated-server client input';
const RELEASE_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const PAGE_SIZE = 100;
const MAX_PAGES = 5;
const MAX_PREVIOUS_CANDIDATES = 25;
// GitHub allows a re-run for 30 days, and a re-run keeps created_at but moves updated_at.
const RERUN_LOOKBACK_MS = 31 * 24 * 60 * 60 * 1000;

// Returns the America/Chicago calendar date and minute of day for one instant.
function centralClock(instant) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: TIME_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(instant);
  const values = new Map(parts.map((part) => [part.type, part.value]));
  return {
    date: `${values.get('year')}-${values.get('month')}-${values.get('day')}`,
    minutes: (Number(values.get('hour')) * 60) + Number(values.get('minute')),
  };
}

// Moves a YYYY-MM-DD calendar date by whole days, independent of any DST transition.
function shiftDate(date, days) {
  const shifted = new Date(`${date}T12:00:00.000Z`);
  shifted.setUTCDate(shifted.getUTCDate() + days);
  return shifted.toISOString().slice(0, 10);
}

// Returns the nightly date whose 11:57 PM Central cutoff most recently passed at this instant.
function dueReleaseDate(instant) {
  const clock = centralClock(instant);
  return clock.minutes >= CUTOFF_MINUTES ? clock.date : shiftDate(clock.date, -1);
}

// The two UTC cron entries cover both Central Standard and Daylight time. In
// winter the daylight-time entry arrives at 10:57 PM Central, so it must wait
// for the actual cutoff. A delayed run after midnight is still valid.
function isCutoffOpen(instant, schedule) {
  const clock = centralClock(instant);
  const earlyWinterEntry = schedule === '57 4 * * *';
  const beforeCutoffOnSameDay = clock.minutes >= (22 * 60 + 57) && clock.minutes < CUTOFF_MINUTES;
  return !(earlyWinterEntry && beforeCutoffOnSameDay);
}

// Returns the due nightly date, refusing a requested date that is malformed or not due.
function resolveReleaseDate(instant, requested) {
  const due = dueReleaseDate(instant);
  if (!requested) return due;
  if (!RELEASE_DATE_PATTERN.test(requested)) throw new Error('release_date must use YYYY-MM-DD');
  if (requested !== due) throw new Error('release_date must be the currently due nightly');
  return due;
}

// Tells whether a timestamp falls between this nightly's cutoff and the next night's cutoff.
function belongsToReleaseWindow(timestamp, releaseDate) {
  const instant = new Date(timestamp);
  if (!Number.isFinite(instant.getTime())) return false;
  const clock = centralClock(instant);
  if (clock.date === releaseDate) return clock.minutes >= CUTOFF_MINUTES;
  return clock.date === shiftDate(releaseDate, 1) && clock.minutes < CUTOFF_MINUTES;
}

// Lists this workflow's runs newest first without a status filter, until the runs are older than
// notBefore. Throws rather than returning a list that could silently be missing runs.
async function listRunsNewestFirst(github, repo, notBefore) {
  const runs = [];
  for (let page = 1; page <= MAX_PAGES; page += 1) {
    const response = await github.rest.actions.listWorkflowRuns({
      ...repo,
      workflow_id: WORKFLOW_ID,
      per_page: PAGE_SIZE,
      page,
    });
    const batch = response.data.workflow_runs;
    runs.push(...batch);
    const oldest = batch[batch.length - 1];
    if (batch.length < PAGE_SIZE) return runs;
    if (new Date(oldest.created_at).getTime() < notBefore.getTime()) return runs;
  }
  throw new Error(`Nightly run history exceeded the ${MAX_PAGES} page bound`);
}

// Tells whether a run finished successfully on the nightly branch, from its own fields.
function isSuccessfulNightlyRun(run) {
  return run.head_branch === BRANCH && run.status === 'completed' && run.conclusion === 'success';
}

// Finds another successful run that already belongs to this nightly's release window.
function findPublishedRun(runs, releaseDate, currentRunId) {
  return runs.find((run) =>
    run.id !== currentRunId
    && isSuccessfulNightlyRun(run)
    && (belongsToReleaseWindow(run.created_at, releaseDate)
      || belongsToReleaseWindow(run.updated_at, releaseDate)));
}

// Decides whether this run may build at all; only a scheduled run can be refused, because a
// dispatch deliberately replaces the nightly. The storage check before publishing is the backstop.
async function shouldPublish({ github, repo, eventName, schedule, currentRunId, releaseDate, now = new Date(), log }) {
  if (eventName !== 'schedule') return true;
  if (!isCutoffOpen(now, schedule)) {
    log(`Scheduled nightly arrived before the ${CUTOFF_MINUTES} Central cutoff; waiting for the DST-safe cron entry.`);
    return false;
  }
  const windowStart = new Date(`${releaseDate}T00:00:00.000Z`);
  const notBefore = new Date(windowStart.getTime() - RERUN_LOOKBACK_MS);
  const runs = await listRunsNewestFirst(github, repo, notBefore);
  const published = findPublishedRun(runs, releaseDate, currentRunId);
  if (!published) return true;
  log(`Scheduled nightly for ${releaseDate} will not overwrite ${published.html_url} (${published.event}).`);
  return false;
}

// Tells whether a finished run really published a client, from its job and step conclusions.
function publishedClient(jobs) {
  return jobs.some((job) =>
    job.name === PUBLISH_JOB
    && job.conclusion === 'success'
    && (job.steps ?? []).some((step) => step.name === PUBLISH_STEP && step.conclusion === 'success'));
}

// Returns the commit of the newest earlier run that published a client, or '' when none is found,
// which only omits the changelog base. A run's conclusion is not used: cleanup can fail after publishing.
async function findPreviousPublishedSha({ github, repo, currentRunId, now, log }) {
  const notBefore = new Date(now.getTime() - RERUN_LOOKBACK_MS);
  const runs = await listRunsNewestFirst(github, repo, notBefore);
  const candidates = runs
    .filter((run) => run.id !== currentRunId && run.head_branch === BRANCH && run.status === 'completed')
    .slice(0, MAX_PREVIOUS_CANDIDATES);
  for (const run of candidates) {
    const jobs = await github.rest.actions.listJobsForWorkflowRun({ ...repo, run_id: run.id, per_page: 20 });
    if (publishedClient(jobs.data.jobs)) return run.head_sha;
  }
  log(`No published nightly was found in the last ${candidates.length} finished runs.`);
  return '';
}

module.exports = {
  belongsToReleaseWindow,
  dueReleaseDate,
  isCutoffOpen,
  findPreviousPublishedSha,
  findPublishedRun,
  listRunsNewestFirst,
  resolveReleaseDate,
  shouldPublish,
};
