param(
    [Parameter(Mandatory = $true)]
    [string]$BuildDirectory,
    [Parameter(Mandatory = $true)]
    [string]$ArchiveName
)

$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$taskBuildsRoot = [IO.Path]::GetFullPath((Join-Path $taskProjectRoot 'Builds'))
$taskBuildPath = [IO.Path]::GetFullPath($BuildDirectory)
if (-not $taskBuildPath.StartsWith($taskBuildsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'BuildDirectory must be inside this project''s Builds directory.'
}
if ([IO.Path]::GetFileName($ArchiveName) -ne $ArchiveName -or -not $ArchiveName.EndsWith('.zip')) {
    throw 'ArchiveName must be a ZIP filename without a directory.'
}
foreach ($taskRequired in @('TowerDefense.exe', 'TowerDefense_Data', 'UnityPlayer.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskBuildPath $taskRequired))) {
        throw "The player build is incomplete: missing $taskRequired"
    }
}
$taskArchivePath = Join-Path $taskBuildsRoot $ArchiveName
if (Test-Path -LiteralPath $taskArchivePath) {
    throw 'The archive already exists. Choose a new archive name.'
}

$taskLicensesPath = Join-Path $taskBuildPath 'ThirdPartyLicenses'
New-Item -ItemType Directory -Path $taskLicensesPath -Force | Out-Null
$taskLicenses = @{
    'Assets\ThirdParty\Kenney\TowerDefenseKit\License.txt' = 'Kenney-CC0.txt'
    'Assets\ThirdParty\Quaternius\UltimateMonsters\License.txt' = 'Quaternius-CC0.txt'
    'Assets\ThirdParty\Fonts\NotoSansSC\LICENSE' = 'NotoSansSC-OFL.txt'
    'Assets\TextMesh Pro\Fonts\LiberationSans - OFL.txt' = 'LiberationSans-OFL.txt'
}
foreach ($taskSource in $taskLicenses.Keys) {
    Copy-Item -LiteralPath (Join-Path $taskProjectRoot $taskSource) -Destination (Join-Path $taskLicensesPath $taskLicenses[$taskSource])
}
Copy-Item -LiteralPath (Join-Path $taskProjectRoot 'Docs\Windows-Player-Readme.txt') -Destination (Join-Path $taskBuildPath 'README.txt')

# Preserve all runtime files; omit Unity's explicitly marked debugging backups.
$taskPackageItems = Get-ChildItem -LiteralPath $taskBuildPath | Where-Object {
    $_.Name -notlike '*_BackUpThisFolder_ButDontShipItWithYourGame' -and
    $_.Name -notlike '*_BurstDebugInformation_DoNotShip' -and
    $_.Extension -ne '.pdb'
}
Compress-Archive -LiteralPath @($taskPackageItems.FullName) -DestinationPath $taskArchivePath -CompressionLevel Optimal
$taskHash = Get-FileHash -LiteralPath $taskArchivePath -Algorithm SHA256
Write-Output ('Archive: ' + $taskArchivePath)
Write-Output ('Bytes: ' + (Get-Item -LiteralPath $taskArchivePath).Length)
Write-Output ('SHA256: ' + $taskHash.Hash)
