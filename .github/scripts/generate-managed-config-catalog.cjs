const { createHash } = require('node:crypto');
const { execFileSync } = require('node:child_process');
const { readFileSync, writeFileSync } = require('node:fs');
const path = require('node:path');

const sourcePath = 'source/GameInterface/Configuration/ModConfigData.cs';
const defaultsPath = 'deploy/mod-config.default.json';
const outputPath = 'deploy/managed-config-catalog.json';

const optionTypes = new Map([
  ['bool', 'boolean'],
  ['int', 'integer'],
  ['float', 'number'],
  ['double', 'number'],
  ['List<string>', 'steamIdList'],
]);
const metadataTypes = new Map([
  ['stable', 'boolean'],
  ['availableSince', 'string'],
  ['min', 'number'],
  ['max', 'number'],
  ['maxItems', 'number'],
  ['itemPattern', 'string'],
  ['uniqueItems', 'boolean'],
  ['coercePositiveBelow', 'number'],
  ['omitForOldAgents', 'boolean'],
]);

function stripComments(text) {
  return text.replace(/"(?:\\.|[^"\\])*"|\/\/[^\r\n]*|\/\*[\s\S]*?\*\//gu, token => token.startsWith('"') ? token : ' ');
}

function parseDefaults(text) {
  const json = stripComments(text.replace(/^\uFEFF/u, ''))
    .replace(/"(?:\\.|[^"\\])*"|,(?=\s*[}\]])/gu, token => token === ',' ? '' : token);
  const { modOptions } = JSON.parse(json);
  if (!modOptions || typeof modOptions !== 'object' || Array.isArray(modOptions)) {
    throw new Error('modOptions is missing from the default config');
  }
  return modOptions;
}

function parseEnums(source) {
  const enums = new Map();
  for (const { groups: { name, body } } of source.matchAll(/\bpublic\s+enum\s+(?<name>\w+)(?:\s*:\s*\w+)?\s*\{(?<body>[^}]*)\}/gu)) {
    const values = body.trim().replace(/,$/u, '').split(',').map(member => {
      const match = member.trim().match(/^(?<name>[A-Za-z_]\w*)(?:\s*=\s*.+)?$/su);
      if (!match) throw new Error('Unsupported member in enum ' + name + ': ' + member.trim());
      return match.groups.name;
    });
    enums.set(name, values);
  }
  return enums;
}

function parseMetadata(attributes, key, constants) {
  const matches = [...attributes.matchAll(/\[\s*ManagedConfigOption\s*\((?<args>(?:"(?:\\.|[^"\\])*"|[^")])*)\)\s*\]/gu)];
  if (matches.length !== 1) throw new Error(key + ' must have exactly one ManagedConfigOption attribute');

  const metadata = {};
  const args = matches[0].groups.args.trim();
  const argument = /\s*(?<name>[A-Z]\w*)\s*=\s*(?<value>"(?:\\.|[^"\\])*"|[^,\s"]+)\s*(?:,|$)/gyu;
  let end = 0;
  for (const match of args.matchAll(argument)) {
    end = match.index + match[0].length;
    const name = match.groups.name[0].toLowerCase() + match.groups.name.slice(1);
    const type = metadataTypes.get(name);
    if (!type || Object.hasOwn(metadata, name)) throw new Error('Invalid metadata field for ' + key + ': ' + name);

    const raw = match.groups.value;
    let value;
    try {
      value = constants.has(raw) ? constants.get(raw) : JSON.parse(raw);
    } catch {
      throw new Error('Invalid metadata value for ' + key + ': ' + name + ' = ' + raw);
    }
    if (typeof value !== type || (type === 'number' && !Number.isFinite(value))) {
      throw new Error('Invalid metadata value for ' + key + ': ' + name);
    }
    metadata[name] = value;
  }
  if (end !== args.length) throw new Error('Invalid ManagedConfigOption arguments for ' + key);
  if (metadata.stable === undefined || !/^\d+\.\d+\.\d+$/u.test(metadata.availableSince ?? '')) {
    throw new Error(key + ' needs explicit stable and availableSince metadata');
  }

  const { coercePositiveBelow, ...constraints } = metadata;
  if (coercePositiveBelow !== undefined) {
    if (!Number.isSafeInteger(coercePositiveBelow) || coercePositiveBelow < 1) {
      throw new Error('Invalid positive coercion threshold for ' + key);
    }
    if (coercePositiveBelow > 1) constraints.coerceRanges = [[1, coercePositiveBelow - 1, coercePositiveBelow]];
  }
  return constraints;
}

