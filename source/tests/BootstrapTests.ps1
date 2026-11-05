param([string]$PackageRoot=(Split-Path (Split-Path $PSScriptRoot)))
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $PackageRoot 'PillarsOfEternityII_Data\DialogOverhaul\BepInEx\core\Deadfire.DialogOverhaul.Bootstrap.dll'))
$old='UnityEngine.dll';$new='UnityEngine.CoreModule.dll'
function Select-Config($lines,$target){return ([Doorstop.BootstrapConfig]::SelectEngine([string[]]$lines,$target) -join "`n")}
$base=@('[Logging.Disk]','Enabled = true','[Preloader.Entrypoint]','Assembly = UnityEngine.dll','Type = Application')
if((Select-Config $base $old) -ne ($base -join "`n")){throw 'Retail config changed'}
$beta=Select-Config $base $new
if($beta -notmatch 'Assembly = UnityEngine.CoreModule.dll'){throw 'Beta module not selected'}
if((Select-Config ($beta -split "`n") $old) -ne ($base -join "`n")){throw 'Return to retail failed'}
if((Select-Config @() $new) -notmatch '\[Preloader.Entrypoint\]\nAssembly = UnityEngine.CoreModule.dll'){throw 'Empty config failed'}
if((Select-Config @('[Preloader.Entrypoint]','Type = Application','[Logging]','Enabled = true') $new) -notmatch 'Assembly = UnityEngine.CoreModule.dll\n\[Logging\]'){throw 'Missing entry field failed'}
$custom=@('[Preloader.Entrypoint]','Assembly = Custom.dll')
if((Select-Config $custom $new) -ne ($custom -join "`n")){throw 'Custom configuration was overwritten'}
if((Select-Config @('[Other]','Assembly = Whatever.dll') $new) -notmatch 'Assembly = Whatever.dll'){throw 'Unrelated section changed'}
if((Select-Config ($beta -split "`n") $new) -ne $beta){throw 'Config migration not idempotent'}
Write-Host '8 retail/beta bootstrap configuration checks passed.'
