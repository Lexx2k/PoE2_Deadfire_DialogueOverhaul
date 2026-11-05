param(
 [string]$GameRoot='C:\Steam\steamapps\common\Pillars of Eternity II',
 [string]$OutputDirectory=(Split-Path $PSScriptRoot),
 [string]$ReferenceManagedDirectory='',
 [switch]$BootstrapOnly
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($OutputDirectory)
$managed=Join-Path $GameRoot 'PillarsOfEternityII_Data\Managed'
if($ReferenceManagedDirectory){$managed=[IO.Path]::GetFullPath($ReferenceManagedDirectory)}
if(-not $BootstrapOnly -and -not $ReferenceManagedDirectory -and (Test-Path -LiteralPath (Join-Path $managed 'UnityEngine.CoreModule.dll'))){throw 'To rebuild a shared retail/beta interface, pass -ReferenceManagedDirectory pointing to retail Managed references. Use -BootstrapOnly to update the compatibility bootstrap while retaining the existing shared interface/plugin.'}
$core=Join-Path $root 'PillarsOfEternityII_Data\DialogOverhaul\BepInEx\core'
if(-not(Test-Path -LiteralPath (Join-Path $core 'BepInEx.dll'))){throw 'Pinned loader dependencies are missing. Keep the repository data folder alongside source.'}
$sdk=& dotnet --list-sdks | Select-Object -Last 1
$compiler=Join-Path ($sdk -replace '^([^ ]+) \[([^\]]+)\]$','$2\$1') 'Roslyn\bincore\csc.dll'
$helperDir=Join-Path $root 'PillarsOfEternityII_Data\Managed'
$pluginDir=Join-Path $root 'PillarsOfEternityII_Data\DialogOverhaul\BepInEx\plugins\DeadfireDialogOverhaul'
New-Item -ItemType Directory -Force $helperDir,$pluginDir | Out-Null
$helper=Join-Path $helperDir 'Deadfire.DialogOverhaul.dll'
if(-not $BootstrapOnly){
$common=@($compiler,'/nologo','/target:library','/langversion:5','/nostdlib+','/noconfig','/optimize+','/debug-')
$refs=@('mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','UnityEngine.UI.dll') | ForEach-Object {'/r:'+(Join-Path $managed $_)}
& dotnet @common @refs ('/out:'+$helper) @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
if($LASTEXITCODE -ne 0){throw 'Interface compilation failed'}
$plugin=Join-Path $pluginDir 'Deadfire.DialogOverhaul.Runtime.dll'
$runtimeRefs=@(('/r:'+$helper),('/r:'+(Join-Path $core 'BepInEx.dll')),('/r:'+(Join-Path $core '0Harmony.dll')))
& dotnet @common @refs @runtimeRefs ('/out:'+$plugin) (Join-Path $PSScriptRoot 'runtime\RuntimePlugin.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime plugin compilation failed'}
}elseif(-not(Test-Path -LiteralPath $helper)){throw 'Existing shared interface payload is missing.'}
& (Join-Path $PSScriptRoot 'Build-Bootstrap.ps1') -OutputDirectory $root
$payload=Join-Path $root 'PillarsOfEternityII_Data'
$files=@(Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object {$_.Name -notin @('LogOutput.log','BepInEx.cfg')} | ForEach-Object {
 @{path=$_.FullName.Substring($root.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
foreach($name in @('version.dll','doorstop_config.ini','.doorstop_version')){
 $file=Join-Path $PSScriptRoot ('loader\'+$name)
 $files+=@{path=('source/loader/'+$name);sha256=(Get-FileHash -LiteralPath $file).Hash}
}
@{format=3;runtime='BepInEx 5.4.23.2 / HarmonyX';knownGameAssemblies=@{retail='3C3749B24078611189078032EECFA617304011D01E846E1AF3385CE0B29C722A';beta='D35BBA8DC6B6F2579FAD9ECAE076ACA644AFDB68C4374C4D655938192EA912AF'};files=$files} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-manifest.json') -Encoding UTF8
if(Test-Path -LiteralPath (Join-Path $helperDir 'Assembly-CSharp.dll')){throw 'Runtime payload must not contain Assembly-CSharp.dll. Remove the legacy payload first.'}
Write-Host 'Runtime payload verified and compatibility bootstrap built. No game assembly was patched or copied into the payload.'

