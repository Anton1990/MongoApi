$url = "http://localhost:30080/api/Products"

# Большой payload — длинное имя чтобы выделить больше памяти на каждый запрос
$bigName = "LoadTest-" + ("X" * 500)
$body = "{`"name`":`"$bigName`",`"price`":1.99,`"stock`":100,`"categoryId`":`"000000000000000000000001`",`"isAvailable`":true}"

$workers = 200
$requestsPerWorker = 1000
$totalRequests = $workers * $requestsPerWorker

Write-Host "=== Phase 1: LOAD (watch memory go UP in Lens) ==="
Write-Host "Workers: $workers | Requests each: $requestsPerWorker | Total: $totalRequests"
Write-Host ""

$start = [DateTime]::UtcNow

$jobs = 1..$workers | ForEach-Object {
    Start-Job -ScriptBlock {
        param($url, $body, $count)
        $ok = 0; $fail = 0
        $headers = @{ "Content-Type" = "application/json" }
        for ($i = 0; $i -lt $count; $i++) {
            try {
                $r = Invoke-WebRequest -Uri $url -Method POST -Body $body -Headers $headers -UseBasicParsing -TimeoutSec 15
                if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) { $ok++ } else { $fail++ }
            } catch { $fail++ }
        }
        return @{ ok = $ok; fail = $fail }
    } -ArgumentList $url, $body, $requestsPerWorker
}

Write-Host "All $workers workers started..."
$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job

$elapsed = ([DateTime]::UtcNow - $start).TotalSeconds
$totalOk   = ($results | Measure-Object -Property ok   -Sum).Sum
$totalFail = ($results | Measure-Object -Property fail -Sum).Sum
$rps = [math]::Round($totalRequests / $elapsed, 1)

Write-Host ""
Write-Host "=== Results ==="
Write-Host "Total : $totalRequests | OK: $totalOk | Failed: $totalFail"
Write-Host "Duration: $([math]::Round($elapsed,2)) s | Throughput: $rps req/s"
Write-Host ""
Write-Host "=== Phase 2: COOLDOWN — watch memory DROP as GC kicks in ==="
Write-Host "Waiting 60 seconds... watch Lens graph now!"

for ($i = 60; $i -gt 0; $i -= 5) {
    Write-Host "  $i seconds remaining..."
    Start-Sleep -Seconds 5
}

Write-Host ""
Write-Host "Done. Check memory graph — GC should have reclaimed memory."
