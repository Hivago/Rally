param(
    [string]$BaseUrl = "http://localhost:5023",
    [string]$RestaurantId = "00000000-0000-0000-0000-000000000001",
    [decimal]$OrderAmount = 500,
    [int]$ParallelLimit = 4
)

# Pickup: Koramangala, Bangalore (sample restaurant location)
$pickupLat = 12.9352
$pickupLng = 77.6245

# Distances we want to probe (km)
$distancesKm = @(0.5, 1, 2, 3, 5, 7, 9, 11, 13, 14, 14.5, 15, 16, 18, 20, 25, 30)

# Bearings to probe (degrees from north, clockwise)
$bearings = @(0, 45, 90, 135, 180, 225, 270, 315)

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
        [math]::Cos($d) - [math]::Sin($lat1) * [math]::Sin($lat2)
    )

    return @{ lat = $lat2 * 180 / [math]::PI; lng = $lng2 * 180 / [math]::PI }
}

$cases = @()
foreach ($dist in $distancesKm) {
    foreach ($brng in $bearings) {
        $drop = Move-Coordinate -lat $pickupLat -lng $pickupLng -distanceKm $dist -bearingDeg $brng
        $cases += [pscustomobject]@{
            TargetKm = $dist
            Bearing  = $brng
            DropLat  = $drop.lat
            DropLng  = $drop.lng
        }
    }
}

Write-Host "Total test cases: $($cases.Count)" -ForegroundColor Cyan
Write-Host "Pickup: ($pickupLat, $pickupLng)" -ForegroundColor Cyan
Write-Host ""

$results = @()
$i = 0
foreach ($c in $cases) {
    $i++
    $body = @{
        restaurantId    = $RestaurantId
        pickupLatitude  = $pickupLat
        pickupLongitude = $pickupLng
        dropLatitude    = $c.DropLat
        dropLongitude   = $c.DropLng
        orderAmount     = $OrderAmount
    } | ConvertTo-Json -Compress

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = $null
    $err = $null
    $resp = $null
    try {
        $resp = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/delivery/quote" `
            -ContentType "application/json" -Body $body -TimeoutSec 60 `
            -ErrorAction Stop
        $status = "OK"
    }
    catch {
        $status = "FAIL"
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $err = $reader.ReadToEnd()
        } catch {
            $err = $_.Exception.Message
        }
    }
    $sw.Stop()

    $row = [pscustomobject]@{
        Idx      = $i
        TargetKm = $c.TargetKm
        Bearing  = $c.Bearing
        Status   = $status
        Ms       = [int]$sw.ElapsedMilliseconds
        Fee      = if ($resp) { $resp.deliveryFee } else { $null }
        DistKm   = if ($resp) { $resp.distanceKm } else { $null }
        EtaMin   = if ($resp) { $resp.estimatedMinutes } else { $null }
        Error    = if ($err) { ($err -replace "`r?`n"," ").Substring(0, [math]::Min(200, $err.Length)) } else { $null }
    }
    $results += $row

    $color = if ($status -eq "OK") { "Green" } else { "Red" }
    Write-Host ("[{0,3}/{1,3}] {2,5} km @ {3,3} deg -> {4,-4} {5,5}ms" -f `
        $i, $cases.Count, $c.TargetKm, $c.Bearing, $status, $row.Ms) -ForegroundColor $color
}

Write-Host ""
Write-Host "=== Summary by distance ===" -ForegroundColor Yellow
$results | Group-Object TargetKm | Sort-Object { [double]$_.Name } | ForEach-Object {
    $g = $_.Group
    $ok = ($g | Where-Object Status -eq "OK").Count
    $fail = ($g | Where-Object Status -eq "FAIL").Count
    $rate = "{0:N0}%" -f (($ok / $g.Count) * 100)
    [pscustomobject]@{
        TargetKm = [double]$_.Name
        Total    = $g.Count
        OK       = $ok
        Fail     = $fail
        OkRate   = $rate
    }
} | Format-Table -AutoSize

Write-Host "=== Failures (first 20) ===" -ForegroundColor Yellow
$results | Where-Object Status -eq "FAIL" | Select-Object -First 20 | Format-Table TargetKm,Bearing,Error -AutoSize -Wrap

# Write CSV report
$csvPath = Join-Path (Split-Path $PSCommandPath -Parent) "quote-stress-results.csv"
$results | Export-Csv -Path $csvPath -NoTypeInformation
Write-Host "Full results: $csvPath" -ForegroundColor Cyan
