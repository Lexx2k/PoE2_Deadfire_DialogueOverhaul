param([string]$GameRoot='C:\Steam\steamapps\common\Pillars of Eternity II')
$ErrorActionPreference='Stop'
if(Get-Process PillarsOfEternityII -ErrorAction SilentlyContinue){throw 'Close Deadfire before installing.'}
$managed=Join-Path $GameRoot 'PillarsOfEternityII_Data\Managed'
$payload=Join-Path $PSScriptRoot 'PillarsOfEternityII_Data\Managed'
$manifest=Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') | ConvertFrom-Json
$backup=Join-Path $GameRoot 'DialogueOverhaulBackup'
$assembly=Join-Path $managed 'Assembly-CSharp.dll'
$statePath=Join-Path $backup 'install-state.json'
$current=(Get-FileHash -LiteralPath $assembly).Hash
foreach($file in $manifest.files){if((Get-FileHash -LiteralPath (Join-Path $payload $file.name)).Hash -ne $file.sha256){throw ('Payload hash mismatch: '+$file.name)}}
if($current -ne $manifest.originalSha256){
 if(-not(Test-Path -LiteralPath $statePath)){throw 'Unknown installed assembly. No files changed.'}
 $previous=Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
 if($current -ne ($previous.files | Where-Object name -eq 'Assembly-CSharp.dll').sha256){throw 'Installed assembly changed outside this mod. No files changed.'}
}
$original=Join-Path $backup 'Assembly-CSharp.dll'
if(Test-Path -LiteralPath $original){
 if((Get-FileHash -LiteralPath $original).Hash -ne $manifest.originalSha256){throw 'Backup hash mismatch'}
}else{
 if($current -ne $manifest.originalSha256){throw 'Pristine backup missing'}
 if(Test-Path -LiteralPath (Join-Path $managed 'Deadfire.DialogueOverhaul.dll')){throw 'Existing helper without an installation record. Preserve it before installing.'}
 New-Item -ItemType Directory -Path $backup -Force | Out-Null
 Copy-Item -LiteralPath $assembly -Destination $original
}
foreach($file in $manifest.files){Copy-Item -LiteralPath (Join-Path $payload $file.name) -Destination (Join-Path $managed $file.name) -Force}
foreach($file in $manifest.files){if((Get-FileHash -LiteralPath (Join-Path $managed $file.name)).Hash -ne $file.sha256){throw ('Installation verification failed: '+$file.name)}}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding UTF8
Write-Host 'Deadfire dialogue prototype installed; both DLL hashes verified. Original retained in DialogueOverhaulBackup.'
