[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\artifacts\nuget'),

    [string] $Configuration = 'Release',

    [string] $Version
)

$solutionPath = Join-Path $PSScriptRoot '..\EnterpriseUI.slnx'
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)

New-Item -ItemType Directory -Path $resolvedOutputPath -Force | Out-Null

$packArguments = @(
    'pack', $solutionPath,
    '--configuration', $Configuration,
    '--output', $resolvedOutputPath,
    '--nologo'
)

if ($Version) {
    $packArguments += "/p:PackageVersion=$Version"
}

dotnet @packArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}