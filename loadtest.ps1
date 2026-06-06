$base = "http://localhost:30080"

# LOH payload: строка > 85KB — идёт прямо в Large Object Heap, Gen2 GC
$lohString = "X" * 90000
$productBody  = "{`"name`":`"$lohString`",`"price`":1.99,`"stock`":100,`"categoryId`":`"6a226d14824edff39de9fbf5`",`"isAvailable`":true}"
$customerBody = "{`"name`":`"$lohString`",`"email`":`"test@load.io`",`"phone`":`"+79001234567`"}"
$orderBody    = "{`"customerId`":`"000000000000000000000001`",`"items`":[{`"productId`":`"000000000000000000000001`",`"quantity`":1,`"price`":1.99}]}"

Write-Host "=== MULTI-API LOAD TEST ==="
Write-Host "Target: Gen2 GC via LOH allocs (payload ~90KB each)"
Write-Host ""
Write-Host "Workers breakdown:"
Write-Host "  POST /api/Products  x 60 workers"
Write-Host "  GET  /api/Products  x 40 workers (large response)"
Write-Host "  POST /api/Customers x 40 workers"
Write-Host "  GET  /api/Customers x 20 workers"
Write-Host "  POST /api/Orders    x 40 workers"
Write-Host "Total: 200 workers, 300 requests each = 60 000 requests"
Write-Host ""
Write-Host "Watch Grafana -> GC Heap by Generation"
Write-Host "Expected: Gen0 spikes -> Gen1 promotions -> Gen2 full collection"
Write-Host ""

$start = [DateTime]::UtcNow

$allJobs = @()

# POST /api/Products — LOH alloc per request
$allJobs += 1..60 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($url, $body, $count)
        $ok = 0; $fail = 0
        $h = @{ "Content-Type" = "application/json" }
        for ($i = 0; $i -lt $count; $i++) {
            try { $r = Invoke-WebRequest -Uri $url -Method POST -Body $body -Headers $h -UseBasicParsing -TimeoutSec 30; if ($r.StatusCode -lt 300) { $ok++ } else { $fail++ } } catch { $fail++ }
        }
        return @{ endpoint = "POST /Products"; ok = $ok; fail = $fail }
    } -ArgumentList "$base/api/Products", $productBody, 300
}

# GET /api/Products — большой ответ = аллокация списка
$allJobs += 1..40 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($url, $count)
        $ok = 0; $fail = 0
        for ($i = 0; $i -lt $count; $i++) {
            try { $r = Invoke-WebRequest -Uri $url -Method GET -UseBasicParsing -TimeoutSec 30; if ($r.StatusCode -lt 300) { $ok++ } else { $fail++ } } catch { $fail++ }
        }
        return @{ endpoint = "GET /Products"; ok = $ok; fail = $fail }
    } -ArgumentList "$base/api/Products", 300
}

# POST /api/Customers — LOH alloc
$allJobs += 1..40 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($url, $body, $count)
        $ok = 0; $fail = 0
        $h = @{ "Content-Type" = "application/json" }
        for ($i = 0; $i -lt $count; $i++) {
            try { $r = Invoke-WebRequest -Uri $url -Method POST -Body $body -Headers $h -UseBasicParsing -TimeoutSec 30; if ($r.StatusCode -lt 300) { $ok++ } else { $fail++ } } catch { $fail++ }
        }
        return @{ endpoint = "POST /Customers"; ok = $ok; fail = $fail }
    } -ArgumentList "$base/api/Customers", $customerBody, 300
}

# GET /api/Customers
$allJobs += 1..20 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($url, $count)
        $ok = 0; $fail = 0
        for ($i = 0; $i -lt $count; $i++) {
            try { $r = Invoke-WebRequest -Uri $url -Method GET -UseBasicParsing -TimeoutSec 30; if ($r.StatusCode -lt 300) { $ok++ } else { $fail++ } } catch { $fail++ }
        }
        return @{ endpoint = "GET /Customers"; ok = $ok; fail = $fail }
    } -ArgumentList "$base/api/Customers", 300
}

# POST /api/Orders
$allJobs += 1..40 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($url, $body, $count)
        $ok = 0; $fail = 0
        $h = @{ "Content-Type" = "application/json" }
        for ($i = 0; $i -lt $count; $i++) {
            try { $r = Invoke-WebRequest -Uri $url -Method POST -Body $body -Headers $h -UseBasicParsing -TimeoutSec 30; if ($r.StatusCode -lt 300) { $ok++ } else { $fail++ } } catch { $fail++ }
        }
        return @{ endpoint = "POST /Orders"; ok = $ok; fail = $fail }
    } -ArgumentList "$base/api/Orders", $orderBody, 300
}

Write-Host "All 200 workers started. Hammering APIs..."
$results = $allJobs | Wait-Job | Receive-Job
$allJobs | Remove-Job

$elapsed = ([DateTime]::UtcNow - $start).TotalSeconds

Write-Host ""
Write-Host "=== Results by endpoint ==="
$results | Group-Object endpoint | ForEach-Object {
    $ok   = ($_.Group | Measure-Object -Property ok   -Sum).Sum
    $fail = ($_.Group | Measure-Object -Property fail -Sum).Sum
    Write-Host ("  {0,-22} OK: {1,6}  Fail: {2,4}" -f $_.Name, $ok, $fail)
}

$totalOk   = ($results | Measure-Object -Property ok   -Sum).Sum
$totalFail = ($results | Measure-Object -Property fail -Sum).Sum
$total     = $totalOk + $totalFail
$rps       = [math]::Round($total / $elapsed, 1)

Write-Host ""
Write-Host ("Total: {0} | OK: {1} | Fail: {2}" -f $total, $totalOk, $totalFail)
Write-Host ("Duration: {0} s | Throughput: {1} req/s" -f [math]::Round($elapsed,2), $rps)
Write-Host ""
Write-Host "=== Cooldown 60s - watch Gen2 GC cleanup in Grafana ==="
for ($i = 60; $i -gt 0; $i -= 10) { Write-Host "  $i s..."; Start-Sleep 10 }
Write-Host "Done."
