# deploy.ps1

param(
    [Parameter(Mandatory = $true)]
    [string]$SolutionDir,

    [Parameter(Mandatory = $true)]
    [string]$TargetDir,

    [Parameter(Mandatory = $true)]
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName Microsoft.VisualBasic

function Normalize-PathString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return $Path.Trim('"')
}

function Assert-NotEmpty {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace($Value.ToString())) {
        throw "${Name} is missing or empty."
    }
}

function Move-FileToRecycleBin {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile(
        $Path,
        [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
        [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin
    )
}

function Move-DirectoryChildrenToRecycleBin {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        Write-Output "Directory does not exist, skipping cleanup: ${Path}"
        return
    }

    Get-ChildItem -LiteralPath $Path -Force | ForEach-Object {
        if ($_.PSIsContainer) {
            [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory(
                $_.FullName,
                [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
                [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin
            )
        }
        else {
            [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile(
                $_.FullName,
                [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
                [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin
            )
        }
    }
}

function Assert-SafeModPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModDir,

        [Parameter(Mandatory = $true)]
        [string]$ModsRoot
    )

    $resolvedModsRoot = [System.IO.Path]::GetFullPath($ModsRoot)
    $resolvedModDir = [System.IO.Path]::GetFullPath($ModDir)

    if ($resolvedModDir -eq $resolvedModsRoot) {
        throw "Refusing to deploy because ModDir equals ModsRoot: ${resolvedModDir}"
    }

    if ($resolvedModDir.Length -lt 10) {
        throw "Refusing to deploy to suspiciously short path: ${resolvedModDir}"
    }

    if (-not $resolvedModDir.StartsWith($resolvedModsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to deploy because ModDir is not inside ModsRoot. ModDir: ${resolvedModDir}, ModsRoot: ${resolvedModsRoot}"
    }

    if (-not ($resolvedModDir -like "*\Modules\*")) {
        throw "Refusing to deploy because ModDir does not look like a Bannerlord Modules path: ${resolvedModDir}"
    }
}

Write-Output "*** deploy.ps1 ***"

# Normalize incoming paths
$SolutionDir = Normalize-PathString $SolutionDir
$TargetDir = Normalize-PathString $TargetDir
$ConfigPath = Normalize-PathString $ConfigPath

# Resolve config relative to current working directory if needed
$ConfigPath = [System.IO.Path]::GetFullPath($ConfigPath)

Write-Output "SolutionDir:   ${SolutionDir}"
Write-Output "TargetDir:     ${TargetDir}"
Write-Output "ConfigPath:    ${ConfigPath}"

if (-not (Test-Path -LiteralPath $SolutionDir -PathType Container)) {
    throw "SolutionDir does not exist: ${SolutionDir}"
}

if (-not (Test-Path -LiteralPath $TargetDir -PathType Container)) {
    throw "TargetDir does not exist: ${TargetDir}"
}

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Config file does not exist: ${ConfigPath}"
}

# Path to required files
$BaseDir = Join-Path $SolutionDir ".."
$BaseDir = [System.IO.Path]::GetFullPath($BaseDir)

$DeployDir = Join-Path $BaseDir "deploy"
$SubModuleTemplatePath = Join-Path $DeployDir "SubModule.xml"
$UIMovieDir = Join-Path $BaseDir "UIMovies"

Write-Output "BaseDir:       ${BaseDir}"
Write-Output "DeployDir:     ${DeployDir}"
Write-Output "UIMovieDir:    ${UIMovieDir}"

if (-not (Test-Path -LiteralPath $DeployDir -PathType Container)) {
    throw "Deploy directory does not exist: ${DeployDir}"
}

if (-not (Test-Path -LiteralPath $SubModuleTemplatePath -PathType Leaf)) {
    throw "SubModule.xml template does not exist: ${SubModuleTemplatePath}"
}

# Read config
$config = Get-Content -Raw -LiteralPath $ConfigPath | ConvertFrom-Json

Assert-NotEmpty $config.modsDir "config.modsDir"
Assert-NotEmpty $config.name "config.name"
Assert-NotEmpty $config.main_class "config.main_class"
Assert-NotEmpty $config.version "config.version"
Assert-NotEmpty $config.game_version "config.game_version"

$ModsRoot = Join-Path $BaseDir $config.modsDir
$ModDir = Join-Path $ModsRoot $config.name
$BinDir = Join-Path $ModDir "bin\Win64_Shipping_Client"
$MovieModDir = Join-Path $ModDir "GUI\Prefabs"
$SubModuleOutputPath = Join-Path $ModDir "SubModule.xml"

Write-Output "Mod name:      $($config.name)"
Write-Output "ModsRoot:      ${ModsRoot}"
Write-Output "ModDir:        ${ModDir}"
Write-Output "BinDir:        ${BinDir}"
Write-Output "MovieModDir:   ${MovieModDir}"

if (-not (Test-Path -LiteralPath $ModsRoot -PathType Container)) {
    throw "Mods root does not exist: ${ModsRoot}"
}

Assert-SafeModPath -ModDir $ModDir -ModsRoot $ModsRoot

# Create directories
New-Item -Force -ItemType Directory -Path $ModDir | Out-Null
New-Item -Force -ItemType Directory -Path $BinDir | Out-Null
New-Item -Force -ItemType Directory -Path $MovieModDir | Out-Null

# Clean only deploy-owned outputs
Write-Output "Cleaning deploy-owned outputs by moving them to Recycle Bin..."

Move-DirectoryChildrenToRecycleBin -Path $ModDir

# Copy DLLs from target dir to mod folder
Write-Output "Copying DLLs..."

Get-ChildItem -LiteralPath $TargetDir -Filter "*.dll" -Recurse -ErrorAction Ignore |
    Where-Object { $_.PSIsContainer -eq $false } |
    Copy-Item -Force -Destination $BinDir

# Write SubModule.xml to mod folder
Write-Output "Writing SubModule.xml..."

$subModuleContent = Get-Content -LiteralPath $SubModuleTemplatePath -Raw
$subModuleContent = $subModuleContent.Replace('${name}', $config.name)
$subModuleContent = $subModuleContent.Replace('${main_class}', $config.main_class)
$subModuleContent = $subModuleContent.Replace('${version}', $config.version)
$subModuleContent = $subModuleContent.Replace('${game_version}', $config.game_version)

$subModuleContent | Out-File -Encoding utf8 -FilePath $SubModuleOutputPath

# Copy all deploy files except SubModule.xml to mod folder
Write-Output "Copying deploy files..."

Get-ChildItem -LiteralPath $DeployDir -Recurse -Force |
    Where-Object {
        $_.PSIsContainer -eq $false -and
        $_.Name -ne "SubModule.xml"
    } |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($DeployDir.Length).TrimStart('\', '/')
        $destinationPath = Join-Path $ModDir $relativePath
        $destinationDir = Split-Path -Parent $destinationPath

        New-Item -Force -ItemType Directory -Path $destinationDir | Out-Null
        Copy-Item -Force -LiteralPath $_.FullName -Destination $destinationPath
    }

# Copy UI movie prefabs
if (Test-Path -LiteralPath $UIMovieDir -PathType Container) {
    Write-Output "Copying UI movie prefabs..."

    Get-ChildItem -LiteralPath $UIMovieDir -Recurse -Force |
        Where-Object { $_.PSIsContainer -eq $false } |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($UIMovieDir.Length).TrimStart('\', '/')
            $destinationPath = Join-Path $MovieModDir $relativePath
            $destinationDir = Split-Path -Parent $destinationPath

            New-Item -Force -ItemType Directory -Path $destinationDir | Out-Null
            Copy-Item -Force -LiteralPath $_.FullName -Destination $destinationPath
        }
}
else {
    Write-Output "UIMovies directory does not exist, skipping: ${UIMovieDir}"
}

Write-Output "Deploy complete."