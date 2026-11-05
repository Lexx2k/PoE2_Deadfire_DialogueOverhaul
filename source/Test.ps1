param([string]$GameRoot='C:\Steam\steamapps\common\Pillars of Eternity II')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$work=Join-Path ([IO.Path]::GetTempPath()) ('deadfire-source-tests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$sdk=& dotnet --list-sdks | Select-Object -Last 1
$compiler=Join-Path ($sdk -replace '^([^ ]+) \[([^\]]+)\]$','$2\$1') 'Roslyn\bincore\csc.dll'
$refRoot=Join-Path (Split-Path (Split-Path (Split-Path (Split-Path (Split-Path $compiler))))) 'packs\Microsoft.NETCore.App.Ref'
$refVersion=Get-ChildItem -LiteralPath $refRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
$refs=Get-ChildItem -LiteralPath (Join-Path $refVersion.FullName 'ref\net10.0') -Filter '*.dll' | ForEach-Object {'/r:'+$_.FullName}
function Runner($name,$sources){
 $output=Join-Path $work ($name+'.dll')
 & dotnet $compiler /nologo /target:exe /nostdlib+ /noconfig /nullable:enable /warn:0 ('/out:'+$output) @refs @sources | Out-Host
 if($LASTEXITCODE -ne 0){throw ('Test compilation failed: '+$name)}
 '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}' | Set-Content (Join-Path $work ($name+'.runtimeconfig.json'))
 return $output
}
$common=Join-Path $PSScriptRoot 'tests\GlobalUsings.cs'
$sources=@((Join-Path $PSScriptRoot 'tests\Program.cs'),$common)
foreach($name in @('Formatting','InlineMarkup','MethodDispatch','TranscriptLayout','DialogueCameraLayout')){$sources+=Join-Path $PSScriptRoot ('src\'+$name+'.cs')}
$formatting=Runner 'FormattingTests' $sources
& dotnet $formatting
if($LASTEXITCODE -ne 0){throw 'Formatting/layout tests failed'}
$runtime=Runner 'RuntimeTests' @((Join-Path $PSScriptRoot 'tests\RuntimeTests.cs'),$common)
& dotnet $runtime $root $GameRoot
if($LASTEXITCODE -ne 0){throw 'Runtime transpiler tests failed'}
& (Join-Path $PSScriptRoot 'tests\BootstrapTests.ps1') -PackageRoot $root
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest.files){
 if((Get-FileHash -LiteralPath (Join-Path $root $file.path)).Hash -ne $file.sha256){throw ('Payload hash mismatch: '+$file.path)}
}
if(Test-Path -LiteralPath (Join-Path $root 'PillarsOfEternityII_Data\Managed\Assembly-CSharp.dll')){throw 'Runtime payload contains a replacement game assembly'}
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'loader\doorstop_config.ini') -Raw
if($config -notmatch '(?im)^target_assembly=(.+)$'){throw 'Bootstrap entry point is missing'}
$entry=$Matches[1].Trim()
if(-not(Test-Path -LiteralPath (Join-Path $root $entry))){throw 'Manual-copy bootstrap target does not exist in the data payload'}
Write-Host 'Payload hashes and manual-copy bootstrap target verified; no replacement game assembly.'