function validateDefault(option) {
  const { key, type, default: value } = option;
  if (type === 'boolean' && typeof value !== 'boolean') throw new Error('Default for ' + key + ' is not boolean');
  if (type === 'integer' && (!Number.isInteger(value) || value < -(2 ** 31) || value >= 2 ** 31)) {
    throw new Error('Default for ' + key + ' is not a 32-bit integer');
  }
  if (type === 'number' && !Number.isFinite(value)) throw new Error('Default for ' + key + ' is not numeric');
  if (type === 'enum' && !option.values.includes(value)) throw new Error('Default for ' + key + ' is not an enum member');
  if (type === 'steamIdList' && (!Array.isArray(value) || value.some(item => typeof item !== 'string'))) {
    throw new Error('Default for ' + key + ' is not a string list');
  }
  if (option.min !== undefined && value < option.min) throw new Error('Default for ' + key + ' is below its minimum');
  if (option.max !== undefined && value > option.max) throw new Error('Default for ' + key + ' is above its maximum');
  if (option.maxItems !== undefined && value.length > option.maxItems) throw new Error('Default for ' + key + ' exceeds maxItems');
  if (option.uniqueItems && new Set(value).size !== value.length) throw new Error('Default for ' + key + ' is not unique');
  if (option.itemPattern !== undefined) {
    const pattern = new RegExp(option.itemPattern, 'u');
    if (value.some(item => !pattern.test(item))) throw new Error('Default for ' + key + ' violates itemPattern');
  }
}

function buildCatalog(sourceText, defaultsText, sourceCommit = 'unknown') {
  const source = stripComments(sourceText);
  const defaults = parseDefaults(defaultsText);
  const body = source.match(/\bpublic\s+sealed\s+class\s+ModOptionsData\s*\{(?<body>[\s\S]*?)^\}/mu)?.groups.body;
  if (body === undefined) throw new Error('ModOptionsData was not found');

  const enumValues = parseEnums(source);
  const constants = new Map([...body.matchAll(/\bconst\s+int\s+(?<name>\w+)\s*=\s*(?<value>-?\d[\d_]*)\s*;/gu)]
    .map(({ groups: { name, value } }) => [name, Number(value.replaceAll('_', ''))]));
  const properties = /(?<attributes>(?:\[(?:"(?:\\.|[^"\\])*"|[^\]"])*\]\s*)*)\bpublic\s+(?<type>[^;={}]+?)\s+(?<name>[A-Za-z_]\w*)\s*\{\s*get\s*;\s*set\s*;\s*\}/gu;
  const options = new Map();
  for (const { groups: { attributes, type: declaredType, name } } of body.matchAll(properties)) {
    if (name === 'UnknownKeys') continue;

    const key = name[0].toLowerCase() + name.slice(1);
    const csharpType = declaredType.replace(/\s+/gu, '').replace(/\?$/u, '');
    const values = enumValues.get(csharpType);
    const type = values ? 'enum' : optionTypes.get(csharpType);
    if (!type) throw new Error('Unsupported managed option type for ' + key + ': ' + declaredType);
    if (!Object.hasOwn(defaults, key)) throw new Error('Default is missing for ' + key);

    const metadata = parseMetadata(attributes, key, constants);
    const option = { key, type, ...metadata, ...(values ? { values } : {}), default: defaults[key] };
    validateDefault(option);
    options.set(key, option);
  }

  if (!options.size) throw new Error('No managed options were found');
  const attributeCount = [...body.matchAll(/\[\s*ManagedConfigOption\b/gu)].length;
  if (attributeCount !== options.size) throw new Error('ManagedConfigOption metadata without a managed property');
  for (const key of Object.keys(defaults)) {
    if (!options.has(key)) throw new Error('No managed property for default ' + key);
  }

  const entries = [...options.values()];
  const digest = createHash('sha256').update(JSON.stringify(entries)).digest('hex');
  const oldAgentOmittedKeys = entries.filter(option => option.omitForOldAgents).map(option => option.key);
  const catalog = {
    schemaVersion: 2,
    catalogRevision: digest,
    contentDigest: digest,
    sourceCommit,
    source: 'BannerlordCoop/' + sourcePath,
    oldAgentOmittedKeys,
    options: entries,
  };
  return JSON.stringify(catalog, null, 2) + '\n';
}

function main() {
  const root = path.resolve(__dirname, '../..');
  const read = file => readFileSync(path.join(root, file), 'utf8');
  const check = process.argv.includes('--check');
  let sourceCommit = 'unknown';
  if (!check) {
    try {
      sourceCommit = execFileSync('git', ['log', '-1', '--format=%H', '--', sourcePath], {
        cwd: root,
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'ignore'],
        windowsHide: true,
      }).trim() || sourceCommit;
    } catch { /* Source archives do not have Git history. */ }
  }

  const output = buildCatalog(read(sourcePath), read(defaultsPath), sourceCommit);
  if (check) {
    let current;
    try {
      current = JSON.parse(read(outputPath).replace(/^\uFEFF/u, ''));
    } catch (error) {
      if (error.code !== 'ENOENT' && !(error instanceof SyntaxError)) throw error;
    }
    const expected = JSON.parse(output);
    // Shallow CI checkouts may lack the source commit recorded when the catalog was generated.
    if (current && typeof current.sourceCommit === 'string') expected.sourceCommit = current.sourceCommit;
    if (JSON.stringify(current) !== JSON.stringify(expected)) {
      throw new Error(outputPath + ' is stale; run node .github/scripts/generate-managed-config-catalog.cjs');
    }
  } else {
    writeFileSync(path.join(root, outputPath), output, 'utf8');
  }
}

if (require.main === module) main();
module.exports = { buildCatalog };
