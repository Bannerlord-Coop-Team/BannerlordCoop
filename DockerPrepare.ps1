# Copies the game assemblies and test data the Docker image needs from BANNERLORD_DIR into DockerAssembliesTemp.
$BannerlordDir = $env:BANNERLORD_DIR
if ([string]::IsNullOrWhiteSpace($BannerlordDir)) {
    throw "BANNERLORD_DIR is not set. Run runmefirst.cmd, then open a new terminal."
}
if (-not (Test-Path -LiteralPath (Join-Path $BannerlordDir "bin\Win64_Shipping_Client") -PathType Container)) {
    throw "BANNERLORD_DIR ($BannerlordDir) has no bin\Win64_Shipping_Client folder, so it isn't a Bannerlord install."
}

$AssembliesTempDir = Join-Path $PSScriptRoot "DockerAssembliesTemp"
$MBBinDir = Join-Path $BannerlordDir "bin"
$MBModulesDir = Join-Path $BannerlordDir "Modules"

# Recreate the temp assemblies folder
Remove-Item ${AssembliesTempDir} -Recurse -ErrorAction Ignore
New-Item -Force -ItemType Directory -Path ${AssembliesTempDir} | Out-Null

# Copy game dlls
Copy-Item "${MBBinDir}" -Force -Filter "TaleWorlds*.dll" -Destination "${AssembliesTempDir}\bin" -Recurse
Copy-Item "${MBModulesDir}\Native" -Force -Filter "TaleWorlds*.dll" -Destination "${AssembliesTempDir}\Modules\Native" -Recurse
Copy-Item "${MBModulesDir}\SandBox" -Force -Filter "SandBox*.dll" -Destination "${AssembliesTempDir}\Modules\SandBox" -Recurse

# Copy game UI and module data used by tests
$Modules = @("Native", "SandBox")
$Folders = @("GUI", "ModuleData")

foreach ($Module in $Modules) {
    foreach ($Folder in $Folders) {
        $Destination = "${AssembliesTempDir}\Modules\${Module}\${Folder}"
        New-Item -Force -ItemType Directory -Path $Destination | Out-Null
        Copy-Item "${MBModulesDir}\${Module}\${Folder}\*" -Force -Destination $Destination -Recurse
    }
}

# Remove empty folders
Get-ChildItem $AssembliesTempDir -Recurse -Force -Directory | 
    Sort-Object -Property FullName -Descending |
    Where-Object { $($_ | Get-ChildItem -Force | Select-Object -First 1).Count -eq 0 } |
    Remove-Item