param(
    [Parameter(Mandatory)] [string] $PreparationLog,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_-]{1,64}$')] [string] $SaveName
)
$ErrorActionPreference = 'Stop'
$receipts = @(Get-Content -LiteralPath $PreparationLog | ForEach-Object {
    try { $value = $_ | ConvertFrom-Json; if ($value.serverData) { $value } } catch { }
})
if ($receipts.Count -ne 1) { throw 'Expected exactly one prepared server-data receipt.' }
$data = [IO.Path]::GetFullPath([string]$receipts[0].serverData)
foreach ($extension in @('sav','json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $data "Game Saves\$SaveName.$extension") -PathType Leaf)) {
        throw "Selected save is missing $extension in prepared server-data: $SaveName"
    }
}
$configPath = Join-Path $data 'server-config.json'
$original = [IO.File]::ReadAllText($configPath)
$config = ($original -replace '(?m)^\s*//.*$', '') | ConvertFrom-Json
$config.saveName = $SaveName
[IO.File]::WriteAllText("$configPath.bak-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff'))", $original)
[IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
[ordered]@{ saveName=$SaveName; serverData=$data; configPath=$configPath } | ConvertTo-Json -Compress