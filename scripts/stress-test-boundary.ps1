param(
    [string]$BaseUrl = "http://localhost:5023",
    [string]$RestaurantId = "00000000-0000-0000-0000-000000000001",
    [int]$Iterations = 5
)

$pickupLat = 12.9352
$pickupLng = 77.6245

# Probe the boundary zone in finer steps, repeat each point several times
$distancesKm = @(12, 13, 13.5, 14, 14.5, 15, 15.5)
$bearings   = @(0, 90, 135, 180, 270)

function Move-Coordinate {
    param([double]$lat, [double]$lng, [double]$distanceKm, [double]$bearingDeg)
    $R = 6371.0
    $brng = $bearingDeg * [math]::PI / 180
    $lat1 = $lat * [math]::PI / 180
    $lng1 = $lng * [math]::PI / 180
    $d = $distanceKm / $R
    $lat2 = [math]::Asin([math]::Sin($lat1) * [math]::Cos($d) + [math]::Cos($lat1) * [math]::Sin($d) * [math]::Cos($brng))
    $lng2 = $lng1 + [math]::Atan2(
        [math]::Sin($brng) * [math]::Sin($d) * [math]::Cos($lat1),
        [math]::Cos($d) - [math]::Sin($lat1) * [math]::Sin($lat2))
    return @{ lat = $lat2 * 180 / [math]::PI; lng = $lng2 * 180 / [math]::PI }
}

$results = @()
foreach ($dist in $distancesKm) {
    foreach ($brng in $bearings) {
        for ($i = 1; $i -le $Iterations; $i++) {
            $drop = Move-Coordinate -lat $pickupLat -lng $pickupLng -distanceKm $dist -bearingDeg $brng
            $body = @{
                restaurantId    = $RestaurantId
                pickupLatitude  = $pickupLat
                pickupLongitude = $pickupLng
                dropLatitude    = $drop.lat
                dropLongitude   = $drop.lng
                orderAmount     = 500
            } | ConvertTo-Json -Compress
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $status = $null
            $resp = $null
            $err = $null
            try {
                $resp = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/delivery/quote" `
                    -ContentType "application/json" -Body $body -TimeoutSec 70
                $status = "OK"
            } catch {
                $status = "FAIL"
                try {
                    $stream = $_.Exception.Response.GetResponseStream()
                    $reader = New-Object System.IO.StreamReader($stream)
                    $err = $reader.ReadToEnd()
                } catch { $err = $_.Exception.Message }
            }
            $sw.Stop()
            $row = [pscustomobject]@{
                Dist   = $dist
                Bearing = $brng
                Iter   = $i
                Status = $status
                Ms     = [int]$sw.ElapsedMilliseconds
                Fee    = if ($resp) { $resp.deliveryFee } else { $null }
                RoadKm = if ($resp) { $resp.distanceKm } else { $null }
                Error  = if ($err) { $err.Substring(0, [math]::Min(120, $err.Length)) } else { $null }
            }
            $results += $row
            $color = if ($status -eq "OK") { "Green" } else { "Red" }
            Write-Host ("{0,5}km @ {1,3}° i{2} -> {3,-4} {4,5}ms fee={5}" -f `
                $dist, $brng, $i, $status, $row.Ms, $row.Fee) -ForegroundColor $color
        }
    }
}

Write-Host ""
Write-Host "=== Boundary results ===" -ForegroundColor Yellow
$results | Group-Object Dist | Sort-Object { [double]$_.Name } | ForEach-Object {
    $g = $_.Group
    $ok = ($g | Where-Object Status -eq "OK").Count
    $fail = ($g | Where-Object Status -eq "FAIL").Count
    $medMs = [int](($g | Sort-Object Ms)[[int]($g.Count/2)].Ms)
    $maxMs = ($g | Measure-Object Ms -Maximum).Maximum
    [pscustomobject]@{
        DistKm   = [double]$_.Name
        Total    = $g.Count
        OK       = $ok
        Fail     = $fail
        MedianMs = $medMs
        MaxMs    = $maxMs
    }
} | Format-Table -AutoSize

$csvPath = Join-Path (Split-Path $PSCommandPath -Parent) "boundary-results.csv"
$results | Export-Csv -Path $csvPath -NoTypeInformation
Write-Host "CSV: $csvPath"
