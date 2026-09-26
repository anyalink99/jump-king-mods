param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Jump King',
    [switch]$SkipTests,
    [ValidateSet('Fast','Integration','Full')][string]$Tier = 'Fast',
    [string]$CameraAssembly,
    [string]$RuntimeAssembly
)

$ErrorActionPreference = 'Stop'
$ModRoot = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ModRoot '..\..')).Path
$Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$JumpKing = Join-Path $GameDir 'JumpKing.exe'
$MonoGame = Join-Path $GameDir 'MonoGame.Framework.dll'
$SlimDX = Join-Path $GameDir 'SlimDX.dll'
$BuildRoot = Join-Path $RepoRoot 'build\subframe-charge'
$UploadDir = Join-Path $BuildRoot 'UPLOAD_TO_WORKSHOP'
$InternalDir = Join-Path $BuildRoot '_INTERNAL'
$Assembly = Join-Path $InternalDir 'SubframeCharge.Module.dll'
$Runtime = Join-Path $RepoRoot 'build\jk-runtime\UPLOAD_TO_WORKSHOP\JKRuntime.dll'
if ($RuntimeAssembly) { $Runtime = (Resolve-Path -LiteralPath $RuntimeAssembly).Path }
$Harmony = Join-Path $ModRoot 'lib/0Harmony.dll'

foreach ($required in @($Compiler, $JumpKing, $MonoGame, $SlimDX, $Harmony)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required build input is missing: $required"
    }
}

$MonoGameAssembly = [System.Reflection.Assembly]::LoadFrom($MonoGame)
$GameAssembly = [System.Reflection.Assembly]::LoadFrom($JumpKing)
$JumpState = $GameAssembly.GetType('JumpKing.Player.JumpState')
$BodyComp = $GameAssembly.GetType('JumpKing.Player.BodyComp')
$PlayerValues = $GameAssembly.GetType('JumpKing.PlayerValues')
$ControllerManager = $GameAssembly.GetType('JumpKing.Controller.ControllerManager')
$KeyboardPad = $GameAssembly.GetType('JumpKing.Controller.KeyboardPad')
$XboxPad = $GameAssembly.GetType('JumpKing.Controller.XboxPad')
$SaveLube = $GameAssembly.GetType('JumpKing.SaveThread.SaveLube')
$GamePad = $MonoGameAssembly.GetType(
    'Microsoft.Xna.Framework.Input.GamePad')
$GamePadState = $MonoGameAssembly.GetType(
    'Microsoft.Xna.Framework.Input.GamePadState')
