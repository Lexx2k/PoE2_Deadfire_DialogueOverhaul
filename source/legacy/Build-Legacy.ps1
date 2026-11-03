param([string]$GameRoot='C:\Steam\steamapps\common\Pillars of Eternity II', [string]$OutputDirectory='', [switch]$DiagnosticMode)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$package=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $root 'dist'}
$managed=Join-Path $GameRoot 'PillarsOfEternityII_Data\Managed'
$payload=Join-Path $package 'PillarsOfEternityII_Data\Managed'
$original=Join-Path $root 'originals\Assembly-CSharp.dll'
$expected='3C3749B24078611189078032EECFA617304011D01E846E1AF3385CE0B29C722A'
New-Item -ItemType Directory -Force -Path $payload,(Split-Path $original) | Out-Null
if(-not(Test-Path -LiteralPath $original)){
 $installed=Join-Path $managed 'Assembly-CSharp.dll'
 if((Get-FileHash -LiteralPath $installed).Hash -ne $expected){throw 'Unknown assembly. Obtain the pristine supported build before patching.'}
 Copy-Item -LiteralPath $installed -Destination $original
}
if((Get-FileHash -LiteralPath $original).Hash -ne $expected){throw 'Original backup hash mismatch'}
$compiler=Join-Path ((& dotnet --list-sdks | Select-Object -Last 1) -replace '^([^ ]+) \[([^\]]+)\]$','$2\$1') 'Roslyn\bincore\csc.dll'
$helper=Join-Path $payload 'Deadfire.DialogueOverhaul.dll'
$compile=@($compiler,'/nologo','/target:library','/langversion:5','/nostdlib+','/noconfig','/optimize+','/debug-',('/out:'+$helper))
foreach($name in @('mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','UnityEngine.UI.dll')){$compile+=('/r:'+(Join-Path $managed $name))}
if($DiagnosticMode){$compile+=(Join-Path $root 'diagnostics\Hooks.cs')}
else{$compile+=@(Get-ChildItem -LiteralPath (Join-Path (Split-Path $root) 'src') -Filter '*.cs' | ForEach-Object FullName)}
& dotnet @compile
if($LASTEXITCODE -ne 0){throw 'Helper compilation failed'}
# Compile the offline utility directly: no NuGet restore or user-level config.
$refRoot=Join-Path (Split-Path (Split-Path (Split-Path (Split-Path (Split-Path $compiler))))) 'packs\Microsoft.NETCore.App.Ref'
$refVersion=Get-ChildItem -LiteralPath $refRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
$refs=Join-Path $refVersion.FullName 'ref\net10.0'
$cecil='D:\tools\Unity\6000.0.85f1\Editor\Data\Managed\Unity.Cecil.dll'
$bin=Join-Path $root 'patcher\bin'; New-Item -ItemType Directory -Force -Path $bin | Out-Null
$toolArgs=@($compiler,'/nologo','/target:exe','/nostdlib+','/noconfig','/nullable:enable',('/out:'+(Join-Path $bin 'Patcher.dll')),('/r:'+$cecil))
$toolArgs+=@(Get-ChildItem -LiteralPath $refs -Filter '*.dll' | ForEach-Object { '/r:'+$_.FullName })
$toolArgs+=@((Join-Path $root 'patcher\Program.cs'),(Join-Path $root 'patcher\GlobalUsings.cs'))
& dotnet @toolArgs
if($LASTEXITCODE -ne 0){throw 'Patcher compilation failed'}
Copy-Item -LiteralPath $cecil -Destination (Join-Path $bin 'Unity.Cecil.dll') -Force
'{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}' | Set-Content -LiteralPath (Join-Path $bin 'Patcher.runtimeconfig.json')
& dotnet (Join-Path $bin 'Patcher.dll') $original $helper (Join-Path $payload 'Assembly-CSharp.dll') $managed | Set-Content -LiteralPath (Join-Path $package 'patch-evidence.json') -Encoding UTF8
if($LASTEXITCODE -ne 0){throw 'Patch failed'}
$files=@('Assembly-CSharp.dll','Deadfire.DialogueOverhaul.dll') | ForEach-Object { [pscustomobject]@{name=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $payload $_)).Hash} }
@{originalSha256=$expected;files=$files} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $package 'manifest.json') -Encoding UTF8
Write-Host "Built and structurally verified: $payload"

