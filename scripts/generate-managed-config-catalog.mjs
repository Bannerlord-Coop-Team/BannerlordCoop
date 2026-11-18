#!/usr/bin/env node
import { createHash } from 'node:crypto';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sourcePath = path.join(root, 'source', 'GameInterface', 'Configuration', 'ModConfigData.cs');
const defaultsPath = path.join(root, 'deploy', 'mod-config.default.json');
const outputPath = path.join(root, 'deploy', 'managed-config-catalog.json');
const check = process.argv.includes('--check');
const execFileAsync = promisify(execFile);

const source = await readFile(sourcePath, 'utf8');
const defaultsText = await readFile(defaultsPath, 'utf8');
const defaults = JSON.parse(defaultsText.replace(/^\uFEFF/u, '').replaceAll(/\/\/.*$/gmu, '').replaceAll(/,\s*([}\]])/gu, '$1'));
const block = source.match(/public sealed class ModOptionsData\s*\{(?<body>[\s\S]*?)\n\}/u)?.groups?.body;
if (block === undefined) throw new Error('ModOptionsData was not found');

const enumValues = new Map();
for (const match of source.matchAll(/public\s+enum\s+(?<name>[A-Za-z0-9_]+)\s*\{(?<body>[\s\S]*?)\n\}/gu)) {
  const body = match.groups.body.replaceAll(/\/\/[^\n]*|\/\*[\s\S]*?\*\//gu, '');
  enumValues.set(match.groups.name, [...new Set([...body.matchAll(/^\s*(?<value>[A-Z][A-Za-z0-9_]*)\s*,?/gmu)].map((entry) => entry.groups.value))]);
}

function parseAttribute(text, key) {
  const attribute = text.match(/\[ManagedConfigOption\((?<args>[^\n]*)\)\]/u)?.groups?.args;
  if (attribute === undefined) throw new Error(`${key} is missing ManagedConfigOption metadata`);
  const result = {};
  for (const pair of attribute.split(',').map((item) => item.trim()).filter(Boolean)) {
    const match = pair.match(/^(?<name>[A-Za-z][A-Za-z0-9_]*)\s*=\s*(?<value>.+)$/u);
    if (match === null) throw new Error(`Invalid ManagedConfigOption argument for ${key}: ${pair}`);
    const name = ({ Stable: 'stable', AvailableSince: 'availableSince', Min: 'min', Max: 'max', MaxItems: 'maxItems', ItemPattern: 'itemPattern', UniqueItems: 'uniqueItems', CoerceRanges: 'coerceRanges', OmitForOldAgents: 'omitForOldAgents' })[match.groups.name];
    if (name === undefined) throw new Error(`Unknown ManagedConfigOption field for ${key}: ${match.groups.name}`);
    const raw = match.groups.value.trim();
    if (/^".*"$/u.test(raw)) result[name] = raw.slice(1, -1);
    else if (raw === 'true' || raw === 'false') result[name] = raw === 'true';
    else if (/^-?\d+(?:\.\d+)?$/u.test(raw)) result[name] = Number(raw);
    else throw new Error(`Unsupported ManagedConfigOption value for ${key}: ${raw}`);
    if (name === 'coerceRanges') {
      const matchRange = String(result[name]).match(/^(?<from>-?\d+)-(?<to>-?\d+)=(?<value>-?\d+)$/u);
      if (matchRange === null) throw new Error(`Invalid coercion range for ${key}`);
      const range = [Number(matchRange.groups.from), Number(matchRange.groups.to), Number(matchRange.groups.value)];
      if (range.some(part => !Number.isSafeInteger(part)) || range[0] > range[1]) throw new Error(`Invalid coercion range for ${key}`);
      result[name] = [range];
    }
  }
  return result;
}

function validateDefault(option) {
  const value = option.default;
  if (option.type === 'boolean' && typeof value !== 'boolean') throw new Error(`Default for ${option.key} is not boolean`);
  if (option.type === 'integer' && (!Number.isSafeInteger(value))) throw new Error(`Default for ${option.key} is not an integer`);
  if (option.type === 'number' && (typeof value !== 'number' || !Number.isFinite(value))) throw new Error(`Default for ${option.key} is not numeric`);
  if (option.type === 'enum' && (!option.values?.includes(value))) throw new Error(`Default for ${option.key} is not an enum member`);
  if (option.type === 'steamIdList' && (!Array.isArray(value) || value.some((item) => typeof item !== 'string'))) throw new Error(`Default for ${option.key} is not a string list`);
  if (option.min !== undefined && value < option.min) throw new Error(`Default for ${option.key} is below its minimum`);
  if (option.max !== undefined && value > option.max) throw new Error(`Default for ${option.key} is above its maximum`);
  if (option.maxItems !== undefined && value.length > option.maxItems) throw new Error(`Default for ${option.key} exceeds maxItems`);
  if (option.uniqueItems === true && new Set(value).size !== value.length) throw new Error(`Default for ${option.key} is not unique`);
  if (option.itemPattern !== undefined && value.some((item) => !new RegExp(option.itemPattern, 'u').test(item))) throw new Error(`Default for ${option.key} violates itemPattern`);
}

const options = [];
for (const match of block.matchAll(/(?<attributes>(?:^\s*\[ManagedConfigOption\([^\n]+\)\]\s*\r?\n)*)^\s*public\s+(?<type>bool|int|float|double|List\s*<\s*string\s*>)\??\s+(?<name>[A-Z][A-Za-z0-9]*)\s*\{\s*get;\s*set;\s*\}/gmu)) {
  const key = match.groups.name[0].toLowerCase() + match.groups.name.slice(1);
  const type = match.groups.type.replaceAll(/\s/gu, '') === 'List<string>' ? 'steamIdList'
    : match.groups.type === 'bool' ? 'boolean'
      : match.groups.type === 'int' ? 'integer' : 'number';
  const value = defaults.modOptions?.[key];
  if (value === undefined) throw new Error(`Default is missing for ${key}`);
  const metadata = parseAttribute(match.groups.attributes, key);
  options.push({ key, type, ...metadata, default: value });
}
for (const match of block.matchAll(/(?<attributes>(?:^\s*\[ManagedConfigOption\([^\n]+\)\]\s*\r?\n)*)^\s*public\s+(?<type>[A-Z][A-Za-z0-9_]*)\??\s+(?<name>[A-Z][A-Za-z0-9]*)\s*\{\s*get;\s*set;\s*\}/gmu)) {
  if (options.some((option) => option.key === match.groups.name[0].toLowerCase() + match.groups.name.slice(1))) continue;
  const key = match.groups.name[0].toLowerCase() + match.groups.name.slice(1);
  const value = defaults.modOptions?.[key];
  if (value === undefined) throw new Error(`Default is missing for ${key}`);
  const metadata = parseAttribute(match.groups.attributes, key);
  const values = enumValues.get(match.groups.type);
  if (values === undefined) throw new Error(`Enum declaration is missing for ${match.groups.type}`);
  options.push({ key, type: 'enum', values, ...metadata, default: value });
}
for (const option of options) validateDefault(option);
if (options.length === 0) throw new Error('No managed options were found');
const normalized = options.map(({ key, type, values, default: value, ...metadata }) => ({ key, type, ...metadata, ...(values === undefined ? {} : { values }), default: value }));
const digest = createHash('sha256').update(JSON.stringify(normalized)).digest('hex');
let sourceCommit = 'unknown';
try {
  ({ stdout: sourceCommit } = await execFileAsync('git', ['log', '-1', '-Spublic bool OmitForOldAgents', '--format=%H', '--', 'source/GameInterface/Configuration/ModConfigData.cs'], { cwd: root }));
  sourceCommit = sourceCommit.trim();
} catch { /* source checkout may be an archive */ }
const oldAgentOmittedKeys = normalized.filter(option => option.omitForOldAgents === true).map(option => option.key);
const catalog = { schemaVersion: 2, catalogRevision: digest, contentDigest: digest, sourceCommit, source: 'BannerlordCoop/source/GameInterface/Configuration/ModConfigData.cs', oldAgentOmittedKeys, options: normalized };
const output = `${JSON.stringify(catalog, null, 2)}\n`;
if (check) {
  const current = await readFile(outputPath, 'utf8').catch(() => '');
  let currentCatalog;
  try { currentCatalog = JSON.parse(current.replace(/^\uFEFF/u, '')); } catch { currentCatalog = null; }
  const generatedCatalog = JSON.parse(output);
  // Shallow PR checkouts do not retain the metadata commit's history. The content digest is the
  // reproducible contract check, while the checked-in sourceCommit remains release provenance.
  if (currentCatalog !== null && generatedCatalog.sourceCommit !== currentCatalog.sourceCommit) generatedCatalog.sourceCommit = currentCatalog.sourceCommit;
  if (currentCatalog === null || JSON.stringify(currentCatalog) !== JSON.stringify(generatedCatalog)) throw new Error('managed-config-catalog.json is stale; run node scripts/generate-managed-config-catalog.mjs');
} else {
  await writeFile(outputPath, output, 'utf8');
}
