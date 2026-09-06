$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runtimeDirectory = Join-Path $repoRoot '.mcp-local'
$executable = Join-Path $runtimeDirectory 'server\CoopMcpServer.exe'
$profile = Join-Path $runtimeDirectory 'profiles.json'

try {
    if (!(Test-Path -LiteralPath $executable -PathType Leaf) -or !(Test-Path -LiteralPath $profile -PathType Leaf)) {
        throw 'MCP is not set up in this checkout. Run runmefirst.cmd (first time), or powershell -NoProfile -ExecutionPolicy Bypass -File tools\mcp\setup.ps1 (existing mb2 junction). See doc/automated-testing/mcp-setup.md.'
    }
    # Native stdio is the protocol transport; never write setup/build output here.
    & $executable --config $profile
    exit $LASTEXITCODE
}
catch {
    [Console]::Error.WriteLine("Coop MCP: $($_.Exception.Message)")
    exit 1
}
