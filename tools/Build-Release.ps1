#Requires -Version 5.1
<#
.SYNOPSIS
Builds the plugin and packages its language files for installation.
.PARAMETER BepInExDirectory
The initialized BepInEx folder in an installed copy of the game.
.PARAMETER OutputDirectory
Destination for the archive and staging files. Defaults to the repository's dist folder.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BepInExDirectory,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Dependencies {
    param([string]$Directory)

    $requiredFiles = @(
        'core\BepInEx.Core.dll'
        'core\BepInEx.Unity.IL2CPP.dll'
        'core\0Harmony.dll'
        'core\Il2CppInterop.Runtime.dll'
        'interop\Assembly-CSharp.dll'
    )
    foreach ($relativePath in $requiredFiles) {
        $path = Join-Path $Directory $relativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing dependency: $relativePath. Start the game with BepInEx IL2CPP first."
        }
    }
}

function Get-ReleaseVersion {
    param([string]$Root)

    $source = Get-Content -LiteralPath (Join-Path $Root 'src\ComputerTycoon.LanguageMod\Plugin.cs') -Raw
    $match = [regex]::Match($source, 'public const string Version = "(\d+\.\d+\.\d+)"')
    if (-not $match.Success) {
        throw 'Cannot read the plugin version.'
    }
    $version = $match.Groups[1].Value
    foreach ($name in @('ComputerTycoon.LanguageMod', 'Localization.Core')) {
        [xml]$project = Get-Content -LiteralPath (Join-Path $Root "src\$name\$name.csproj") -Raw
        if ([string]$project.Project.PropertyGroup.Version -ne $version) {
            throw "Version mismatch in $name.csproj. Expected $version."
        }
    }
    return $version
}

function Read-LanguagePack {
    param([string]$Path)

    $json = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        return ConvertFrom-Json -InputObject $json -AsHashtable
    }

    # Windows PowerShell's ConvertFrom-Json rejects keys such as Name and NAME.
    # The built-in .NET Framework parser keeps these distinct without extra modules.
    Add-Type -AssemblyName System.Web.Extensions
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializer.MaxJsonLength = 16 * 1024 * 1024
    return $serializer.DeserializeObject($json)
}

function Copy-LanguagePacks {
    param(
        [string]$Source,
        [string]$Destination
    )

    $packs = Get-ChildItem -LiteralPath $Source -File -Filter '*.json'
    foreach ($packFile in $packs) {
        # English source keys are case-sensitive (for example, Name and NAME).
        $pack = Read-LanguagePack -Path $packFile.FullName
        if ($pack.id -notmatch '^[a-zA-Z][a-zA-Z0-9-]{0,31}$' -or $pack.id -cne $packFile.BaseName) {
            throw "Language id must be valid and match the filename: $($packFile.Name)"
        }
        # The English file is a source-only translator template.
        if ($pack.id -eq 'en') {
            continue
        }
        Copy-Item -LiteralPath $packFile.FullName -Destination $Destination

        $assets = Join-Path $Source "$($pack.id)-assets"
        if (-not (Test-Path -LiteralPath $assets -PathType Container)) {
            continue
        }
        $assetTarget = Join-Path $Destination "$($pack.id)-assets"
        New-Item -ItemType Directory -Path $assetTarget | Out-Null
        # Include only runtime images, never editor backups or source artwork.
        foreach ($mode in @('vanilla', 'historical', 'simplified')) {
            $image = Join-Path $assets "map-$mode.png"
            if (Test-Path -LiteralPath $image -PathType Leaf) {
                Copy-Item -LiteralPath $image -Destination $assetTarget
            }
        }
    }
}

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$interopRoot = (Resolve-Path -LiteralPath $BepInExDirectory).Path
Assert-Dependencies -Directory $interopRoot
$version = Get-ReleaseVersion -Root $projectRoot
$dist = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $projectRoot 'dist'
} else {
    [IO.Path]::GetFullPath($OutputDirectory)
}
$archive = Join-Path $dist "ComputerTycoon.LanguageMod-$version.zip"
if (Test-Path -LiteralPath $archive) {
    throw "Archive already exists: $archive. Preserve or rename it before rebuilding."
}

$projectFile = Join-Path $projectRoot 'src\ComputerTycoon.LanguageMod\ComputerTycoon.LanguageMod.csproj'
& dotnet build $projectFile -c Release "-p:InteropRoot=$interopRoot"
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

$stage = Join-Path $dist ('stage-' + [guid]::NewGuid().ToString('N'))
$plugin = Join-Path $stage 'BepInEx\plugins\ComputerTycoon.LanguageMod'
$languages = Join-Path $plugin 'Languages'
New-Item -ItemType Directory -Path $languages -Force | Out-Null
$buildOutput = Join-Path $projectRoot 'src\ComputerTycoon.LanguageMod\bin\Release\net6.0'
foreach ($name in @('ComputerTycoon.LanguageMod.dll', 'Localization.Core.dll')) {
    Copy-Item -LiteralPath (Join-Path $buildOutput $name) -Destination $plugin
}
Copy-LanguagePacks -Source (Join-Path $projectRoot 'languages') -Destination $languages
foreach ($name in @('LICENSE', 'NOTICE.md', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $stage
}

# Only a complete ZIP receives the release filename. Keep stage for inspection.
$pendingArchive = Join-Path $dist ('pending-' + [guid]::NewGuid().ToString('N') + '.zip')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $pendingArchive
Move-Item -LiteralPath $pendingArchive -Destination $archive
Write-Output "Release: $archive"
