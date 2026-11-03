param(
 [string]$GameRoot='C:\Steam\steamapps\common\Pillars of Eternity II',
 [string]$OutputDirectory=(Split-Path $PSScriptRoot)
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($OutputDirectory)
$managed=Join-Path $GameRoot 'PillarsOfEternityII_Data\Managed'
$core=Join-Path $root 'PillarsOfEternityII_Data\DialogOverhaul\BepInEx\core'
if(-not(Test-Path -LiteralPath (Join-Path $core 'BepInEx.dll'))){throw 'Pinned loader dependencies are missing. Keep the repository data folder alongside source.'}
$sdk=& dotnet --list-sdks | Select-Object -Last 1
$compiler=Join-Path ($sdk -replace '^([^ ]+) \[([^\]]+)\]$','$2\$1') 'Roslyn\bincore\csc.dll'
$helperDir=Join-Path $root 'PillarsOfEternityII_Data\Managed'
$pluginDir=Join-Path $root 'PillarsOfEternityII_Data\DialogOverhaul\BepInEx\plugins\DeadfireDialogOverhaul'
New-Item -ItemType Directory -Force $helperDir,$pluginDir | Out-Null
$helper=Join-Path $helperDir 'Deadfire.DialogOverhaul.dll'
$common=@($compiler,'/nologo','/target:library','/langversion:5','/nostdlib+','/noconfig','/optimize+','/debug-')
$refs=@('mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','UnityEngine.UI.dll') | ForEach-Object {'/r:'+(Join-Path $managed $_)}
& dotnet @common @refs ('/out:'+$helper) @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
if($LASTEXITCODE -ne 0){throw 'Interface compilation failed'}
$plugin=Join-Path $pluginDir 'Deadfire.DialogOverhaul.Runtime.dll'
$runtimeRefs=@(('/r:'+$helper),('/r:'+(Join-Path $core 'BepInEx.dll')),('/r:'+(Join-Path $core '0Harmony.dll')))
& dotnet @common @refs @runtimeRefs ('/out:'+$plugin) (Join-Path $PSScriptRoot 'runtime\RuntimePlugin.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime plugin compilation failed'}
$payload=Join-Path $root 'PillarsOfEternityII_Data'
$files=@(Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object {$_.Name -notin @('LogOutput.log','BepInEx.cfg')} | ForEach-Object {
 @{path=$_.FullName.Substring($root.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
foreach($name in @('winhttp.dll','doorstop_config.ini','.doorstop_version')){
 $file=Join-Path $PSScriptRoot ('loader\'+$name)
 $files+=@{path=('source/loader/'+$name);sha256=(Get-FileHash -LiteralPath $file).Hash}
}
@{format=2;runtime='BepInEx 5.4.23.2 / HarmonyX';originalSha256='3C3749B24078611189078032EECFA617304011D01E846E1AF3385CE0B29C722A';files=$files} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-manifest.json') -Encoding UTF8
if(Test-Path -LiteralPath (Join-Path $helperDir 'Assembly-CSharp.dll')){throw 'Runtime payload must not contain Assembly-CSharp.dll. Remove the legacy payload first.'}
Write-Host 'Runtime interface/plugin built. No game assembly was patched or copied into the payload.'
