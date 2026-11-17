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
  options.push({ key, type, default: value });
}
for (const match of block.matchAll(/^\s*public\s+(?<type>[A-Z][A-Za-z0-9_]*)\??\s+(?<name>[A-Z][A-Za-z0-9]*)\s*\{\s*get;\s*set;\s*\}/gmu)) {
  if (options.some((option) => option.key === match.groups.name[0].toLowerCase() + match.groups.name.slice(1))) continue;
  const key = match.groups.name[0].toLowerCase() + match.groups.name.slice(1);
  const value = defaults.modOptions?.[key];
  if (value === undefined) throw new Error(`Default is missing for ${key}`);
  options.push({ key, type: 'enum', values: key === 'goldFoodInfluenceChangeInBattles' ? ['Disabled', 'OneDayMax', 'Enabled'] : key === 'lordDefectionRetries' ? ['Vanilla', 'NeverExpire', 'AlwaysRetry'] : key === 'clansAfterKingdomDestruction' ? ['Vanilla', 'KeepClans'] : undefined, default: value });
}
if (options.some((option) => option.values === undefined && option.type === 'enum')) throw new Error(`An enum option needs an explicit value catalog: ${options.filter((option) => option.values === undefined && option.type === 'enum').map((option) => option.key).join(', ')}`);
const normalized = options.map(({ key, type, values, default: value }) => ({ key, type, ...(values === undefined ? {} : { values }), default: value }));
const digest = createHash('sha256').update(JSON.stringify(normalized)).digest('hex');
const catalog = { schemaVersion: 1, catalogRevision: digest, source: 'BannerlordCoop/source/GameInterface/Configuration/ModConfigData.cs', options: normalized };
const output = `${JSON.stringify(catalog, null, 2)}\n`;
if (check) {
  const current = await readFile(outputPath, 'utf8').catch(() => '');
  if (current.replaceAll('\r\n', '\n') !== output) throw new Error('managed-config-catalog.json is stale; run node scripts/generate-managed-config-catalog.mjs');
} else {
  await writeFile(outputPath, output, 'utf8');
}
