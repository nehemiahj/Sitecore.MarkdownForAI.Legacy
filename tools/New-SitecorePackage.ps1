<#
.SYNOPSIS
  Builds a Sitecore package (.zip for the Installation Wizard) from Sitecore CLI YAML items and files,
  without a running Sitecore instance.

.DESCRIPTION
  Writes the same layout Sitecore's Package Designer writes: an outer zip holding package.zip, which
  holds installer/project, metadata/*, items/<db><path>/<id>/<lang>/<version>/xml, files/<path> and a
  properties/... entry per item and file. Each item's install mode (Overwrite, Merge, Skip) comes from
  the longest matching path in -ItemModes.

  Supports the YAML the Sitecore CLI writes for items: ID, Parent, Template, Path, SharedFields,
  Languages (unversioned Fields and Versions). Block values whose lines are all IDs are multilists
  and are stored pipe separated, as Sitecore stores them.

.EXAMPLE
  ./tools/New-SitecorePackage.ps1 -ItemsPath serialization/items -OutputPath dist/My.Module-1.0.0.zip `
      -Name My.Module -Version 1.0.0 -Author 'Jane Doe' `
      -Files @(@{ Source = 'bin/My.Module.dll'; Target = 'bin/My.Module.dll' }) `
      -ItemModes @(@{ Path = '/sitecore/templates/Modules'; Mode = 'Skip' }, @{ Path = '/sitecore/templates/Modules/My Module'; Mode = 'Merge' })
#>
param(
    [Parameter(Mandatory)] [string]$ItemsPath,
    [Parameter(Mandatory)] [string]$OutputPath,
    [Parameter(Mandatory)] [string]$Name,
    [Parameter(Mandatory)] [string]$Version,
    [string]$Author = '',
    [string]$Publisher = '',
    [string]$Readme = '',
    [string]$Database = 'master',
    [object[]]$Files = @(),
    [object[]]$ItemModes = @()
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

function ConvertFrom-YamlScalar([string]$raw) {
    $raw = $raw.Trim()
    if ($raw.Length -ge 2 -and $raw.StartsWith('"') -and $raw.EndsWith('"')) {
        $inner = $raw.Substring(1, $raw.Length - 2)
        return [regex]::Replace($inner, '\\(.)', { param($m) switch ($m.Groups[1].Value) { 'n' { "`n" } 't' { "`t" } default { $m.Groups[1].Value } } })
    }
    if ($raw.Length -ge 2 -and $raw.StartsWith("'") -and $raw.EndsWith("'")) {
        return $raw.Substring(1, $raw.Length - 2).Replace("''", "'")
    }
    return $raw
}

function Read-ItemYaml([string]$file) {
    $lines = [IO.File]::ReadAllLines($file)
    $item = @{ Shared = New-Object System.Collections.Generic.List[object]; Languages = [ordered]@{} }
    $list = $null; $field = $null; $lang = $null
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^(ID|Parent|Template|Path):\s*(.*)$') { $item[$Matches[1]] = ConvertFrom-YamlScalar $Matches[2]; continue }
        if ($line -match '^SharedFields:\s*$') { $list = $item.Shared; continue }
        if ($line -match '^- Language:\s*(.*)$') {
            $lang = @{ Unversioned = New-Object System.Collections.Generic.List[object]; Versions = [ordered]@{} }
            $item.Languages[(ConvertFrom-YamlScalar $Matches[1])] = $lang
            continue
        }
        if ($line -match '^  Fields:\s*$') { $list = $lang.Unversioned; continue }
        if ($line -match '^  - Version:\s*(\d+)\s*$') {
            $list = New-Object System.Collections.Generic.List[object]
            $lang.Versions[$Matches[1]] = $list
            continue
        }
        if ($line -match '^\s*- ID:\s*(.*)$') { $field = @{ ID = (ConvertFrom-YamlScalar $Matches[1]); Value = '' }; $list.Add($field); continue }
        if ($line -match '^\s*Hint:\s*(.*)$') { $field.Hint = ConvertFrom-YamlScalar $Matches[1]; continue }
        if ($line -match '^(\s*)Value:\s*(.*)$') {
            $indent = $Matches[1].Length
            $raw = $Matches[2]
            if ($raw -match '^\|[-+]?\s*$') {
                $block = New-Object System.Collections.Generic.List[string]
                while ($i + 1 -lt $lines.Count -and ($lines[$i + 1].Trim() -eq '' -or ($lines[$i + 1] -match '^(\s*)' -and $Matches[1].Length -gt $indent))) {
                    $i++
                    $block.Add($lines[$i])
                }
                $strip = ($block | Where-Object { $_.Trim() -ne '' } | ForEach-Object { ($_ -replace '^(\s*).*$', '$1').Length } | Measure-Object -Minimum).Minimum
                $values = @($block | ForEach-Object { if ($_.Length -ge $strip) { $_.Substring($strip) } else { '' } })
                while ($values.Count -gt 0 -and $values[-1] -eq '') { $values = @($values[0..($values.Count - 2)]) }
                if ($values.Count -gt 0 -and -not ($values | Where-Object { $_ -notmatch '^\{[0-9A-Fa-f-]{36}\}$' })) {
                    $field.Value = $values -join '|'
                }
                else {
                    $field.Value = $values -join "`r`n"
                }
            }
            else {
                $field.Value = ConvertFrom-YamlScalar $raw
            }
            continue
        }
    }
    return $item
}

function Format-Id([string]$guid) { '{' + $guid.Trim('{', '}').ToUpperInvariant() + '}' }
function Escape-Xml([string]$text) { [Security.SecurityElement]::Escape($text) }

function Get-InstallMode([string]$path) {
    $best = $null
    foreach ($rule in $ItemModes) {
        $p = $rule.Path.TrimEnd('/')
        if (($path -eq $p -or $path.StartsWith($p + '/', [StringComparison]::OrdinalIgnoreCase)) -and ($best -eq $null -or $p.Length -gt $best.Path.Length)) {
            $best = @{ Path = $p; Mode = $rule.Mode }
        }
    }
    if ($best -eq $null) { return 'Merge' }
    return $best.Mode
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

function Add-Entry($archive, [string]$name, [byte[]]$bytes) {
    $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
    $stream = $entry.Open()
    try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
}
function Add-Text($archive, [string]$name, [string]$text, [switch]$Bom) {
    $encoding = if ($Bom) { $utf8Bom } else { $utf8 }
    Add-Entry $archive $name ($encoding.GetPreamble() + $encoding.GetBytes($text))
}

# Templates before the items based on them, and parents before children, so a clean install can
# create every item.
$items = Get-ChildItem $ItemsPath -Recurse -Filter *.yml | ForEach-Object { Read-ItemYaml $_.FullName } |
    Sort-Object { if ($_.Path -like '/sitecore/templates/*') { 0 } else { 1 } }, { ($_.Path -split '/').Count }, { $_.Path }

$inner = New-Object IO.MemoryStream
$archive = New-Object IO.Compression.ZipArchive($inner, [IO.Compression.ZipArchiveMode]::Create, $true)
try {
    Add-Text $archive 'installer/version' '19.00.000000.000000'

    $groups = [ordered]@{}
    foreach ($item in $items) {
        $mode = Get-InstallMode $item.Path
        if (-not $groups.Contains($mode)) { $groups[$mode] = New-Object System.Collections.Generic.List[string] }
        $groups[$mode].Add("/$Database/$(Format-Id $item.ID)/invariant/0")
    }
    $sources = foreach ($mode in $groups.Keys) {
        $merge = if ($mode -eq 'Merge') { 'Merge' } else { 'Undefined' }
        $entries = ($groups[$mode] | ForEach-Object { "        <x-item>$_</x-item>" }) -join "`r`n"
        @"
    <xitems>
      <Entries>
$entries
      </Entries>
      <SkipVersions>False</SkipVersions>
      <Converter>
        <ItemToEntryConverter>
          <Transforms>
            <InstallerConfigurationTransform>
              <Options>
                <BehaviourOptions>
                  <ItemMode>$mode</ItemMode>
                  <ItemMergeMode>$merge</ItemMergeMode>
                </BehaviourOptions>
              </Options>
            </InstallerConfigurationTransform>
          </Transforms>
        </ItemToEntryConverter>
      </Converter>
      <Include />
      <Exclude />
      <Name>Items ($mode)</Name>
    </xitems>
"@
    }
    $fileEntries = ($Files | ForEach-Object { "        <x-item>/$($_.Target.TrimStart('/'))</x-item>" }) -join "`r`n"
    $project = @"
<project>
  <Metadata>
    <metadata>
      <PackageName>$(Escape-Xml $Name)</PackageName>
      <Author>$(Escape-Xml $Author)</Author>
      <Version>$(Escape-Xml $Version)</Version>
      <Revision />
      <License />
      <Comment />
      <Attributes />
      <Readme>$(Escape-Xml $Readme)</Readme>
      <Publisher>$(Escape-Xml $Publisher)</Publisher>
      <PostStep />
      <PackageID />
    </metadata>
  </Metadata>
  <SaveProject>True</SaveProject>
  <Sources>
$($sources -join "`r`n")
    <xfiles>
      <Entries>
$fileEntries
      </Entries>
      <Converter>
        <FileToEntryConverter>
          <Root>/</Root>
          <Transforms />
        </FileToEntryConverter>
      </Converter>
      <Include />
      <Exclude />
      <Name>Files</Name>
    </xfiles>
  </Sources>
  <Converter>
    <TrivialConverter>
      <Transforms />
    </TrivialConverter>
  </Converter>
  <Include />
  <Exclude />
  <Name />
</project>
"@
    Add-Text $archive 'installer/project' $project
    Add-Text $archive 'metadata/sc_author.txt' $Author
    Add-Text $archive 'metadata/sc_name.txt' $Name
    Add-Text $archive 'metadata/sc_publisher.txt' $Publisher
    Add-Text $archive 'metadata/sc_readme.txt' $Readme
    Add-Text $archive 'metadata/sc_version.txt' $Version

    foreach ($item in $items) {
        $mode = Get-InstallMode $item.Path
        $merge = if ($mode -eq 'Merge') { 'Merge' } else { 'Undefined' }
        $itemName = ($item.Path -split '/')[-1]
        $sortorder = ($item.Shared | Where-Object { $_.ID -eq 'ba3f86a2-4a1c-4d78-b63d-91c2779c1b5e' } | Select-Object -First 1).Value
        foreach ($language in $item.Languages.Keys) {
            $lang = $item.Languages[$language]
            foreach ($number in $lang.Versions.Keys) {
                $fields = @()
                $fields += $item.Shared | ForEach-Object { @{ Field = $_; Sharing = 'Shared' } }
                $fields += $lang.Unversioned | ForEach-Object { @{ Field = $_; Sharing = 'Unversioned' } }
                $fields += $lang.Versions[$number] | ForEach-Object { @{ Field = $_; Sharing = 'Versioned' } }
                $created = ($fields | Where-Object { $_.Field.ID -eq '25bed78c-4957-4165-998a-ca1b52f67497' } | Select-Object -First 1).Field.Value

                $xml = New-Object Text.StringBuilder
                [void]$xml.Append("<item name=`"$(Escape-Xml $itemName)`" key=`"$(Escape-Xml $itemName.ToLowerInvariant())`" id=`"$(Format-Id $item.ID)`" tid=`"$(Format-Id $item.Template)`" mid=`"{00000000-0000-0000-0000-000000000000}`" sortorder=`"$sortorder`" language=`"$language`" version=`"$number`" template=`"`" parentid=`"$(Format-Id $item.Parent)`" created=`"$created`"><fields>")
                foreach ($f in $fields) {
                    $key = if ($f.Field.Hint) { $f.Field.Hint.ToLowerInvariant() } else { '' }
                    $content = if ($f.Field.Value -eq '') { '<content />' } else { "<content>$(Escape-Xml $f.Field.Value)</content>" }
                    [void]$xml.Append("<field tfid=`"$(Format-Id $f.Field.ID)`" key=`"$(Escape-Xml $key)`" type=`"`">$content</field>")
                }
                [void]$xml.Append('</fields></item>')

                $entryPath = "items/$Database$($item.Path)/$(Format-Id $item.ID)/$language/$number/xml"
                Add-Text $archive $entryPath $xml.ToString()

                $fieldProperties = ($fields | ForEach-Object { "$(Format-Id $_.Field.ID):$($_.Sharing)" }) -join '|'
                $properties = "database=$Database`r`nid=$(Format-Id $item.ID)`r`nlanguage=$language`r`nversion=$number`r`nrevision=`r`nfieldproperties=$fieldProperties`r`nid_InstallMode=$mode`r`nid_VersionMergeMode=$merge`r`n"
                Add-Text $archive "properties/$entryPath" $properties -Bom
            }
        }
    }

    foreach ($file in $Files) {
        $target = $file.Target.TrimStart('/').Replace('\', '/')
        Add-Entry $archive "files/$target" ([IO.File]::ReadAllBytes((Resolve-Path $file.Source)))
        Add-Text $archive "properties/files/$target" "type=file`r`n" -Bom
    }
}
finally {
    $archive.Dispose()
}

$outputDir = Split-Path $OutputPath -Parent
if ($outputDir) { New-Item -ItemType Directory -Force $outputDir | Out-Null }
if (Test-Path $OutputPath) { Remove-Item $OutputPath -Force }
$outerStream = [IO.File]::Create($OutputPath)
try {
    $outer = New-Object IO.Compression.ZipArchive($outerStream, [IO.Compression.ZipArchiveMode]::Create)
    try { Add-Entry $outer 'package.zip' $inner.ToArray() } finally { $outer.Dispose() }
}
finally {
    $outerStream.Dispose()
    $inner.Dispose()
}

Write-Host "Sitecore package: $OutputPath ($($items.Count) items, $($Files.Count) files)"
