import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';

const catalog = JSON.parse(await readFile(new URL('../deploy/managed-config-catalog.json', import.meta.url), 'utf8'));

test('generated catalog carries bounds, channel availability and list constraints', () => {
  const byKey = new Map(catalog.options.map(option => [option.key, option]));
  assert.deepEqual(byKey.get('clansAfterKingdomDestruction').values, ['Vanilla', 'KeepClans']);
  assert.equal(byKey.get('clansAfterKingdomDestruction').stable, false);
  assert.deepEqual(byKey.get('kingdomDecisionVoteTimeoutSeconds').min, 1);
  assert.deepEqual(byKey.get('pvpResponseTimerSeconds').forbiddenRanges, [[1, 9]]);
  assert.deepEqual(byKey.get('preferredBattleHosts').maxItems, 32);
  assert.equal(byKey.get('preferredBattleHosts').uniqueItems, true);
  assert.equal(byKey.get('preferredBattleHosts').itemPattern, '^[0-9]{17}$');
  for (const option of catalog.options) assert.notEqual(option.default, undefined, `${option.key} has no default`);
});

test('generator check rejects stale output', async () => {
  const result = await new Promise(resolve => {
    const child = spawn(process.execPath, ['scripts/generate-managed-config-catalog.mjs', '--check'], { stdio: 'ignore' });
    child.on('close', code => resolve(code));
  });
  assert.equal(result, 0);
});
