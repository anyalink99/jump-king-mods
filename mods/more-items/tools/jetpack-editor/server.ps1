param(
    [Parameter(Mandatory = $true)][int]$Port,
    [Parameter(Mandatory = $true)][string]$WebRoot,
    [Parameter(Mandatory = $true)][string]$AtlasPath,
    [Parameter(Mandatory = $true)][string]$PosePath
)

$ErrorActionPreference = 'Stop'
$Listener = [System.Net.HttpListener]::new()
$Listener.Prefixes.Add("http://127.0.0.1:$Port/")
$Listener.Start()

function Send-Bytes {
    param(
        [System.Net.HttpListenerResponse]$Response,
        [byte[]]$Bytes,
        [string]$ContentType,
        [int]$StatusCode = 200
    )
    $Response.StatusCode = $StatusCode
    $Response.ContentType = $ContentType
    $Response.ContentLength64 = $Bytes.Length
    $Response.OutputStream.Write($Bytes, 0, $Bytes.Length)
    $Response.OutputStream.Close()
}

function Send-Text {
    param(
        [System.Net.HttpListenerResponse]$Response,
        [string]$Text,
        [string]$ContentType = 'text/plain; charset=utf-8',
        [int]$StatusCode = 200
    )
    Send-Bytes $Response ([Text.Encoding]::UTF8.GetBytes($Text)) $ContentType $StatusCode
}

function Read-Poses {
    $Result = @()
    foreach ($Line in [IO.File]::ReadAllLines($PosePath)) {
        if ([string]::IsNullOrWhiteSpace($Line)) {
            continue
        }
        $Values = $Line.Split(',')
        $Result += [ordered]@{
            name = $Values[0]
            cellX = [int]$Values[1]
            cellY = [int]$Values[2]
            x = [int]$Values[3]
            y = [int]$Values[4]
            rotated = [bool]::Parse($Values[5])
        }
    }
    return $Result
}

function Save-Poses {
    param([object[]]$Poses)
    if ($Poses.Count -ne 13) {
        throw 'Pose data must contain 13 frames'
    }
    $Lines = foreach ($Pose in $Poses) {
        if ([string]::IsNullOrWhiteSpace([string]$Pose.name)) {
            throw 'Pose name is required'
        }
        $Name = ([string]$Pose.name) -replace '[^a-z_]', ''
        $CellX = [int]$Pose.cellX
        $CellY = [int]$Pose.cellY
        $X = [int]$Pose.x
        $Y = [int]$Pose.y
        $Rotated = ([bool]$Pose.rotated).ToString().ToLowerInvariant()
        "$Name,$CellX,$CellY,$X,$Y,$Rotated"
    }
    [IO.File]::WriteAllLines($PosePath, $Lines, [Text.UTF8Encoding]::new($false))
}

$ContentTypes = @{
    '.html' = 'text/html; charset=utf-8'
    '.css' = 'text/css; charset=utf-8'
    '.js' = 'application/javascript; charset=utf-8'
}

try {
    while ($Listener.IsListening) {
        $Context = $Listener.GetContext()
        $Request = $Context.Request
        $Response = $Context.Response
        try {
            if ($Request.Url.AbsolutePath -eq '/api/poses') {
                if ($Request.HttpMethod -eq 'GET') {
                    Send-Text $Response ((Read-Poses) | ConvertTo-Json -Depth 4) 'application/json; charset=utf-8'
                    continue
                }
                if ($Request.HttpMethod -eq 'POST') {
                    $Reader = [IO.StreamReader]::new($Request.InputStream, $Request.ContentEncoding)
                    $Payload = $Reader.ReadToEnd() | ConvertFrom-Json
                    $Reader.Dispose()
                    Save-Poses @($Payload)
                    Send-Text $Response '{"saved":true}' 'application/json; charset=utf-8'
                    continue
                }
                Send-Text $Response 'Method not allowed' 'text/plain; charset=utf-8' 405
                continue
            }

            if ($Request.Url.AbsolutePath -eq '/atlas.png') {
                Send-Bytes $Response ([IO.File]::ReadAllBytes($AtlasPath)) 'image/png'
                continue
            }

            $Relative = $Request.Url.AbsolutePath.TrimStart('/')
            if ([string]::IsNullOrWhiteSpace($Relative)) {
                $Relative = 'index.html'
            }
            $Candidate = [IO.Path]::GetFullPath((Join-Path $WebRoot $Relative))
            $Root = [IO.Path]::GetFullPath($WebRoot)
            if (-not $Candidate.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase) -or
                -not [IO.File]::Exists($Candidate)) {
                Send-Text $Response 'Not found' 'text/plain; charset=utf-8' 404
                continue
            }
            $Extension = [IO.Path]::GetExtension($Candidate).ToLowerInvariant()
            $ContentType = $ContentTypes[$Extension]
            if (-not $ContentType) {
                $ContentType = 'application/octet-stream'
            }
            Send-Bytes $Response ([IO.File]::ReadAllBytes($Candidate)) $ContentType
        } catch {
            if ($Response.OutputStream.CanWrite) {
                Send-Text $Response $_.Exception.Message 'text/plain; charset=utf-8' 500
            }
        }
    }
} finally {
    $Listener.Stop()
    $Listener.Close()
}