$PrivateInstance = [System.Reflection.BindingFlags]'Instance,NonPublic'
$PublicInstance = [System.Reflection.BindingFlags]'Instance,Public'
$PublicStatic = [System.Reflection.BindingFlags]'Static,Public'
$NativeSlimPad = $GameAssembly.GetType('JumpKing.Controller.Slim.SlimPad')
$NativeSlimState = $GameAssembly.GetType('JumpKing.Controller.Slim.SlimPadState')
$NativeLegacyPad = $GameAssembly.GetType('JumpKing.Controller.LegacyPad')
if ($null -eq $NativeSlimPad -or $null -eq $NativeSlimState -or $null -eq $NativeLegacyPad) {
    throw 'Required installed DirectInput decoding contract is unavailable'
}
$JoystickField = $NativeSlimPad.GetField('m_joystick', $PrivateInstance)
if ($null -eq $JoystickField) { throw 'Required native DirectInput joystick field is unavailable' }
$JoystickType = $JoystickField.FieldType
$JoystickStateType = $JoystickType.Assembly.GetType('SlimDX.DirectInput.JoystickState')
$CooperativeType = $JoystickType.Assembly.GetType('SlimDX.DirectInput.CooperativeLevel')
$RequiredContracts = @(
    $NativeSlimPad.GetField('_connected', $PrivateInstance),
    $NativeSlimPad.GetField('m_current_state', $PrivateInstance),
    $NativeSlimPad.GetMethod('SetUpAxises', $PrivateInstance),
    $NativeSlimState.GetConstructor($PrivateInstance, $null, [Type[]]@($JoystickStateType), $null),
    $NativeLegacyPad.GetConstructor([Type[]]@($NativeSlimPad)),
    $JoystickType.GetMethod('Poll'),
    $JoystickType.GetMethod('Acquire'),
    $JoystickType.GetMethod('Unacquire'),
    $JoystickType.GetMethod('GetCurrentState', [Type[]]@($JoystickStateType.MakeByRefType())),
    $JoystickType.GetMethod('SetCooperativeLevel', [Type[]]@([IntPtr], $CooperativeType)),
    $JumpState,
    $BodyComp,
    $PlayerValues,
    $ControllerManager,
    $KeyboardPad,
    $XboxPad,
    $SaveLube,
    $GamePad,
    $GamePadState,
    $JumpState.GetField('m_timer', $PrivateInstance),
    $JumpState.GetMethod('Start', $PrivateInstance),
    $JumpState.GetField('customBlockSounds', $PrivateInstance),
    $JumpState.GetField('customBlockParticleSpawningActions', $PrivateInstance),
    $GameAssembly.GetType('JumpKing.Player.PlayerEntity').GetField('m_jump_state', $PrivateInstance),
    $GameAssembly.GetType('BehaviorTree.BTIsNodeRunning').GetField('m_check', $PrivateInstance),
    $GameAssembly.GetType('BehaviorTree.BTmanager').GetField('m_root_node', $PrivateInstance),
    $BodyComp.GetField('m_behaviours', $PrivateInstance),
    $GameAssembly.GetType('EntityComponent.Entity').GetField('m_components', $PrivateInstance),
    $ControllerManager.GetField('m_pads', $PrivateInstance),
    $XboxPad.GetField('m_index', $PrivateInstance),
    $KeyboardPad.GetMethod('GetSaveIdentifier', $PublicInstance),
    $KeyboardPad.GetMethod('GetDefaultBind', $PublicInstance),
    $SaveLube.GetMethod('LoadControllerBinding', $PublicStatic)
)
if ($RequiredContracts -contains $null) {
    throw 'The installed Jump King build does not expose the required Subframe Charge contract'
}
$JumpTime = $PlayerValues.GetProperty('JUMP_TIME').GetValue($null)
$FpsField = $PlayerValues.GetField('FPS')
if ($FpsField.GetRawConstantValue() -ne 60 -or [Math]::Abs($JumpTime - 0.6) -gt 0.0001) {
    throw "Unsupported Jump King timing contract: FPS=$($FpsField.GetRawConstantValue()), JUMP_TIME=$JumpTime"
}

New-Item -ItemType Directory -Force -Path $UploadDir, $InternalDir | Out-Null
# Recover stale logs produced by older contract-test builds. Never ship them.
$PayloadLog = Join-Path $UploadDir 'SubframeCharge.log'
if (Test-Path -LiteralPath $PayloadLog -PathType Leaf) {
    $ArchivedLog = Join-Path $InternalDir ('payload-log-' + [Guid]::NewGuid().ToString('N') + '.log')
    Move-Item -LiteralPath $PayloadLog -Destination $ArchivedLog
    Write-Host "[OK] Archived stale payload log: $ArchivedLog"
}
Copy-Item -LiteralPath $Runtime -Destination $InternalDir -Force
$Sources = Get-ChildItem -LiteralPath (Join-Path $ModRoot 'src') -Filter '*.cs' -File |
    Sort-Object Name |
    ForEach-Object FullName

& $Compiler /nologo /optimize+ /target:library /langversion:5 `
    "/out:$Assembly" `
    "/reference:$JumpKing" `
    "/reference:$MonoGame" `
    "/reference:$Runtime" `
    "/reference:$Harmony" `
    $Sources
if ($LASTEXITCODE -ne 0) {
    throw "Subframe Charge compilation failed with exit code $LASTEXITCODE"
}

