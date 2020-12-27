param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",    
    [string]$OutputDir = (Join-Path $PWD $Configuration),
    [string]$TempDir = (Join-Path $env:TEMP "PlayniteBuild"),    
    [string]$ToolboxPath = "e:\Devel\Playnite\source\Tools\Playnite.Toolbox\bin\x86\Debug\Toolbox.exe"
)

$ErrorActionPreference = "Break"
& ..\PlayniteRepo\build\common.ps1

if (Test-Path $OutputDir)
{
    Remove-Item $OutputDir -Recurse -Force
}

$solutionDir = Join-Path $pwd "..\source"
$projectFiles = Get-ChildItem "..\source\" -Filter "*.csproj" -Recurse
$msbuildpath = Get-MsBuildPath
Invoke-Nuget "restore `"..\source\PlayniteExtensions.sln`" -SolutionDirectory `"$solutionDir`""    

foreach ($projectFile in $projectFiles)
{
    if ($projectFile.FullName.Contains(".Tests"))
    {
        continue
    }

    $projectDir = Split-Path $projectFile -Parent
    $addonManifest = Get-Content (Join-Path $projectDir "extension.yaml") | ConvertFrom-Yaml
    $buildDir = Join-Path $OutputDir $addonManifest.Id
    
    $arguments = "-p:OutDir=`"$buildDir`";Configuration=$configuration;AllowedReferenceRelatedFileExtensions=none `"$projectFile`""
    $compilerResult = StartAndWait $msbuildPath $arguments
    if ($compilerResult -ne 0)
    {
        throw "Build failed."
    }

    StartAndWait $ToolboxPath "pack `"$buildDir`" `"$OutputDir`""
}