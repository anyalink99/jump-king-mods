param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$taskRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskRelease=Join-Path $taskRepo ('build/run-verifier/release-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path "$taskRelease/required","$taskRelease/optional" -Force|Out-Null
Copy-Item -LiteralPath "$taskRepo/build/jk-runtime/UPLOAD_TO_WORKSHOP/JKRuntime.dll","$taskRepo/build/run-verifier/UPLOAD_TO_WORKSHOP/RunVerifier.dll" -Destination "$taskRelease/required"
Copy-Item -LiteralPath "$taskRepo/build/replays/UPLOAD_TO_WORKSHOP/Replays.dll" -Destination "$taskRelease/optional"
Copy-Item -LiteralPath "$PSScriptRoot/Install-Distribution.ps1" -Destination "$taskRelease/Install.ps1"
Copy-Item -LiteralPath "$PSScriptRoot/README.md" -Destination $taskRelease
Copy-Item -LiteralPath "$PSScriptRoot/docs" -Destination $taskRelease -Recurse
@'
Close Jump King. Open PowerShell in this extracted folder:
  powershell -ExecutionPolicy Bypass -File .\Install.ps1
To install/update the optional Replays bridge too:
  powershell -ExecutionPolicy Bypass -File .\Install.ps1 -IncludeReplays
For a different Steam library, add -GameDir "D:\SteamLibrary\steamapps\common\Jump King".
The installer updates existing Workshop copies, preserves settings and saves rollback DLLs.
It refuses multiple active copies. Restart the game and enable the mods if necessary.
Use Mods > Run Verifier > Run history > Settings > Connect Steam, then enable Online + auto upload before a new attempt.
'@ | Set-Content "$taskRelease/INSTALL.txt" -Encoding ASCII
# Optional local archive only. Public distribution is through Steam Workshop.
$taskDownloads=if($OutputDirectory){$OutputDirectory}else{Join-Path $taskRepo 'build/run-verifier/_INTERNAL/distribution'}
New-Item -ItemType Directory -Path $taskDownloads -Force|Out-Null
$taskZip=Join-Path $taskDownloads 'run-verifier-1.2.0.zip'
Compress-Archive -Path "$taskRelease/*" -DestinationPath $taskZip -Force
$taskManifest=@{version='1.2.0';runtime='1.28.0';replays='2.2.0';sha256=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant();bytes=(Get-Item -LiteralPath $taskZip).Length;files=@(Get-ChildItem -LiteralPath $taskRelease -Recurse -File|ForEach-Object {@{name=$_.FullName.Substring($taskRelease.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})}
$taskManifest|ConvertTo-Json -Depth 5|Set-Content (Join-Path $taskDownloads 'manifest.json') -Encoding UTF8
Write-Host "Release package: $taskZip"