$ModAssembly = [System.Reflection.Assembly]::LoadFrom($Assembly)
$PrecisionState = $ModAssembly.GetType('SubframeCharge.SubframeChargeState')
$PrecisionRun = if ($null -eq $PrecisionState) {
    $null
}
else {
    $PrecisionState.GetMethod('MyRun', $PrivateInstance)
}
if ($null -eq $PrecisionState `
    -or -not $PrecisionState.IsSubclassOf($JumpState) `
    -or $null -eq $PrecisionRun `
    -or -not $PrecisionRun.IsVirtual) {
    throw 'Subframe Charge must retain the native JumpState timing replacement'
}

if (-not $SkipTests) {
    $TestExecutable = Join-Path $InternalDir 'SubframeChargeTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 `
        "/out:$TestExecutable" `
        "/reference:$JumpKing" `
        "/reference:$MonoGame" `
    "/reference:$Runtime" `
        (Join-Path $ModRoot 'src\ChargeQuantizer.cs') `
        (Join-Path $ModRoot 'src\ChargeTimeline.cs') `
        (Join-Path $ModRoot 'src\ChargeFrameComponents.cs') `
        (Join-Path $ModRoot 'src\PauseClockObserver.cs') `
        (Join-Path $ModRoot 'src\DiagnosticLog.cs') `
        (Join-Path $ModRoot 'src\JumpPercentIntegration.cs') `
        (Join-Path $ModRoot 'src\LevelPermission.cs') `
        (Join-Path $ModRoot 'src\Settings.cs') `
        (Join-Path $ModRoot 'src\SubframeTapReplay.cs') `
        (Join-Path $ModRoot 'tests\SubframeChargeTests.cs') `
        (Join-Path $ModRoot 'tests\ChargeTimelineTests.cs') `
        (Join-Path $ModRoot 'tests\InputDiagnosticsTests.cs') `
        (Join-Path $ModRoot 'tests\InputReliabilityTests.cs') `
        (Join-Path $ModRoot 'tests\DirectInputTests.cs')
    if ($LASTEXITCODE -ne 0) {
        throw "Subframe Charge test compilation failed with exit code $LASTEXITCODE"
    }
    Copy-Item -LiteralPath $MonoGame -Destination $InternalDir -Force
    Copy-Item -LiteralPath $JumpKing -Destination $InternalDir -Force
    Copy-Item -LiteralPath $SlimDX -Destination $InternalDir -Force
    Copy-Item -LiteralPath $Harmony -Destination $InternalDir -Force
    foreach ($Name in @('SharpDX.dll', 'SharpDX.XInput.dll', 'SharpDX.DXGI.dll', 'SharpDX.Direct3D11.dll', 'SharpDX.Direct2D1.dll', 'LanguageJK.dll', 'Steamworks.NET.dll')) {
        Copy-Item -LiteralPath (Join-Path $GameDir $Name) -Destination $InternalDir -Force
    }
    & $TestExecutable $GameDir
    if ($LASTEXITCODE -ne 0) {
        throw "Subframe Charge tests failed with exit code $LASTEXITCODE"
    }
    $EvidenceExecutable = Join-Path $InternalDir 'EvidenceTraceTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 "/out:$EvidenceExecutable" "/reference:$JumpKing" "/reference:$MonoGame" "/reference:$Runtime" "/reference:$Harmony" $Sources (Join-Path $ModRoot 'tests/EvidenceTraceTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostic fixture compilation failed' }
    & $EvidenceExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostic evidence fixture failed' }
    $PerformanceExecutable = Join-Path $InternalDir 'PerformanceTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /platform:x64 /langversion:5 /nowarn:1685 /reference:System.Windows.Forms.dll "/out:$PerformanceExecutable" "/reference:$JumpKing" "/reference:$MonoGame" "/reference:$Runtime" "/reference:$Harmony" $Sources (Join-Path $ModRoot 'tests/PerformanceTests.cs') (Join-Path $ModRoot 'tests/PresentationGraphicsTests.cs') (Join-Path $ModRoot 'tests/MenuTransitionTests.cs') (Join-Path $ModRoot 'tests/TerrainPresentationTests.cs') (Join-Path $ModRoot 'tests/NativePredictionTests.cs') (Join-Path $ModRoot 'tests/BufferedJumpTreeTests.cs') (Join-Path $ModRoot 'tests/TextInputRoutingTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Performance fixture compilation failed' }
    Get-ChildItem -LiteralPath $GameDir -Filter "SharpDX*.dll" -File | Copy-Item -Destination $InternalDir -Force
    & $PerformanceExecutable --native-startup
    if ($LASTEXITCODE -ne 0) { throw "Native startup/animation fixture failed" }
    & $PerformanceExecutable --input-startup
    if ($LASTEXITCODE -ne 0) { throw 'Native input transition fixture failed' }
    & $PerformanceExecutable --buffered-jumps
    if ($LASTEXITCODE -ne 0) { throw 'Native buffered jump/ice leniency fixture failed' }
    $PresentationArguments = @(if ($CameraAssembly) { $CameraAssembly }; if ($Tier -eq 'Fast') { '--fast' })
    & $PerformanceExecutable @PresentationArguments
    if ($LASTEXITCODE -ne 0) { throw 'Performance fixture failed' }
    if ($Tier -ne 'Fast') {
    $TerrainWorkshop = Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'workshop/content/1061090'
    $TerrainSwitch = Join-Path $TerrainWorkshop '3188962826/SwitchBlocks.dll'
    $TerrainUpside = Join-Path $TerrainWorkshop '3410235901/UpSideDownCore.dll'
    if ((Test-Path -LiteralPath $TerrainSwitch) -and (Test-Path -LiteralPath $TerrainUpside)) {
        & $PerformanceExecutable --terrain $GameDir $TerrainSwitch $TerrainUpside
        if ($LASTEXITCODE -ne 0) { throw 'Installed campaign terrain fixture failed' }
    }

    }
    $DisabledExecutable = Join-Path $InternalDir 'DisabledModeTests.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 "/out:$DisabledExecutable" "/reference:$JumpKing" "/reference:$MonoGame" "/reference:$Runtime" (Join-Path $ModRoot 'tests/DisabledModeTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Disabled mode fixture compilation failed' }
    & $DisabledExecutable $Assembly (Join-Path $InternalDir '0Harmony.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Disabled mode fixture failed' }
    if ($Tier -ne 'Fast') {
    & (Join-Path $RepoRoot 'mods/jk-runtime/verify-conveyor-compat.ps1') -GameDir $GameDir -ChargeAssembly $Assembly
    $SteamApps = Split-Path (Split-Path $GameDir -Parent) -Parent
    $JumpPercentDir = Join-Path $SteamApps 'workshop\content\1061090\3158935297'
    if (Test-Path -LiteralPath (Join-Path $JumpPercentDir 'JumpKingLastJumpValue.dll')) {
        $ContractExecutable = Join-Path $InternalDir 'JumpPercentContractTests.exe'
        & $Compiler /nologo /optimize+ /target:exe /langversion:5 `
            "/out:$ContractExecutable" `
            "/reference:$JumpKing" `
            "/reference:$MonoGame" `
    "/reference:$Runtime" `
            (Join-Path $ModRoot 'tests\JumpPercentContractTests.cs') `
            (Join-Path $ModRoot 'tests\ChargeLifecycleContractTests.cs') `
            (Join-Path $ModRoot 'tests\MouseBindingContractTests.cs') `
            (Join-Path $ModRoot 'tests\DirectInputDiscoveryContractTests.cs')
        if ($LASTEXITCODE -ne 0) { throw 'Jump% contract test compilation failed' }
        foreach ($DiscoveryOrder in @('early', 'late')) {
            & $ContractExecutable $GameDir $JumpPercentDir $Assembly $DiscoveryOrder
            if ($LASTEXITCODE -ne 0) { throw "Jump% contract test failed: $DiscoveryOrder" }
        }
    }
    else {
        Write-Host '[SKIP] Optional installed Jump% contract test: Workshop item is absent'
    }
    } else { Write-Host '[SKIP Integration] GPU presentation, installed terrain, Conveyor and Jump% contracts' }
}

if (Test-Path -LiteralPath $PayloadLog) { throw 'Test diagnostics leaked into Workshop payload' }
$Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Assembly).Hash.ToLowerInvariant()
Write-Host "[OK] Subframe Charge: $Assembly"
Write-Host "[OK] SHA-256: $Hash"

if ($RuntimeAssembly) {
    # Pin the loader requirement to the SDK actually used for compilation.
    # Do not edit or rebuild the workspace's shared Runtime just to diagnose SFC.
    $TargetBuilder = Join-Path $InternalDir 'PackageBuilder.exe'
    & $Compiler /nologo /optimize+ /target:exe /langversion:5 "/out:$TargetBuilder" /reference:System.Xml.Linq.dll "/reference:$JumpKing" "/reference:$MonoGame" "/reference:$Runtime" (Join-Path $RepoRoot 'mods/jk-runtime/sdk/PackageBuilder.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Target Runtime package builder compilation failed' }
    & $TargetBuilder $Assembly (Join-Path $UploadDir 'SubframeCharge.dll') $GameDir $Harmony
    if ($LASTEXITCODE -ne 0) { throw 'Target Runtime package compilation failed' }
} else {
    & (Join-Path $RepoRoot 'mods\jk-runtime\sdk\package.ps1') -Implementation $Assembly -Output (Join-Path $UploadDir 'SubframeCharge.dll') -GameDir $GameDir -References @($Harmony)
}
Copy-Item -LiteralPath $Harmony -Destination $UploadDir -Force
Copy-Item -LiteralPath (Join-Path $ModRoot 'README.md'),(Join-Path $ModRoot 'THIRD_PARTY_NOTICES.md') -Destination $UploadDir -Force
$PackageDocs = Join-Path $UploadDir 'docs'
New-Item -ItemType Directory -Path $PackageDocs -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $ModRoot 'docs/diagnostic-build.md') -Destination $PackageDocs -Force
