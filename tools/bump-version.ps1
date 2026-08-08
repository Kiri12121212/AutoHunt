<#
.SYNOPSIS
  Bump HuntTrainAuto plugin version in csproj + Dalamud manifest JSON.

.DESCRIPTION
  Single source: HuntTrainAuto/HuntTrainAuto.csproj <Version>.
  Keeps HuntTrainAuto/HuntTrainAuto.json AssemblyVersion in sync.
  Creates a git tag only for major bumps (unless -NoTag).

.EXAMPLE
  powershell -File tools/bump-version.ps1 -Bump patch
  powershell -File tools/bump-version.ps1 -Bump minor
  powershell -File tools/bump-version.ps1 -Bump major
#>
param(
	[ValidateSet('patch', 'minor', 'major')]
	[string] $Bump = 'patch',

	[switch] $NoTag,

	[string] $RepoRoot = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
	$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$csprojPath = Join-Path $RepoRoot 'HuntTrainAuto\HuntTrainAuto.csproj'
$jsonPath = Join-Path $RepoRoot 'HuntTrainAuto\HuntTrainAuto.json'

if (-not (Test-Path -LiteralPath $csprojPath)) {
	throw "csproj not found: $csprojPath"
}
if (-not (Test-Path -LiteralPath $jsonPath)) {
	throw "manifest not found: $jsonPath"
}

$csproj = Get-Content -LiteralPath $csprojPath -Raw
if ($csproj -notmatch '<Version>([^<]+)</Version>') {
	throw "No <Version> element in $csprojPath"
}

$oldText = $Matches[1].Trim()
try {
	$old = [version]$oldText
}
catch {
	throw "Invalid Version '$oldText' in $csprojPath"
}

$major = $old.Major
$minor = $old.Minor
$build = [Math]::Max($old.Build, 0)
$revision = [Math]::Max($old.Revision, 0)

switch ($Bump) {
	'major' {
		$major++
		$minor = 0
		$build = 0
		$revision = 0
	}
	'minor' {
		$minor++
		$build = 0
		$revision = 0
	}
	'patch' {
		# Four-part convention: patch bumps Build (0.1.0.0 -> 0.1.1.0)
		$build++
		$revision = 0
	}
}

$newText = '{0}.{1}.{2}.{3}' -f $major, $minor, $build, $revision

$csprojNew = [regex]::Replace(
	$csproj,
	'<Version>[^<]+</Version>',
	"<Version>$newText</Version>",
	1)
if (($csprojNew -eq $csproj) -and ($oldText -ne $newText)) {
	throw 'Failed to rewrite csproj Version'
}
[System.IO.File]::WriteAllText($csprojPath, $csprojNew)

$json = Get-Content -LiteralPath $jsonPath -Raw
if ($json -match '"AssemblyVersion"\s*:\s*"[^"]*"') {
	$jsonNew = [regex]::Replace(
		$json,
		'"AssemblyVersion"\s*:\s*"[^"]*"',
		('"AssemblyVersion": "{0}"' -f $newText),
		1)
}
else {
	if ($json -match '"InternalName"\s*:\s*"[^"]*"\s*,') {
		$jsonNew = [regex]::Replace(
			$json,
			'("InternalName"\s*:\s*"[^"]*"\s*,)',
			('${1}' + "`r`n    `"AssemblyVersion`": `"$newText`","),
			1)
	}
	else {
		throw 'Cannot find InternalName in manifest to insert AssemblyVersion'
	}
}
[System.IO.File]::WriteAllText($jsonPath, $jsonNew)

Write-Host "Version: $oldText -> $newText"

if (($Bump -eq 'major') -and (-not $NoTag)) {
	$tag = "v$newText"
	Write-Host "Creating git tag $tag (major bump)"
	& git -C $RepoRoot tag $tag
	if ($LASTEXITCODE -ne 0) {
		throw "git tag $tag failed"
	}
}
else {
	Write-Host 'No git tag (only major bumps are tagged).'
}
