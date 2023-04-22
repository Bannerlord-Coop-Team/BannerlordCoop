# arguments
param([string]$SolutionDir,
      [string]$TargetDir);

$Libs = $Libs -split ','

Write-Output "*** deploy.ps1 ***"
Write-Output "SolutionDir:   ${SolutionDir}"
Write-Output "TargetDir:     ${TargetDir}"

# path to required files
$BaseDir        = "${SolutionDir}..\"
$DeployDir      = "${BaseDir}deploy\"
$ConfigPath     = "${BaseDir}config.json"
$TemplateDir    = "${BaseDir}template"
$UIMovieDir     = "${BaseDir}UIMovies"

# create output directory structure
$DeployBinDir = "$DeployDir\mb2"

Remove-Item ${DeployBinDir} -Recurse -ErrorAction Ignore

New-Item -ItemType Directory -Force -Path $DeployDir | Out-Null

# read config
$config = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
Write-Output $config

# write SubModule.xml
$subModuleContent = Get-Content -path $TemplateDir\SubModule.xml -Raw
$subModuleContent = $subModuleContent.replace('${name}', $config.name)
$subModuleContent = $subModuleContent.replace('${main_class}', $config.main_class)
$subModuleContent = $subModuleContent.replace('${version}', $config.version)
$subModuleContent = $subModuleContent.replace('${game_version}', $config.game_version)
$subModuleContent | Out-File -Encoding utf8 -FilePath $DeployDir\SubModule.xml

# copy all dlls
Copy-Item mb2 -Force -Filter "TaleWorlds*.dll" -Destination $DeployDir -Recurse
Copy-Item mb2 -Force -Filter "SandBox*.dll" -Destination $DeployDir -Recurse

# remove empty folders
Get-ChildItem $DeployBinDir -Recurse -Force -Directory | 
    Sort-Object -Property FullName -Descending |
    Where-Object { $($_ | Get-ChildItem -Force | Select-Object -First 1).Count -eq 0 } |
    Remove-Item

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
