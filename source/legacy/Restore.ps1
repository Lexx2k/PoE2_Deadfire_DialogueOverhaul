param([string]$GameRoot='C:\Steam\steamapps\common\Pillars of Eternity II')
$ErrorActionPreference='Stop'
if(Get-Process PillarsOfEternityII -ErrorAction SilentlyContinue){throw 'Close Deadfire before restoring.'}
$backup=Join-Path $GameRoot 'DialogueOverhaulBackup'
$state=Get-Content -Raw -LiteralPath (Join-Path $backup 'install-state.json') | ConvertFrom-Json
$managed=Join-Path $GameRoot 'PillarsOfEternityII_Data\Managed'
$original=Join-Path $backup 'Assembly-CSharp.dll'
if((Get-FileHash -LiteralPath $original).Hash -ne $state.originalSha256){throw 'Original backup hash mismatch'}
foreach($file in $state.files){
 $installed=Join-Path $managed $file.name
 if((Get-FileHash -LiteralPath $installed).Hash -ne $file.sha256){throw ('Installed file changed outside this mod; preserve it before restoring: '+$file.name)}
}
Copy-Item -LiteralPath $original -Destination (Join-Path $managed 'Assembly-CSharp.dll') -Force
if((Get-FileHash -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll')).Hash -ne $state.originalSha256){throw 'Restoration verification failed'}
Remove-Item -LiteralPath (Join-Path $managed 'Deadfire.DialogueOverhaul.dll')
Write-Host 'Original game assembly restored and verified. Backup retained.'
