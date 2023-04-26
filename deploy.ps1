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
$MBBinDir       = "${BaseDir}\mb2\bin\Win64_Shipping_Client"

# create output directory structure
$DeployBinDir = "${DeployDir}\mb2"

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

# copy mod dll
Copy-Item -Force *.dll -Destination $DeployBinDir

# remove empty folders from deployment
Get-ChildItem $DeployBinDir -Recurse -Force -Directory | 
    Sort-Object -Property FullName -Descending |
    Where-Object { $($_ | Get-ChildItem -Force | Select-Object -First 1).Count -eq 0 } |
    Remove-Item

# copy to games mod folder
if(Test-Path (${BaseDir} + $config.modsDir))
{
    $ModDir = ${BaseDir} + $config.modsDir + "\" + $config.name
    Remove-Item "${ModDir}\*" -Recurse -Force -ErrorAction Ignore
    New-Item -Force -ItemType Directory -Path ${ModDir} | Out-Null
    New-Item -Force -ItemType Directory -Path "${ModDir}\bin" | Out-Null
    New-Item -Force -ItemType Directory -Path "${ModDir}\bin\Win64_Shipping_Client" | Out-Null
    $ModSourceDir = ${SolutionDir} + "\" + $config.name
    Get-ChildItem -Path "${ModSourceDir}" -Filter "*.dll" -Recurse -ErrorAction Ignore | Where { $_.PSIsContainer -eq $false } | Copy-Item -Destination "$ModDir\bin\Win64_Shipping_Client"
    $BindingRedirectFile = $config.name + ".dll.config"
    Get-ChildItem -Path "${ModSourceDir}" -Filter $BindingRedirectFile -ErrorAction Ignore -Recurse | Copy-Item -Destination "${MBBinDir}\Bannerlord.exe.config"
    Copy-Item -Force $DeployDir\SubModule.xml -Destination $ModDir\
}

# write Movie Prefabs
$MovieModDir = ${BaseDir} + $config.modsDir + "\" + $config.name + "\GUI\Prefabs"
New-Item -Force -ItemType Directory -Path ${MovieModDir} | Out-Null
Copy-Item -Force ${UIMovieDir}\* -Recurse -Destination ${MovieModDir}\
