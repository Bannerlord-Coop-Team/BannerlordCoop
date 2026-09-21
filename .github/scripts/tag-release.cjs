const fs = require('node:fs');

const serverRepo = { owner: 'Bannerlord-Coop-Team', repo: 'BannerlordCoop.DedicatedServer' };
const identityPattern = /<!-- release-coordination (.*?) -->/;
const tagPattern = /^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/;
const identityKeys = ['tag', 'release_id', 'client_sha', 'client_run_id', 'client_run_attempt'];

function validate(payload) {
  if (!tagPattern.test(payload.tag)) {
    throw new Error('Expected a stable vN.N.N release tag');
  }
  if (!/^[a-f0-9]{40}$/.test(payload.client_sha)) throw new Error('Invalid client SHA');
  for (const key of ['release_id', 'client_run_id', 'client_run_attempt']) {
    if (!Number.isSafeInteger(payload[key]) || payload[key] <= 0) throw new Error(`Invalid ${key}`);
  }
}

async function createDraft({ github, context }) {
  const tag = context.ref.replace(/^refs\/tags\//, '');
  if (!tagPattern.test(tag)) throw new Error('Expected a stable vN.N.N release tag');
  const identity = {
    tag,
    client_sha: context.sha,
    client_run_id: context.runId,
    client_run_attempt: Number(process.env.GITHUB_RUN_ATTEMPT),
  };
  const releases = await github.paginate(github.rest.repos.listReleases, { ...context.repo, per_page: 100 });
  let release = releases.find(item => item.tag_name === tag);
  if (release && !release.draft) throw new Error('This release is already published');
  if (!release) {
    release = (await github.rest.repos.createRelease({
      ...context.repo, tag_name: tag, target_commitish: context.sha, name: tag, draft: true,
      generate_release_notes: true,
    })).data;
  }
  identity.release_id = release.id;
  const marker = `<!-- release-coordination ${JSON.stringify(identity)} -->`;
  const body = identityPattern.test(release.body || '')
    ? release.body.replace(identityPattern, marker)
    : `${release.body || ''}\n\n${marker}`;
  const { data: updated } = await github.rest.repos.updateRelease({
    ...context.repo, release_id: release.id, body, tag_name: tag, target_commitish: context.sha,
  });
  if (updated.tag_name !== tag || updated.target_commitish !== context.sha || !updated.draft) {
    throw new Error('Draft release identity changed during update');
  }
  const name = `BannerlordCoop-${tag}.zip`;
  const assets = await github.paginate(github.rest.repos.listReleaseAssets, { ...context.repo, release_id: release.id });
  const existing = assets.find(asset => asset.name === name);
  if (existing) await github.rest.repos.deleteReleaseAsset({ ...context.repo, asset_id: existing.id });
  await github.rest.repos.uploadReleaseAsset({
    ...context.repo, release_id: release.id, name, data: fs.readFileSync(name),
    headers: { 'content-type': 'application/zip' },
  });
  await github.rest.repos.createDispatchEvent({ ...serverRepo, event_type: 'build_release', client_payload: identity });
}

async function verifyServerRun({ github, context }) {
  const payload = context.payload.client_payload;
  validate(payload);
  for (const key of ['server_run_id', 'server_run_attempt']) {
    if (!Number.isSafeInteger(payload[key]) || payload[key] <= 0) throw new Error(`Invalid ${key}`);
  }
  const { data: run } = await github.rest.actions.getWorkflowRun({ ...serverRepo, run_id: payload.server_run_id });
  if (run.path !== '.github/workflows/release.yml' || run.event !== 'repository_dispatch'
      || !((run.status === 'completed' && run.conclusion === 'success')
        || (run.status === 'in_progress' && run.conclusion === null))
      || run.run_attempt !== payload.server_run_attempt) {
    throw new Error('Server release run has not completed successfully at the requested attempt');
  }
  if (run.status === 'in_progress') {
    const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRun, {
      ...serverRepo, run_id: payload.server_run_id, filter: 'all', per_page: 100,
    });
    jobs.sort((a, b) => b.id - a.id);
    for (const name of ['Windows package and R2 upload', 'Linux image and boot test']) {
      const job = jobs.find(item => item.name === name);
      if (!job || job.status !== 'completed' || job.conclusion !== 'success') {
        throw new Error(`Server release job has not succeeded: ${name}`);
      }
    }
  }
}

