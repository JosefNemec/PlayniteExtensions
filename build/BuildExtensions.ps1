#Requires -Modules powershell-yaml

param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",    
    [string]$OutputDir = (Join-Path $PWD $Configuration),
    [string]$TempDir = (Join-Path $env:TEMP "PlayniteBuild"),    
    [string]$ToolboxPath = "e:\Devel\Playnite\source\Tools\Playnite.Toolbox\bin\x86\Debug\Toolbox.exe",
    [switch]$SkipExtensions,
    [switch]$SkipThemes
)

$ErrorActionPreference = "Stop"
& ..\PlayniteRepo\build\common.ps1

if (Test-Path $OutputDir)
{
    Remove-Item $OutputDir -Recurse -Force
}

$allPassed = $true

if (!$SkipExtensions)
{
    $solutionDir = Join-Path $pwd "..\source" 
    $msbuildpath = Get-MsBuildPath
    Invoke-Nuget "restore `"..\source\PlayniteExtensions.sln`" -SolutionDirectory `"$solutionDir`""  

    foreach ($extensionMan in (Get-ChildItem "..\source\" -Filter "extension.yaml" -Recurse))
    {
        if ($extensionMan.FullName.Contains('\bin\'))
        {
            continue
        }

        $extDir = Split-Path $extensionMan -Parent
        $projectFile = Get-ChildItem $extDir -Filter "*.csproj" | Select-Object -First 1

        if ($projectFile)
        {
            if ($projectFile.FullName.Contains(".Tests") -or $projectFile.FullName.Contains(".Common"))
            {
                continue
            }

            $addonManifest = Get-Content (Join-Path $extDir "extension.yaml") | ConvertFrom-Yaml
            $buildDir = Join-Path $OutputDir $addonManifest.Id
            $arguments = "-p:OutDir=`"$buildDir`";Configuration=$configuration;AllowedReferenceRelatedFileExtensions=none `"$projectFile`""
            $compilerResult = StartAndWait $msbuildPath $arguments
            if ($compilerResult -ne 0)
            {
                $allPassed = $false
            }
            else
            {                
                $locOut =  Join-Path $buildDir "Localization\"
                if (Test-Path -LiteralPath $locOut)
                {
                    Copy-Item "..\source\Localization\*.*" $locOut
                }

                if ((StartAndWait $ToolboxPath "pack `"$buildDir`" `"$OutputDir`"") -ne 0)
                {
                    $allPassed = $false
                }
            }
        }
        else
        {
            if ((StartAndWait $ToolboxPath "pack `"$extDir`" `"$OutputDir`"") -ne 0)
            {
                $allPassed = $false
            }         
        }
    }
}

if (!$SkipThemes)
{
    foreach ($themeMan in (Get-ChildItem "..\source\Themes\" -Filter "theme.yaml" -Recurse))
    {
        $themeDir = Split-Path $themeMan -Parent
        $addonManifest = Get-Content (Join-Path $themeDir "theme.yaml") | ConvertFrom-Yaml 
        if ((StartAndWait $ToolboxPath "pack `"$themeDir`" `"$OutputDir`"") -ne 0)
        {
            $allPassed = $false
        }   
    }
}

if (!$allPassed)
{
    Write-Error "Some add-ons failed to build."
}