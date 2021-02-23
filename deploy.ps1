# arguments
param([string]$SolutionDir,
      [string]$TargetDir,
      [string]$TargetFileName,
      [string[]] $Libs);

$Libs = $Libs -split ','

Write-Output "*** deploy.ps1 ***"
Write-Output "SolutionDir:   ${SolutionDir}"
Write-Output "TargetDir:     ${TargetDir}"
Write-Output "TargetName:    ${TargetFileName}"
Write-Output "3rdPartyLibs:  ${Libs}"

# path to required files
$BaseDir        = "${SolutionDir}..\"
$DeployDir      = "${BaseDir}deploy\"
$ConfigPath     = "${BaseDir}config.json"
$TemplateDir    = "${BaseDir}template"
$UIMovieDir     = "${BaseDir}UIMovies"

# create output directory structure
$DeployBinDir = "$DeployDir\bin\Win64_Shipping_Client"

Remove-Item ${DeployBinDir} -Recurse -ErrorAction Ignore

New-Item -ItemType Directory -Force -Path $DeployDir | Out-Null
New-Item -ItemType Directory -Force -Path $DeployBinDir | Out-Null

# read config
$config = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
Write-Output $config

# write SubModule.xml
$subModuleContent = Get-Content -path $TemplateDir\SubModule.xml -Raw
$subModuleContent = $subModuleContent.replace('${name}', $config.name)
$subModuleContent = $subModuleContent.replace('${version}', $config.version)
$subModuleContent = $subModuleContent.replace('${game_version}', $config.game_version)
$subModuleContent | Out-File -Encoding utf8 -FilePath $DeployDir\SubModule.xml


# copy mod dll
$filesToCopy = @(${TargetFileName}) + ${Libs}
foreach ($file in $filesToCopy) 
{
    Copy-item -Force ${TargetDir}${file} -Destination $DeployBinDir
}

# copy to games mod folder
if(Test-Path (${BaseDir} + $config.modsDir))
{
    $ModDir = ${BaseDir} + $config.modsDir + "\" + $config.name
    Remove-Item ${ModDir} -Recurse -ErrorAction Ignore
    New-Item -Force -ItemType Directory -Path ${ModDir} | Out-Null
    Copy-item -Force -Recurse $DeployDir\* -Destination $ModDir\
    
}

# write Movie Prefabs
$MovieModDir = ${BaseDir} + $config.modsDir + "\" + $config.name + "\GUI\Prefabs"
New-Item -Force -ItemType Directory -Path ${MovieModDir} | Out-Null
Copy-Item -Force ${UIMovieDir}\* -Recurse -Destination ${MovieModDir}\
