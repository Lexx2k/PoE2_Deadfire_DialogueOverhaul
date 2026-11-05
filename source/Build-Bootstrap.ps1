param([string]$OutputDirectory=(Split-Path $PSScriptRoot),[string]$Net20ReferenceDirectory='')
$ErrorActionPreference='Stop'
if(-not $Net20ReferenceDirectory){
 foreach($candidate in @((Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v2.0.50727'),'D:\tools\Unity\6000.0.85f1\Editor\Data\MonoBleedingEdge\lib\mono\2.0-api')){
  if(Test-Path -LiteralPath (Join-Path $candidate 'mscorlib.dll')){$Net20ReferenceDirectory=$candidate;break}
 }
}
if(-not $Net20ReferenceDirectory){throw 'A .NET 2.0 reference directory is required to build the shared retail/beta bootstrap.'}
$sdk=& dotnet --list-sdks | Select-Object -Last 1
$compiler=Join-Path ($sdk -replace '^([^ ]+) \[([^\]]+)\]$','$2\$1') 'Roslyn\bincore\csc.dll'
$output=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'PillarsOfEternityII_Data\DialogOverhaul\BepInEx\core\Deadfire.DialogOverhaul.Bootstrap.dll'
New-Item -ItemType Directory -Force (Split-Path $output) | Out-Null
& dotnet $compiler /nologo /target:library /langversion:3 /nostdlib+ /noconfig ('/r:'+(Join-Path $Net20ReferenceDirectory 'mscorlib.dll')) ('/r:'+(Join-Path $Net20ReferenceDirectory 'System.dll')) ('/out:'+$output) (Join-Path $PSScriptRoot 'runtime\CompatibleBootstrap.cs')
if($LASTEXITCODE -ne 0){throw 'Compatibility bootstrap compilation failed'}
Write-Host 'Shared bootstrap built against .NET 2.0 for retail and beta Mono runtimes.'
