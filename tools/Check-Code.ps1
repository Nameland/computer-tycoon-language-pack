#Requires -Version 5.1
<#
.SYNOPSIS
Checks source formatting, Python lint, and PowerShell syntax.
.PARAMETER Fix
Applies C# and Python formatting and safe Python lint fixes.
.PARAMETER Python
Python executable with the development requirements installed.
#>
[CmdletBinding()]
param(
    [switch]$Fix,
    [string]$Python = 'python'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Assert-NativeCommand {
    param([string]$Name)

    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE. See the output above."
    }
}

Push-Location $projectRoot
try {
    # Dependencies are installed explicitly; a check never downloads tools.
    $formatCommand = if ($Fix) { 'format' } else { 'check' }
    & dotnet csharpier $formatCommand src tests
    Assert-NativeCommand -Name 'CSharpier'

    $lintArguments = @('-m', 'ruff', 'check', 'tools')
    if ($Fix) {
        $lintArguments += '--fix'
    }
    & $Python @lintArguments
    Assert-NativeCommand -Name 'Ruff lint'

    $formatArguments = @('-m', 'ruff', 'format', 'tools')
    if (-not $Fix) {
        $formatArguments += '--check'
    }
    & $Python @formatArguments
    Assert-NativeCommand -Name 'Ruff format'

    foreach ($script in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File) {
        $tokens = $null
        $parseErrors = $null
        [void][Management.Automation.Language.Parser]::ParseFile(
            $script.FullName, [ref]$tokens, [ref]$parseErrors
        )
        if ($parseErrors.Count -gt 0) {
            throw ($parseErrors | ForEach-Object { $_.ToString() } | Out-String)
        }
    }

    Write-Output 'Code checks passed.'
} finally {
    Pop-Location
}
