const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { createDraft, publish } = require('./tag-release.cjs');

function fixture(t) {
  const cwd = process.cwd();
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'tag-release-'));
  process.chdir(directory);
  t.after(() => { process.chdir(cwd); fs.rmSync(directory, { recursive: true, force: true }); });
  const identity = { tag: 'v0.2.0', release_id: 10, client_sha: 'a'.repeat(40), client_run_id: 20, client_run_attempt: 1 };
  const payload = { ...identity, server_run_id: 30, server_run_attempt: 1, image_digest: `sha256:${'b'.repeat(64)}` };
  const bytes = Buffer.from('packaged binaries');
  const checksum = crypto.createHash('sha256').update(bytes).digest('hex');
  const result = { ...payload, client_asset_sha256: checksum, server_asset_sha256: checksum };
  fs.mkdirSync('release-result');
  fs.writeFileSync('release-result/release-result.json', JSON.stringify(result));
  const release = { id: 10, tag_name: 'v0.2.0', draft: true, prerelease: false, body: `notes\n<!-- release-coordination ${JSON.stringify(identity)} -->` };
  const assets = ['BannerlordCoop-v0.2.0.zip', 'BannerlordCoop-DedicatedServer-Linux64-v0.2.0.tar.zst'].map((name, id) => ({ id, name, size: bytes.length, state: 'uploaded', digest: `sha256:${checksum}` }));
  const calls = [];
  const serverRun = { path: '.github/workflows/release.yml', event: 'repository_dispatch', status: 'completed', conclusion: 'success', run_attempt: 1 };
  const clientRun = { ...serverRun, event: 'push', head_sha: identity.client_sha };
  const github = {
    paginate: async (method) => method(),
    rest: {
      repos: {
        getRelease: async () => ({ data: release }),
        getCommit: async () => ({ data: { sha: identity.client_sha } }),
        listReleaseAssets: async () => assets,
        updateRelease: async args => { calls.push(['update', args]); },
        createDispatchEvent: async args => { calls.push(['dispatch', args]); },
      },
      actions: { getWorkflowRun: async args => ({ data: args.run_id === 30 ? serverRun : clientRun }) },
    },
  };
  const context = { repo: { owner: 'Bannerlord-Coop-Team', repo: 'BannerlordCoop' }, payload: { client_payload: payload } };
  return { github, context, calls, assets, release, serverRun, clientRun, result };
}

test('publishes only after verification, then requests a digest-bound promotion command', async t => {
  const f = fixture(t);
  await publish(f);
  assert.deepEqual(f.calls.map(call => call[0]), ['update', 'dispatch']);
  assert.equal(f.calls[0][1].draft, false);
  assert.equal(f.calls[1][1].event_type, 'promote_stable');
  assert.equal(f.calls[1][1].client_payload.image_digest, f.result.image_digest);
});

test('rejects failed runs, stale attempts, missing assets and changed bytes before publishing', async t => {
  const cases = [
    f => { f.serverRun.conclusion = 'failure'; },
    f => { f.serverRun.run_attempt = 2; },
    f => { f.clientRun.run_attempt = 2; },
    f => { f.assets.pop(); },
    f => { f.context.payload.client_payload.client_sha = 'c'.repeat(40); },
    f => { f.release.body = f.release.body.replace('"client_run_attempt":1', '"client_run_attempt":2'); },
    f => { f.assets[1].digest = `sha256:${'d'.repeat(64)}`; },
  ];
  for (const mutate of cases) {
    await t.test(mutate.toString(), async t => {
      const f = fixture(t);
      mutate(f);
      await assert.rejects(publish(f));
      assert.deepEqual(f.calls, []);
    });
  }
});

test('a repeated completion can retry promotion without publishing twice', async t => {
  const f = fixture(t);
  f.release.draft = false;
  await publish(f);
  assert.deepEqual(f.calls.map(call => call[0]), ['dispatch']);
});

test('draft creation uploads the client package before dispatch and never publishes', async t => {
  const f = fixture(t);
  const attempt = process.env.GITHUB_RUN_ATTEMPT;
  process.env.GITHUB_RUN_ATTEMPT = '1';
  t.after(() => { if (attempt === undefined) delete process.env.GITHUB_RUN_ATTEMPT; else process.env.GITHUB_RUN_ATTEMPT = attempt; });
  fs.writeFileSync('BannerlordCoop-v0.2.0.zip', 'client');
  f.github.rest.repos.listReleases = async () => [];
  f.github.rest.repos.listReleaseAssets = async () => [];
  f.github.rest.repos.createRelease = async args => { f.calls.push(['create', args]); return { data: f.release }; };
  f.github.rest.repos.uploadReleaseAsset = async args => { f.calls.push(['upload', args]); };
  Object.assign(f.context, { ref: 'refs/tags/v0.2.0', sha: f.result.client_sha, runId: 20 });
  await createDraft(f);
  assert.deepEqual(f.calls.map(call => call[0]), ['create', 'update', 'upload', 'dispatch']);
  assert.equal(f.calls[0][1].draft, true);
  assert.equal(f.calls[3][1].event_type, 'build_release');
  assert.equal(f.calls[3][1].client_payload.release_id, 10);
});
