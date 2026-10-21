$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'Game Saves') -Force | Out-Null
$log = Join-Path $root 'prepare.log'
@{ serverData=$root } | ConvertTo-Json -Compress | Set-Content $log
$config = Join-Path $root 'server-config.json'
[IO.File]::WriteAllText($config, '{"saveName":"original","port":4200}')
$subject = Join-Path $PSScriptRoot 'select_server_save.ps1'
try {
    $rejected = $false
    try { & $subject -PreparationLog $log -SaveName proof } catch { $rejected = $true }
    if (-not $rejected -or (Get-Content $config -Raw | ConvertFrom-Json).saveName -ne 'original') { throw 'Missing save changed config.' }
    [IO.File]::WriteAllText((Join-Path $root 'Game Saves\proof.sav'), 'fixture')
    [IO.File]::WriteAllText((Join-Path $root 'Game Saves\proof.json'), '{}')
    & $subject -PreparationLog $log -SaveName proof
    $actual = Get-Content $config -Raw | ConvertFrom-Json
    if ($actual.saveName -ne 'proof' -or $actual.port -ne 4200) { throw 'Selection failed or changed other settings.' }
    if (@(Get-ChildItem $root -Filter '*.bak-*').Count -ne 1) { throw 'Config backup missing.' }
    'PASS save selector: missing save rejected, selected save applied, other config preserved, backup retained'
} finally { Remove-Item -LiteralPath $root -Recurse -Force }