async function publish({ github, context }) {
  const payload = context.payload.client_payload;
  await verifyServerRun({ github, context });
  const result = JSON.parse(fs.readFileSync('release-result/release-result.json', 'utf8'));
  for (const key of [...identityKeys, 'server_run_id', 'server_run_attempt', 'image_digest']) {
    if (result[key] !== payload[key]) throw new Error(`Server result mismatch: ${key}`);
  }
  if (!/^sha256:[a-f0-9]{64}$/.test(result.image_digest)) throw new Error('Invalid image digest');
  const { data: release } = await github.rest.repos.getRelease({ ...context.repo, release_id: payload.release_id });
  const marker = identityPattern.exec(release.body || '');
  if (!marker || release.tag_name !== payload.tag || release.prerelease) throw new Error('Release identity mismatch');
  const identity = JSON.parse(marker[1]);
  for (const key of identityKeys) {
    if (identity[key] !== payload[key]) throw new Error(`Stale release callback: ${key}`);
  }
  const { data: commit } = await github.rest.repos.getCommit({ ...context.repo, ref: payload.tag });
  if (commit.sha !== payload.client_sha) throw new Error('Release tag has moved');
  const { data: clientRun } = await github.rest.actions.getWorkflowRun({ ...context.repo, run_id: payload.client_run_id });
  if (clientRun.path !== '.github/workflows/release.yml' || clientRun.event !== 'push'
      || clientRun.head_sha !== payload.client_sha || clientRun.status !== 'completed'
      || clientRun.conclusion !== 'success' || clientRun.run_attempt !== payload.client_run_attempt) {
    throw new Error('Client release run has not completed successfully at the requested attempt');
  }
  const assets = await github.paginate(github.rest.repos.listReleaseAssets, { ...context.repo, release_id: release.id });
  for (const [name, checksum] of [
    [`BannerlordCoop-${payload.tag}.zip`, result.client_asset_sha256],
    ['manifest.json', result.manifest_sha256],
  ]) {
    const asset = assets.find(item => item.name === name);
    if (!asset || asset.state !== 'uploaded' || asset.size <= 0) throw new Error(`Missing release asset: ${name}`);
    if (!/^[a-f0-9]{64}$/.test(checksum)) throw new Error(`Missing checksum: ${name}`);
    if (asset.digest !== `sha256:${checksum}`) throw new Error(`Release asset checksum mismatch: ${name}`);
  }
  const windows = result.windows;
  const client = result.client;
  const name = `BannerlordCoop-DedicatedServer-Win64-${payload.tag}.7z`;
  for (const [download, expectedName] of [[windows, name], [client, `BannerlordCoop-${payload.tag}.zip`]]) {
    if (!download || download.name !== expectedName || !/^[a-f0-9]{64}$/.test(download.sha256)
        || !Number.isSafeInteger(download.bytes) || download.bytes <= 0
        || download.url !== `https://pub-c80efa34191141fd803f8508e025f726.r2.dev/release/${payload.tag}/${download.sha256}/${expectedName}`) {
      throw new Error('Invalid R2 release download');
    }
  }
  if (client.sha256 !== result.client_asset_sha256) throw new Error('R2 client checksum mismatch');
  if (release.draft) {
    await github.rest.repos.updateRelease({
      ...context.repo, release_id: release.id, tag_name: payload.tag, target_commitish: payload.client_sha,
      draft: false, make_latest: 'legacy',
      body: `${release.body}

## Client
[Download ${client.name}](${client.url})

Size: ${client.bytes} bytes. SHA-256: \`${client.sha256}\`.

## Windows dedicated server
[Download ${name}](${windows.url})

Size: ${windows.bytes} bytes. SHA-256: \`${windows.sha256}\`.`,
    });
  }
  await github.rest.repos.createDispatchEvent({ ...serverRepo, event_type: 'promote_stable', client_payload: payload });
}

module.exports = { createDraft, verifyServerRun, publish };
