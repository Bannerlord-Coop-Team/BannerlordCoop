const fs = require('node:fs');

const serverRepo = { owner: 'Bannerlord-Coop-Team', repo: 'BannerlordCoop.DedicatedServer' };
const identityPattern = /<!-- release-coordination (.*?) -->/;
const tagPattern = /^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/;
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

module.exports = { createDraft };
