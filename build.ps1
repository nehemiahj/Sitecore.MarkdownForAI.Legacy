<#
.SYNOPSIS
  Builds Sitecore.MarkdownForAI.Legacy, runs the tests and writes the release files to dist/.

.DESCRIPTION
  1. Builds the module (net45, Sitecore 8.0 to 10.0).
  2. Runs the .NET tests.
  3. Writes, for version <v> (the <Version> in the .csproj):
     dist/Sitecore.MarkdownForAI.Legacy-<v>.zip        Sitecore package (Installation Wizard): files and items
     dist/Sitecore.MarkdownForAI.Legacy-<v>-files.zip  the same files laid out as the web root, plus the item installer config
     dist/Sitecore.MarkdownForAI.Legacy.<v>.nupkg      NuGet package with the assembly, for solutions built in CI
     dist/files/                                        the unzipped -files.zip

.EXAMPLE
  ./build.ps1
  ./build.ps1 -SkipTests
#>
param(
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src/Sitecore.MarkdownForAI/Sitecore.MarkdownForAI.csproj'
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Push-Location $root
try {
    dotnet build $project -c $Configuration --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    if (-not $SkipTests) {
        dotnet test (Join-Path $root 'tests/Sitecore.MarkdownForAI.Tests') -c $Configuration --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw '.NET tests failed.' }
    }

    $distRoot = Join-Path $root 'dist'
    if (Test-Path $distRoot) { Remove-Item $distRoot -Recurse -Force }
    $dist = Join-Path $distRoot 'files'
    New-Item -ItemType Directory -Force (Join-Path $dist 'bin') | Out-Null

    $binary = Join-Path $root "src/Sitecore.MarkdownForAI/bin/$Configuration/net45"
    Copy-Item (Join-Path $binary 'Sitecore.MarkdownForAI.dll'), (Join-Path $binary 'Sitecore.MarkdownForAI.pdb'), (Join-Path $binary 'Sitecore.MarkdownForAI.xml') (Join-Path $dist 'bin')

    Copy-Item (Join-Path $root 'config/App_Config') $dist -Recurse

    $filesZip = Join-Path $distRoot "Sitecore.MarkdownForAI.Legacy-$version-files.zip"
    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $filesZip
    Write-Host "Files: $filesZip"

    # Sitecore package. It brings the items itself, so the startup installer config is left out.
    # Everything is installed on the CM; CD servers get the same files from your deployment and the
    # items by publishing.
    $packageFiles = @(
        'bin/Sitecore.MarkdownForAI.dll',
        'App_Config/Include/MarkdownForAI/Sitecore.MarkdownForAI.config',
        'App_Config/Include/MarkdownForAI/z.MarkdownForAI.Site.config.example'
    ) | ForEach-Object { @{ Source = (Join-Path $dist $_); Target = $_ } }

    # Merge updates the module's own items on upgrade. Skip leaves the shared parent folder alone.
    $itemModes = @(
        @{ Path = '/sitecore/templates/Modules'; Mode = 'Skip' },
        @{ Path = '/sitecore/templates/Modules/Markdown For AI'; Mode = 'Merge' },
        @{ Path = '/sitecore/system/Modules/Markdown For AI'; Mode = 'Merge' }
    )

    $readme = @"
Sitecore.MarkdownForAI.Legacy ${version}: Markdown versions of Sitecore MVC and WebForms pages for AI agents (/page.md, llms.txt). Sitecore 8.0 to 10.0.

After installing:
1. Rename App_Config/Include/MarkdownForAI/z.MarkdownForAI.Site.config.example to z.MarkdownForAI.Site.config and set your site names and excluded placeholders.
2. Publish /sitecore/templates/Modules/Markdown For AI and /sitecore/system/Modules/Markdown For AI.
3. Deploy the DLL and both config files to your CD servers. The config has no role rules, so it also answers Markdown on this server; remove it here if you do not want that.

Documentation: https://github.com/nehemiahj/Sitecore.MarkdownForAI.Legacy
"@

    & (Join-Path $root 'tools/New-SitecorePackage.ps1') -ItemsPath (Join-Path $root 'package/items') `
        -OutputPath (Join-Path $distRoot "Sitecore.MarkdownForAI.Legacy-$version.zip") `
        -Name 'Sitecore.MarkdownForAI.Legacy' -Version $version -Author 'Nehemiah Jeyakumar' -Publisher 'Nehemiah Jeyakumar' `
        -Readme $readme -Files $packageFiles -ItemModes $itemModes

    dotnet pack $project -c $Configuration --no-build --nologo -v q -o $distRoot
    if ($LASTEXITCODE -ne 0) { throw 'NuGet pack failed.' }
}
finally {
    Pop-Location
}
