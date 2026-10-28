param([string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Jump King')
$ErrorActionPreference='Stop'
$actorRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$actorReport=Join-Path $actorRepo 'build/_work/actor-visuals'
$actorStage=Join-Path $actorReport 'graphics-stage'
New-Item -ItemType Directory -Force -Path $actorStage | Out-Null
# retain the image/report, remove only these reproducible fixture binaries after the run
try {
    $actorRuntime=Join-Path $actorRepo 'build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll'
    $actorReferences=@("/reference:$GameDir/JumpKing.exe","/reference:$GameDir/MonoGame.Framework.dll","/reference:$actorRuntime",'/reference:System.Windows.Forms.dll')
    $actorExe=Join-Path $actorStage 'ActorVisualGraphicsTests.exe'
    & 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /optimize+ /target:exe /platform:x64 /langversion:5 "/out:$actorExe" $actorReferences (Join-Path $PSScriptRoot 'tests/ActorVisualGraphicsTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Actor visual graphics fixture compilation failed'}
    Copy-Item -LiteralPath $actorRuntime -Destination $actorStage
    foreach($name in @('JumpKing.exe','MonoGame.Framework.dll','LanguageJK.dll','Steamworks.NET.dll')){Copy-Item -LiteralPath (Join-Path $GameDir $name) -Destination $actorStage}
    Get-ChildItem -LiteralPath $GameDir -Filter 'SharpDX*.dll' -File | Copy-Item -Destination $actorStage
    & $actorExe $GameDir (Join-Path $actorRepo 'build/morph-ball/_INTERNAL/MorphBall.Module.dll') (Join-Path $actorRepo 'build/more-items/_INTERNAL/MoreItems.Module.dll') (Join-Path $actorRepo 'build/powers/_INTERNAL/Powers.Module.dll') (Join-Path $actorReport 'actors.png')
    if($LASTEXITCODE -ne 0){throw 'Actor visual graphics verification failed'}
} finally {
    $actorResolved=[IO.Path]::GetFullPath($actorStage)
    if($actorResolved -ne [IO.Path]::GetFullPath((Join-Path $actorRepo 'build/_work/actor-visuals/graphics-stage'))){throw 'Unexpected actor fixture cleanup path'}
    Remove-Item -LiteralPath $actorResolved -Recurse -Force
}
