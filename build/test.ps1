$template = @"
AddonId: {0}
Packages:
  - Version: 1.0
    RequiredApiVersion: 5.6.1
    ReleaseDate: 2020-11-18
    PackageUrl: e:\Devel\PlayniteExtensions\build\Release\{1}_1_0.pext
"@

$ErrorActionPreference = "Break"
$manifestDir = "..\manifests"
$addonsDir = "..\..\PlayniteAddonDatabase\addons\extensions"

$projectFiles = Get-ChildItem "..\source\" -Filter "*.csproj" -Recurse
foreach ($projectFile in $projectFiles)
{
    if ($projectFile.FullName.Contains(".Tests") -or $projectFile.FullName.Contains(".Common"))
    {
        continue
    }

    $projectDir = Split-Path $projectFile -Parent
    $addonManifest = Get-Content (Join-Path $projectDir "extension.yaml") | ConvertFrom-Yaml

    $template -f $addonManifest.Id, $addonManifest.Id | Out-File (Join-Path $manifestDir "$($addonManifest.Id).yaml")
    Copy-Item (Join-Path $projectDir "addon.yaml") (Join-Path $addonsDir "$($addonManifest.Id).yaml")
}