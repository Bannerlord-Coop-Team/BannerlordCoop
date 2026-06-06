# arguments
param([string]$SolutionDir,
      [string]$TargetDir);

$Libs = $Libs -split ','

Write-Output "*** deploy.ps1 ***"
Write-Output "SolutionDir:   ${SolutionDir}"
Write-Output "TargetDir:     ${TargetDir}"

# path to required files
$SolutionDir            = $SolutionDir.Trim('"')
$BaseDir                = "${SolutionDir}..\"
$BaseDirWithoutQuotes   = $BaseDir.Trim('"')
$DeployDir              = "${BaseDirWithoutQuotes}deploy\"
$ConfigPath             = "${BaseDirWithoutQuotes}config.json"
$UIMovieDir             = "${BaseDirWithoutQuotes}UIMovies"

# Read config
$config = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
Write-Output $config

# copy to games mod folder
if(Test-Path (${BaseDir} + $config.modsDir))
{
    $ModDir = ${BaseDir} + $config.modsDir + "\" + $config.name
    Remove-Item "${ModDir}\*" -Recurse -Force -ErrorAction Ignore
    New-Item -Force -ItemType Directory -Path "${ModDir}\bin\Win64_Shipping_Client" | Out-Null
    $ModSourceDir = ${SolutionDir} + "\" + $config.name

    # Copy all dlls from target dir to mod folder
    Get-ChildItem -Path "${TargetDir}\" -Filter "*.dll" -Recurse -ErrorAction Ignore | Where { $_.PSIsContainer -eq $false } | Copy-Item -Destination "${ModDir}\bin\Win64_Shipping_Client"

    # Write SubModule.xml to mod folder
    $subModuleContent = Get-Content -path "${DeployDir}\SubModule.xml" -Raw
    $subModuleContent = $subModuleContent.replace('${name}', $config.name)
    $subModuleContent = $subModuleContent.replace('${main_class}', $config.main_class)
    $subModuleContent = $subModuleContent.replace('${version}', $config.version)
    $subModuleContent = $subModuleContent.replace('${game_version}', $config.game_version)
    $subModuleContent | Out-File -Encoding utf8 -FilePath "${ModDir}\SubModule.xml"

    # Copy all files except SubModule.xml to mod folder
    Get-ChildItem -Path "${DeployDir}\*" -Recurse -Force |
    Where-Object {
        $_.PSIsContainer -eq $false -and
        $_.Name -ne "SubModule.xml"
    } |
    Copy-Item -Force -Destination "${ModDir}\"
}

# Write Movie Prefabs
$MovieModDir = ${BaseDir} + $config.modsDir + "\" + $config.name + "\GUI\Prefabs"
New-Item -Force -ItemType Directory -Path "${MovieModDir}" | Out-Null
Copy-Item -Force "${UIMovieDir}\*" -Recurse -Destination "${MovieModDir}\"
