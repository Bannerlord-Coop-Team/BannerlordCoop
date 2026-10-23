$ErrorActionPreference = 'Stop'
$setupSucceeded = $false

Write-Output "*** prepare-links.ps1 ***"
Write-Output ""

# Writes piped text in green.
function Green
{
    process { Write-Host $_ -ForegroundColor Green }
}

# Writes piped text in red.
function Red
{
    process { Write-Host $_ -ForegroundColor Red }
}

# Returns the WSLENV value with exactly one BANNERLORD_DIR/p entry, keeping every other entry in order.
function Join-WslEnv([string] $current)
{
    $entries = @($current -split ':' | Where-Object { $_ -and ($_ -split '/')[0] -ne 'BANNERLORD_DIR' })
    return (@($entries) + 'BANNERLORD_DIR/p') -join ':'
}

# Saves the game path as the user BANNERLORD_DIR and forwards it to WSL as a translated path.
function Save-GameDirectory([string] $gameDirectory)
{
    [Environment]::SetEnvironmentVariable('BANNERLORD_DIR', $gameDirectory, [System.EnvironmentVariableTarget]::User)
    $wslEnv = [Environment]::GetEnvironmentVariable('WSLENV', [System.EnvironmentVariableTarget]::User)
    $updatedWslEnv = Join-WslEnv $wslEnv
    if ($updatedWslEnv -eq $wslEnv) { return }
    [Environment]::SetEnvironmentVariable('WSLENV', $updatedWslEnv, [System.EnvironmentVariableTarget]::User)
}

# Removes the old mb2 link from this checkout; rmdir without /s deletes only the link, never the game files behind it.
function Remove-OldGameLink
{
    $link = Join-Path $PSScriptRoot 'mb2'
    $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
    if ($null -eq $item -or $item.LinkType -notin @('Junction', 'SymbolicLink')) { return }
    cmd /c rmdir "$link"
    Write-Output "*** Removed the old mb2 link, the game install is untouched ***" | Green
}

#ask for run the game
$processes = @()
While ($processes.Count -eq 0)
{
	$processes = Get-Process -Name Bannerlord,Bannerlord.Native,Bannerlord_BE,TaleWorlds.MountAndBlade.Launcher -ErrorAction SilentlyContinue -FileVersionInfo
    If ($processes.Count -gt 0) {Break}
    Write-Output "*** Run Mount & Blade: Bannerlord now and then press any key here... ***"
    $key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}

#extract game path
$path = $processes | Select-Object -First 1 -ExpandProperty FileName
$path = Split-Path -Path $path -Parent
$path = Split-Path -Path $path -Parent
$path = Split-Path -Path $path -Parent

#confirm the path
$key = 0
While ($key -eq 0)
{
	Write-Output "Is this path correct for game Mount & Blade: Bannerlord?"
	Write-Output $path
	Write-Output "y - yes, n - no [default = y]"
	$key = $Host.UI.RawUI.ReadKey('IncludeKeyDown') | Select-Object -ExpandProperty VirtualKeyCode
	Write-Output ""
	Write-Output ""
	if (@(89,13,32).Where({$_ -eq $key}, 'First'))
	{
		if (!(Test-Path -LiteralPath (Join-Path $path 'bin\Win64_Shipping_Client') -PathType Container))
		{
			Write-Output "$path has no bin\Win64_Shipping_Client folder, so it is not a Bannerlord install. Process aborted." | Red
			break
		}
		#remember the game path for builds, tools and WSL
		Save-GameDirectory $path
		Remove-OldGameLink
		$COOPLOGdir = $PSScriptRoot + "\COOP_LOG"
		New-Item -ItemType "directory" -Path $COOPLOGdir -Force
		[Environment]::SetEnvironmentVariable('COOP_LOG',$COOPLOGdir,[System.EnvironmentVariableTarget]::User)
		Write-Output "*** BANNERLORD_DIR set to $path ***" | Green
		Write-Output "*** Restart Visual Studio, terminals and WSL (wsl --shutdown) so they pick up BANNERLORD_DIR ***" | Green
        $setupSucceeded = $true
	}
	elseif ($key -eq 78)
	{
		Write-Output "Please, check runned game and try again. Process aborted." | Red
	}
	else
	{
		$key = 0
	}
}
Write-Output "*** press any key... ***"
$key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')

if (!$setupSucceeded) { exit 1 }
exit 0
