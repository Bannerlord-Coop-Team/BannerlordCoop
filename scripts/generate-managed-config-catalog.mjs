#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sourcePath = path.join(root, 'source', 'GameInterface', 'Configuration', 'ModConfigData.cs');
const defaultsPath = path.join(root, 'deploy', 'mod-config.default.json');
const outputPath = path.join(root, 'deploy', 'managed-config-catalog.json');
const check = process.argv.includes('--check');

const OPTION_METADATA = Object.freeze({
  voiceEnabled: { stable: true }, battleSize: { stable: true, min: 200, max: 12_000 }, fastForwardEnabled: { stable: true },
  autoPauseEnabled: { stable: true }, allowClientTimeControls: { stable: false }, clientsCanUseCheats: { stable: true },
  goldFoodInfluenceChangeInSettlements: { stable: true }, goldFoodInfluenceChangeInBattles: { stable: true },
  goldFoodInfluenceChangeForDisconnectedPlayers: { stable: true }, blockAiWarDeclarationsOnOfflinePlayers: { stable: true },
  playerBattleAiJoinWindowHours: { stable: true, min: 0, max: 8_760 }, postBattleAiAmnestySeconds: { stable: false, min: 0, max: 86_400 },
  speedLimitWhilePlayersInBattle: { stable: true }, ensureUnaffiliatedWanderers: { stable: true }, wandererLimit: { stable: true, min: 0, max: 1_000 },
  spawnRateWanderers: { stable: true, min: 0, max: 100 }, wandererLimitScalesWithPlayers: { stable: true },
  playerKingdomClanTierRequired: { stable: true, min: 0, max: 6 }, coopClansEnabled: { stable: true },
  smithingStaminaRecoveryOutsideSettlements: { stable: true }, smithingStaminaRecoveryMultiplier: { stable: true, min: 0, max: 100 },
  maximumLootersMultiplier: { stable: true, min: 0, max: 100 }, looterPartySizeMultiplier: { stable: true, min: 0, max: 100 },
  lordDefectionRetries: { stable: true }, enableHeroExecutions: { stable: true }, enablePlayerClanMemberExecutions: { stable: true },
  enablePlayerExecutions: { stable: true }, playerWoundedBattleEntry: { stable: true }, showPlayerNameplates: { stable: true },
  kingdomDecisionVoteTimeoutSeconds: { stable: true, min: 1, max: 86_400 }, clansAfterKingdomDestruction: { stable: false },
  preferredBattleHosts: { stable: false, maxItems: 32, itemPattern: '^[0-9]{17}$', uniqueItems: true },
  pvpBlockLeave: { stable: false }, pvpResponseTimerSeconds: { stable: false, min: 0, max: 86_400, forbiddenRanges: [[1, 9]] },
  warSails: { stable: false },
});

const source = await readFile(sourcePath, 'utf8');
const defaultsText = await readFile(defaultsPath, 'utf8');
const defaults = JSON.parse(defaultsText.replace(/^\uFEFF/u, '').replaceAll(/\/\/.*$/gmu, '').replaceAll(/,\s*([}\]])/gu, '$1'));
const block = source.match(/public sealed class ModOptionsData\s*\{(?<body>[\s\S]*?)\n\}/u)?.groups?.body;
if (block === undefined) throw new Error('ModOptionsData was not found');

const options = [];
for (const match of block.matchAll(/^\s*public\s+(?<type>bool|int|float|double|List<string>)\??\s+(?<name>[A-Z][A-Za-z0-9]*)\s*\{\s*get;\s*set;\s*\}/gmu)) {
  const key = match.groups.name[0].toLowerCase() + match.groups.name.slice(1);
  const type = match.groups.type === 'List<string>' ? 'steamIdList'
    : match.groups.type === 'bool' ? 'boolean'
      : match.groups.type === 'int' ? 'integer' : 'number';
  const value = defaults.modOptions?.[key];
  if (value === undefined) throw new Error(`Default is missing for ${key}`);
  options.push({ key, type, ...OPTION_METADATA[key], default: value });
}
for (const match of block.matchAll(/^\s*public\s+(?<type>[A-Z][A-Za-z0-9_]*)\??\s+(?<name>[A-Z][A-Za-z0-9]*)\s*\{\s*get;\s*set;\s*\}/gmu)) {
  if (options.some((option) => option.key === match.groups.name[0].toLowerCase() + match.groups.name.slice(1))) continue;
  const key = match.groups.name[0].toLowerCase() + match.groups.name.slice(1);
  const value = defaults.modOptions?.[key];
  if (value === undefined) throw new Error(`Default is missing for ${key}`);
  options.push({ key, type: 'enum', values: key === 'goldFoodInfluenceChangeInBattles' ? ['Disabled', 'OneDayMax', 'Enabled'] : key === 'lordDefectionRetries' ? ['Vanilla', 'NeverExpire', 'AlwaysRetry'] : key === 'clansAfterKingdomDestruction' ? ['Vanilla', 'KeepClans'] : undefined, ...OPTION_METADATA[key], default: value });
}
if (options.some((option) => option.values === undefined && option.type === 'enum')) throw new Error(`An enum option needs an explicit value catalog: ${options.filter((option) => option.values === undefined && option.type === 'enum').map((option) => option.key).join(', ')}`);
const normalized = options.map(({ key, type, values, default: value, ...metadata }) => ({
  key, type, ...metadata, ...(values === undefined ? {} : { values }), default: value,
}));
const digest = createHash('sha256').update(JSON.stringify(normalized)).digest('hex');
const catalog = { schemaVersion: 1, catalogRevision: digest, source: 'BannerlordCoop/source/GameInterface/Configuration/ModConfigData.cs', options: normalized };
const output = `${JSON.stringify(catalog, null, 2)}\n`;
if (check) {
  const current = await readFile(outputPath, 'utf8').catch(() => '');
  if (current.replaceAll('\r\n', '\n') !== output) throw new Error('managed-config-catalog.json is stale; run node scripts/generate-managed-config-catalog.mjs');
} else {
  await writeFile(outputPath, output, 'utf8');
}
