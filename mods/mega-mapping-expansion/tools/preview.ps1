param(
    [Parameter(Mandatory=$true)][string]$LevelRoot,
    [ValidateSet('scene','alpha','emission','light','reflection')][string]$Mode,
    [ValidateSet('all','background','world','foreground')][string]$Layer,
    [int]$Screen,
    [ValidateRange(0,462)][float]$PlayerX,
    [ValidateRange(0,334)][float]$PlayerY,
    [float]$Seek = -1,
    [ValidateRange(0,0.1)][float]$Step,
    [Nullable[bool]]$Paused,
    [Nullable[bool]]$Colliders,
    [string]$Select,
    [string[]]$Hidden,
    [switch]$Reload,
    [switch]$Capture,
    [switch]$CaptureScene,
    [switch]$Restart,
    [switch]$TopologyCheck,
    [switch]$ResumeGame,
    [switch]$Status
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $LevelRoot).Path
$scene = Join-Path $root 'props\mega-mapping-expansion'
if (-not (Test-Path -LiteralPath (Join-Path $scene 'scene.xml'))) { throw 'LevelRoot must contain a built Mega Mapping scene' }
if ($Status) {
    Get-Content -LiteralPath (Join-Path $scene 'preview-status.xml')
    return
}
$document = New-Object Xml.XmlDocument
$node = $document.CreateElement('Preview'); $document.AppendChild($node) | Out-Null
$node.SetAttribute('request', [Guid]::NewGuid().ToString('N'))
foreach ($key in @('Mode','Layer','Screen','PlayerX','PlayerY','Seek','Step','Paused','Colliders','Select','Hidden')) {
    if (-not $PSBoundParameters.ContainsKey($key)) { continue }
    $value = $PSBoundParameters[$key]
    if ($key -eq 'Hidden') { $value = $value -join ';' }
    elseif ($key -eq 'Paused' -or $key -eq 'Colliders') { $value = ([bool]$value).ToString().ToLowerInvariant() }
    elseif ($value -is [IFormattable]) { $value = $value.ToString($null,[Globalization.CultureInfo]::InvariantCulture) }
    $node.SetAttribute($key.ToLowerInvariant(),[string]$value)
}
if ($Reload) { $node.SetAttribute('reload','true') }
if ($Capture) { $node.SetAttribute('capture','true') }
if ($CaptureScene) { $node.SetAttribute('capturescene','true') }
if ($Restart) { $node.SetAttribute('restart','true') }
if ($TopologyCheck) { $node.SetAttribute('topologycheck','true') }
if ($ResumeGame) { $node.SetAttribute('resumegame','true') }
$target = Join-Path $scene 'preview-control.xml'
$temporary = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    $document.Save($temporary)
    if (Test-Path -LiteralPath $target) { [IO.File]::Replace($temporary,$target,[NullString]::Value) }
    else { [IO.File]::Move($temporary,$target) }
}
finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
Write-Host 'Preview request sent. Requires a running Jump King -debug instance using this LevelRoot. Use -Status for acknowledgment.'
