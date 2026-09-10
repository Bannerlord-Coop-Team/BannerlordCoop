// Static checks only. Uses an existing adapter installation; never installs or starts an agent.
import assert from 'node:assert/strict';
import { readFileSync, copyFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const [adapterDirectory, schemaPath] = process.argv.slice(2);
if (!adapterDirectory || !schemaPath) {
    console.error('Usage: node tools/mcp/validate-config.mjs ADAPTER_DIRECTORY OFFICIAL_CODEX_SCHEMA.json');
    process.exit(1);
}
const repo = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(join(resolve(adapterDirectory), 'package.json'));
const { parse } = require('smol-toml');
const Ajv = require('ajv');
const temporary = mkdtempSync(join(tmpdir(), 'Coop MCP config '));
// Isolate the adapter parser from all user/project override sources.
process.env.PI_MCP_CONFIG_MODE = 'exclusive';
process.env.PI_CODING_AGENT_DIR = temporary;
delete process.env.PI_PACKAGE_DIR;
copyFileSync(join(repo, '.mcp.json'), join(temporary, 'mcp.json'));
try {
    const { loadMcpConfig, getProjectConfigPath } = await import(pathToFileURL(join(resolve(adapterDirectory), 'dist/config.js')));
    const shared = JSON.parse(readFileSync(join(repo, '.mcp.json'), 'utf8'));
    const loaded = loadMcpConfig(undefined, repo);
    const pi = loaded.mcpServers['bannerlord-coop'];
    assert.deepEqual(pi, shared.mcpServers['bannerlord-coop']);
    assert.equal(pi.lifecycle, 'lazy-keep-alive');
    assert.equal(pi.directTools, true);
    assert.ok(pi.requestTimeoutMs > 300000);
    assert.equal(getProjectConfigPath(repo), join(repo, '.mcp.json'));
    assert.equal(getProjectConfigPath(join(repo, 'source')), join(repo, 'source', '.mcp.json'));

    const codex = parse(readFileSync(join(repo, '.codex/config.toml'), 'utf8'));
    const schema = JSON.parse(readFileSync(resolve(schemaPath), 'utf8'));
    // Rust numeric formats are annotations; JSON Schema types/ranges still validate.
    const validate = new Ajv({ strict: false, allErrors: true, validateFormats: false }).compile(schema);
    assert.ok(validate(codex), JSON.stringify(validate.errors));
    const server = codex.mcp_servers['bannerlord-coop'];
    assert.equal(server.command, pi.command);
    assert.deepEqual(server.args, pi.args);
    assert.equal(server.cwd, undefined); // No assumption about Codex's relative MCP cwd base.
    assert.ok(server.tool_timeout_sec > 300);
    assert.ok(server.startup_timeout_sec >= 30);
    assert.equal(codex.projects, undefined); // Never grant project trust.
    assert.equal(validate({ ...codex, mcp_servers: { 'bannerlord-coop': { ...server, tool_timeout_sec: 'invalid' } } }), false);
    console.log('PASS: Pi adapter parser, cwd-local discovery, lazy-keep-alive/direct tools/360s timeout');
    console.log('PASS: TOML parser + official Codex JSON Schema (positive and negative), identical launcher argv, no trust/cwd override');
    console.log('NOT RUN: actual Pi/Codex agent integration; parser checks do not prove client process behavior.');
}
finally { rmSync(temporary, { recursive: true, force: true }); }
