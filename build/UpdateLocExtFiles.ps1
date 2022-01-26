$ErrorActionPreference = 'Stop'

$extensionPaths = @{
    'Amazon' = 'source\Libraries\AmazonGamesLibrary\'
    'BattleNet' = 'source\Libraries\BattleNetLibrary\'
    'Bethesda' = 'source\Libraries\BethesdaLibrary\'
    'Epic' = 'source\Libraries\EpicLibrary\'
    'GOG' = 'source\Libraries\GogLibrary\'
    'Humble' = 'source\Libraries\HumbleLibrary\'
    'itch.io' = 'source\Libraries\ItchioLibrary\'
    'Origin' = 'source\Libraries\OriginLibrary\'
    'Rockstar' = 'source\Libraries\RockstarLibrary\'
    'Steam' = 'source\Libraries\SteamLibrary\'
    'Ubisoft' = 'source\Libraries\UplayLibrary\'
    'Xbox' = 'source\Libraries\XboxLibrary\'
    'IGDB' = 'source\Metadata\IGDBMetadata\'
    'UniversalSteamMetadata' = 'source\Metadata\UniversalSteamMetadata\'
}

$repoPath = (Resolve-Path (Join-Path $pwd "..")).Path
$langSourceDir = Join-Path $repoPath "source\Localization\"

foreach ($extension in $extensionPaths.Keys)
{
    foreach ($langFile in  (Get-ChildItem -LiteralPath $langSourceDir -Filter "*.xaml"))
    {
        $targetFile = [System.IO.Path]::Combine($repoPath, $extensionPaths[$extension], "Localization", $langFile.Name)
        Copy-Item $langFile.FullName $targetFile        
        [xml]$langXml = Get-Content -LiteralPath $langFile -Raw        
        [xml]$targetXml = Get-Content -LiteralPath $targetFile -Raw
        
        foreach ($node in $targetXml.ResourceDictionary.ChildNodes | ForEach { $_ })
        {
            $node.ParentNode.RemoveChild($node) | Out-Null
        }
        
        $parsingExt = $null
        foreach ($node in $langXml.ResourceDictionary.ChildNodes)
        {
            if ($node.Name -eq "#comment")
            {
                $parsingExt = $node.InnerText.Trim()
                if ($parsingExt -eq $extension)
                {
                    $nodeClone = $targetXml.ImportNode($node, $true)
                    $targetXml.ResourceDictionary.AppendChild($nodeClone) | Out-Null
                }
            }
            elseif ($parsingExt -eq $extension)
            {
                $nodeClone = $targetXml.ImportNode($node, $true)
                $targetXml.ResourceDictionary.AppendChild($nodeClone) | Out-Null
            }
            elseif ($null -eq $parsingExt)
            {           
                $nodeClone = $targetXml.ImportNode($node, $true)
                $nodeClone.SetAttribute('x:Key', "LOC" + $extension.Replace('.', '') + $nodeClone.GetAttribute('x:Key').Substring(3))
                $targetXml.ResourceDictionary.AppendChild($nodeClone) | Out-Null
            }
        }
        
        $targetXml.Save($targetFile)
    }

    $enXmlPath = [System.IO.Path]::Combine($repoPath, $extensionPaths[$extension], "Localization", "en_US.xaml")        
    $locKeysSrc = [System.IO.Path]::Combine($repoPath, $extensionPaths[$extension], "LocalizationKeys.cs")
    [xml]$enXml = Get-Content -LiteralPath $enXmlPath -Raw

@"
///
/// DO NOT MODIFY! Automatically generated via UpdateLocExtFiles.ps1 script.
/// 
namespace System
{
    public static class LOC
    {
"@ | Out-File $locKeysSrc -Encoding utf8

foreach ($node in $enXml.ResourceDictionary.ChildNodes)
{
    if (!$node.Key)
    {
        continue
    }

    if (![string]::IsNullOrEmpty($node.InnerXml))
    {
@"
        /// <summary>
        {0}
        /// </summary>
"@ -f (($node.InnerXml -split "\n") -replace "^", "/// ") | Out-File $locKeysSrc -Encoding utf8 -Append
        "        public const string $($node.Key -replace `"^LOC`", `"`") = `"$($node.Key)`";" `
         | Out-File $locKeysSrc -Encoding utf8 -Append
    }
}

@"
    }
}
"@ | Out-File $locKeysSrc -Encoding utf8 -Append
}