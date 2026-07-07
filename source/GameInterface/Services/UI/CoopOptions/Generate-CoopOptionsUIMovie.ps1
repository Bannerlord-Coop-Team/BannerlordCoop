param(
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$CoopOptionsDir = $PSScriptRoot
$TemplatePath = Join-Path $CoopOptionsDir "CoopOptionsUIMovie.template.xml"
$RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $CoopOptionsDir "..\..\..\..\.."))

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepoRoot "UIMovies\CoopOptionsUIMovie.xml"
}

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$IncludePattern = [regex]'(?m)^(?<Indent>[ \t]*)<!--\s*COOP_OPTIONS_(?:PROVIDER|SECTION):\s*(?<Path>[^>]+?)\s*-->'
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Assert-Xml {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    try {
        [xml]$Content | Out-Null
    }
    catch {
        throw "Invalid XML in ${Path}: $($_.Exception.Message)"
    }
}

function Assert-IsUnderCoopOptions {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (!$Path.StartsWith($CoopOptionsDir, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to include XML outside CoopOptions: ${Path}"
    }
}

function Apply-Indent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Indent
    )

    $trimmedText = $Text.TrimEnd("`r", "`n")
    $lines = $trimmedText -split "`r?`n"
    return (($lines | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace($_)) {
            ""
        }
        else {
            "${Indent}${_}"
        }
    }) -join "`r`n")
}

function Expand-Includes {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [string[]]$Stack = @()
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    Assert-IsUnderCoopOptions -Path $resolvedPath

    if ($Stack -contains $resolvedPath) {
        throw "Circular CoopOptions XML include detected: ${resolvedPath}"
    }

    if (!(Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "CoopOptions XML file does not exist: ${resolvedPath}"
    }

    $text = [System.IO.File]::ReadAllText($resolvedPath)
    Assert-Xml -Content $text -Path $resolvedPath

    $sourceDir = Split-Path -Parent $resolvedPath
    $nextStack = $Stack + $resolvedPath

    return $IncludePattern.Replace($text, {
        param($match)

        $includePath = $match.Groups["Path"].Value.Trim()
        $indent = $match.Groups["Indent"].Value
        $fragmentPath = [System.IO.Path]::GetFullPath((Join-Path $sourceDir $includePath))
        $fragmentText = Expand-Includes -Path $fragmentPath -Stack $nextStack
        Apply-Indent -Text $fragmentText -Indent $indent
    })
}

$generatedMovie = Expand-Includes -Path $TemplatePath
$generatedMovie = ($generatedMovie.TrimEnd("`r", "`n") -replace "`r?`n", "`r`n") + "`r`n"
Assert-Xml -Content $generatedMovie -Path $OutputPath

$outputDir = Split-Path -Parent $OutputPath
if (!(Test-Path -LiteralPath $outputDir -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

[System.IO.File]::WriteAllText($OutputPath, $generatedMovie, $Utf8NoBom)
Write-Output "Generated ${OutputPath}"
