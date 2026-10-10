const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createDraft } = require('./tag-release.cjs');

function fixture(t) {
  const cwd = process.cwd();
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'tag-release-'));
  process.chdir(directory);
  t.after(() => { process.chdir(cwd); fs.rmSync(directory, { recursive: true, force: true }); });
  const identity = { tag: 'v0.2.0', release_id: 10, client_sha: 'a'.repeat(40), client_run_id: 20, client_run_attempt: 1 };
  const result = identity;
  const calls = [];
  const release = { id: 10, tag_name: 'v0.2.0', draft: true, prerelease: false, body: `notes\n<!-- release-coordination ${JSON.stringify(identity)} -->` };
  const github = {
    paginate: async (method) => method(),
    rest: {
      repos: {
        getRelease: async () => ({ data: release }),
        listReleaseAssets: async () => [],
        updateRelease: async args => {
          calls.push(['update', args]);
          Object.assign(release, args, { tag_name: args.tag_name || 'untagged-placeholder' });
          return { data: release };
        },
        createDispatchEvent: async args => { calls.push(['dispatch', args]); },
      },

    },
  };
  const context = { repo: { owner: 'Bannerlord-Coop-Team', repo: 'BannerlordCoop' }, payload: {} };
  return { github, context, calls, release, result };
}

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
  assert.equal(f.release.tag_name, f.result.tag);
  assert.equal(f.release.target_commitish, f.result.client_sha);
  assert.equal(f.calls[3][1].event_type, 'build_release');
  assert.equal(f.calls[3][1].client_payload.release_id, 10);
});

test('draft identity mismatch stops uploads and dispatch', async t => {
  const f = fixture(t);
  f.github.rest.repos.listReleases = async () => [f.release];
  f.github.rest.repos.updateRelease = async () => ({ data: { ...f.release, tag_name: 'untagged-placeholder' } });
  Object.assign(f.context, { ref: 'refs/tags/v0.2.0', sha: f.result.client_sha, runId: 20 });
  await assert.rejects(createDraft(f), /Draft release identity changed during update/);
  assert.deepEqual(f.calls, []);
});
