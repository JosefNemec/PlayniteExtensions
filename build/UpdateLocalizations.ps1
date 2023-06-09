#Requires -Version 7

param(
    [string]$AccessToken
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName "PresentationFramework"

$locDir = Join-Path $pwd "..\source\Localization"
$urlRoot = "https://crowdin.com/api/v2"
$playnitePrjId = 345875
$requestHeaders = @{
    "Authorization" = "Bearer $AccessToken"
}

$locProgressData = @{}
$locProgress = Invoke-RestMethod -Headers $requestHeaders -Uri "$urlRoot/projects/$playnitePrjId/languages/progress?limit=100"
foreach ($lng in $locProgress.data)
{
    if ($lng.data.languageId -eq "en")
    {
        continue
    }

    $locProgressData.Add($lng.data.languageId, $lng.data.translationProgress);
    $locDownloadData = Invoke-RestMethod -Method Post -Headers $requestHeaders -Uri "$urlRoot/projects/$playnitePrjId/translations/builds/files/33" `
                                         -Body "{`"targetLanguageId`":`"$($lng.data.languageId)`"}" -ContentType "application/json"
    
    $tempFile = Join-Path $locDir "temp.xaml"
    Remove-Item $tempFile -EA 0
    $locDownload = Invoke-WebRequest -Uri $locDownloadData.data.url -OutFile $tempFile -PassThru
    $locDownload.Headers["Content-Disposition"][0] -match '"(.+)"' | Out-Null
    $fileName = $Matches[1]
    
    Move-Item $tempFile (Join-Path $locDir $fileName) -Force
}

$allOk = $true
foreach ($locFile in (Get-ChildItem $locDir -Filter "*.xaml"))
{
    $stream = New-Object "System.IO.StreamReader" $locFile.FullName

    try
    {
        $xaml = [System.Windows.Markup.XamlReader]::Load($stream.BaseStream)
        Write-Host "$($locFile.Name)...OK" -ForegroundColor Green
    }
    catch
    {
        $allOk = $false
        Write-Host "$($locFile.Name)...FAIL" -ForegroundColor Red
        Write-Host $_.Exception.InnerException.Message -ForegroundColor Red
    }
    finally
    {
        $stream.Dispose()
    }
}

if (-not $allOk)
{
    throw "Some localization files failed verification."
}