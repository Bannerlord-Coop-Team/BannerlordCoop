$ErrorActionPreference = 'Stop'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'stance-link-handles.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
$command = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Command' }, $true)
. ([scriptblock]::Create($command.Extent.Text))
function Save-Json { param($Name,$Value) }
function Invoke-LiveTestClientAction { return $script:reply }
$script:ordinal = 0
function Check([string]$Role, [string]$Output, $Succeeded, [bool]$IncludeSucceeded, [bool]$Expected, [string]$Action = 'state', [bool]$Ok = $true, [bool]$Found = $true, [int]$ExitCode = 0) {
    $result = [ordered]@{ found=$Found; output=$Output }
    if ($IncludeSucceeded) { $result.succeeded=$Succeeded }
    $script:reply = [pscustomobject]@{ ExitCode=$ExitCode; Response=[pscustomobject]@{ ok=$Ok; result=[pscustomobject]$result } }
    $accepted = $true
    try { $null = Command ([pscustomobject]@{process=[pscustomobject]@{pid=1;role=$Role}}) $Action } catch { $accepted=$false }
    if ($accepted -ne $Expected) { throw "Unexpected acceptance: role=$Role output=$Output succeeded=$Succeeded present=$IncludeSucceeded" }
}
$state = 'STANCE_HANDLE_STATE action=state id=StanceLink_vlandia_empire handle=34916 sameString=True sameNumeric=True samePrepared=False received=0'
Check 'server' $state $null $false $true
Check 'client' $state $true $true $true
Check 'client' $state $null $false $false
Check 'server' $state $false $true $false
Check 'client' $state $false $true $false
Check 'server' 'fixture failed' $null $false $false
Check 'server' ($state -replace 'action=state','action=prepare') $null $false $false
Check 'server' ($state + ' unexpected') $null $false $false
Check 'unknown' $state $true $true $false
Check 'server' $state $null $false $false 'state' $false
Check 'server' $state $null $false $false 'state' $true $false
Check 'server' $state $null $false $false 'state' $true $true 1
Check 'server' 'STANCE_HANDLE_SENT id=StanceLink_vlandia_empire handle=34916' $null $false $true 'send'
Check 'server' 'STANCE_HANDLE_SENT id=StanceLink_vlandia_empire handle=34916 unexpected' $null $false $false 'send'
Write-Output 'Command response checks passed: 14 cases; server output-only success and strict failures.'
