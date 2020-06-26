Write-Output "*** prepare-links.ps1 ***"
Write-Output ""

function Green
{
    process { Write-Host $_ -ForegroundColor Green }
}

function Red
{
    process { Write-Host $_ -ForegroundColor Red }
}

#ask for run the game
$processes = @()
While ($processes.Count -eq 0)
{
	$processes = Get-Process -Name Bannerlord,Bannerlord.Native,Bannerlord_BE,TaleWorlds.MountAndBlade.Launcher -ErrorAction SilentlyContinue -FileVersionInfo
    If ($processes.Count -gt 0) {Break}
    Write-Output "*** Run Mount & Blade: Bannerlod now and then press any key here... ***"
    $key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}

#extract game path
$path = Select-Object -InputObject $processes -First 1 -ExpandProperty FileName
$path = Split-Path -Path $path -Parent
$path = Split-Path -Path $path -Parent
$path = Split-Path -Path $path -Parent

#confirm the path
$key = 0
While ($key -eq 0)
{
	Write-Output "Is this path correct for game Mount & Blade: Bannerlod?"
	Write-Output $path
	Write-Output "y - yes, n - no [default = y]"
	$key = $Host.UI.RawUI.ReadKey('IncludeKeyDown') | Select-Object -ExpandProperty VirtualKeyCode
	Write-Output ""
	Write-Output ""
	if (@(89,13,32).Where({$_ -eq $key}, 'First'))
	{
		#create junction link for game in project directory
		New-Item -ItemType Junction -Path .\mb2 -Target $path
		Write-Output "*** Link to the game path succesfully created ***" | Green
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
