# Simulate a ProRouting happy-path delivery by firing the 5 status webhooks in sequence.
#
# Prereq: an order must already be in Searching3PL (i.e. restaurant just marked it ReadyForPickup
# within the last 30 seconds). The first agent-assigned webhook MUST land before the orchestrator's
# AcceptanceTimeoutSeconds (default 30s) expires, or the order will fall back to Own Fleet/Failed.
#
# Usage:
#   ./scripts/simulate-prorouting-happy-path.ps1 -OrderNumber ORD-20260515-00135
#   ./scripts/simulate-prorouting-happy-path.ps1 -OrderNumber ORD-20260515-00135 -BaseUrl https://other.url
#
# Env mode: this script assumes Railway is in Development mode (no x-pro-api-key required).
# If you flip to Production, add -ApiKey "<value of PROROUTING_INBOUND_API_KEY>".

param(
    [Parameter(Mandatory = $true)]
    [string]$OrderNumber,

    [string]$BaseUrl = "https://rally-production-2004.up.railway.app",

    [string]$ApiKey = "",

    [int]$DelayBetweenStepsSeconds = 4
)

$ErrorActionPreference = "Stop"
$webhookUrl = "$BaseUrl/api/webhooks/prorouting"
$trackingUrl = "$BaseUrl/api/track/$OrderNumber"

# Build a fake-but-stable task id from the order number so all webhook events target the same delivery.
$taskId = "SIMULATED-" + $OrderNumber

$headers = @{ "Content-Type" = "application/json" }
if ($ApiKey -ne "") { $headers["x-pro-api-key"] = $ApiKey }

function Send-Webhook {
    param(
        [string]$State,
        [string]$Label,
        [hashtable]$ExtraOrderFields = @{}
    )

    $order = @{
        id                = $taskId
        client_order_id   = $OrderNumber
        network_order_id  = "NET-$OrderNumber"
        state             = $State
        logistics_seller  = "ProRouting (Simulated)"
        lsp_id            = "SIM-LSP"
    }
    foreach ($k in $ExtraOrderFields.Keys) { $order[$k] = $ExtraOrderFields[$k] }

    $body = @{
        status  = 1
        message = "Simulated $State"
        order   = $order
    } | ConvertTo-Json -Depth 10

    Write-Host ""
    Write-Host "──────────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host "→ $Label  (state: $State)" -ForegroundColor Cyan
    Write-Host "──────────────────────────────────────────────────────" -ForegroundColor Cyan

    try {
        $response = Invoke-RestMethod -Method Post -Uri $webhookUrl -Headers $headers -Body $body
        Write-Host "  Webhook accepted: $($response | ConvertTo-Json -Compress)" -ForegroundColor Green
    } catch {
        Write-Host "  Webhook FAILED: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails) { Write-Host "  Body: $($_.ErrorDetails.Message)" -ForegroundColor Red }
        throw
    }

    Start-Sleep -Milliseconds 500
    try {
        $track = Invoke-RestMethod -Method Get -Uri $trackingUrl
        Write-Host "  Tracking now reads: status=$($track.status), text='$($track.statusText)'" -ForegroundColor Yellow
        if ($track.rider) {
            Write-Host "  Rider: $($track.rider.name) / $($track.rider.phone) (isOwnFleet=$($track.rider.isOwnFleet))" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  Tracking lookup failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "═══════════════════════════════════════════════════════════════════"
Write-Host "ProRouting happy-path simulation"
Write-Host "  Order:     $OrderNumber"
Write-Host "  Task ID:   $taskId"
Write-Host "  Webhook:   $webhookUrl"
Write-Host "  Tracking:  $trackingUrl"
Write-Host "  Auth:      $(if ($ApiKey) { 'x-pro-api-key sent' } else { 'unsigned (Development mode)' })"
Write-Host "═══════════════════════════════════════════════════════════════════"

# Step 1: rider assigned
Send-Webhook -State "agent-assigned" -Label "STEP 1: ProRouting assigns a rider" -ExtraOrderFields @{
    price       = 75.0
    distance    = 3.2
    fees        = @{ lsp = 65.0; platform = 10.0; total_with_tax = 88.5 }
    rider       = @{ name = "Test Rider"; phone = "9000099001" }
    tracking_url = "https://preprod.logistics-buyer.prorouting.in/track/$taskId"
}

Start-Sleep -Seconds $DelayBetweenStepsSeconds

# Step 2: rider at pickup
Send-Webhook -State "at-pickup" -Label "STEP 2: Rider arrived at restaurant"

Start-Sleep -Seconds $DelayBetweenStepsSeconds

# Step 3: order picked up
Send-Webhook -State "picked-up" -Label "STEP 3: Order picked up (PickupCode exchanged)"

Start-Sleep -Seconds $DelayBetweenStepsSeconds

# Step 4: rider at customer
Send-Webhook -State "at-delivery" -Label "STEP 4: Rider arrived at customer"

Start-Sleep -Seconds $DelayBetweenStepsSeconds

# Step 5: delivered
Send-Webhook -State "delivered" -Label "STEP 5: Order delivered (DropCode exchanged)"

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  Simulation complete. Final tracking state above." -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Green
