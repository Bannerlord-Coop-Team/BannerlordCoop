[CmdletBinding()]
param(
    [string] $GameDirectory,
    [string] $RuntimeDirectory,
    [string] $DotnetPath = 'dotnet.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$RuntimeDirectory) { $RuntimeDirectory = Join-Path $repoRoot '.mcp-local' }
$RuntimeDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RuntimeDirectory)
$profilePath = Join-Path $RuntimeDirectory 'profiles.json'

try {
    # Existing operator settings are never regenerated, even if mb2 has moved.
    if (!(Test-Path -LiteralPath $profilePath -PathType Leaf)) {
        if (!$GameDirectory) {
            $junctionPath = Join-Path $repoRoot 'mb2'
            if (!(Test-Path -LiteralPath $junctionPath)) {
                throw 'mb2 junction missing. Run runmefirst.cmd or supply -GameDirectory with the game install directory.'
            }
            $junction = Get-Item -LiteralPath $junctionPath -Force -ErrorAction Stop
            if ($junction.LinkType -ne 'Junction' -or !$junction.Target) {
                throw 'mb2 must be a real Windows junction, or supply -GameDirectory with the game install directory.'
            }
            $GameDirectory = @($junction.Target)[0]
        }
        $GameDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($GameDirectory)
        $executable = Join-Path $GameDirectory 'bin\Win64_Shipping_Client\Bannerlord.exe'
        if (!(Test-Path -LiteralPath $executable -PathType Leaf)) {
            throw "Bannerlord.exe not found at $executable. Run runmefirst.cmd or supply -GameDirectory."
        }
        $profile = [ordered]@{
            artifactDirectory = Join-Path $RuntimeDirectory 'runs'
            profiles = @{ local = @{
                executable = $executable
                modules = @('Native', 'SandBoxCore', 'SandBox', 'StoryMode', 'Coop')
                serverPlatformId = 'testserver'
                clientPlatformIds = @('testclient1', 'testclient2', 'testclient3', 'testclient4')
            } }
        }
    }

    $dotnet = (Get-Command $DotnetPath -CommandType Application -ErrorAction Stop).Source
    $serverDirectory = Join-Path $RuntimeDirectory 'server'
    # This standalone project has no game assembly/project references or deployment target.
    & $dotnet publish (Join-Path $repoRoot 'tools\CoopMcpServer\CoopMcpServer.csproj') -c Release --self-contained false -o $serverDirectory
    if ($LASTEXITCODE -ne 0) { throw "MCP publish failed (exit $LASTEXITCODE)." }
    if (!(Test-Path -LiteralPath (Join-Path $serverDirectory 'CoopMcpServer.exe') -PathType Leaf)) {
        throw 'MCP publish did not produce CoopMcpServer.exe.'
    }
    if (!(Test-Path -LiteralPath $profilePath)) {
        $json = $profile | ConvertTo-Json -Depth 6
        # CreateNew also preserves settings created by another setup invocation.
        $stream = [IO.File]::Open($profilePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        try {
            $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally { $stream.Dispose() }
    }
    Write-Host "MCP setup complete. Local settings preserved at $profilePath"
    exit 0
}
catch {
    [Console]::Error.WriteLine("MCP setup failed: $($_.Exception.Message)")
    exit 1
}
