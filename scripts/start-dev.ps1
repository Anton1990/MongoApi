# start-dev.ps1
# Zapuskaetsya avtomaticheski pered bildom cherez MSBuild target.
# Probrasivaet port mongo-0 -> localhost:27017 esli on eshche ne slushaet.

$ErrorActionPreference = 'Continue'
$localPort = 27017

try {
    $listening = netstat -an | Select-String "127.0.0.1:$localPort\s+.*LISTENING"
    if ($listening) {
        Write-Host "[port-forward] localhost:$localPort already listening - skipping"
        exit 0
    }

    Write-Host "[port-forward] Starting kubectl port-forward pod/mongo-0 ${localPort}:${localPort} -n mongoapi ..."
    Start-Process -WindowStyle Hidden -FilePath "kubectl" `
        -ArgumentList "port-forward", "pod/mongo-0", "${localPort}:${localPort}", "-n", "mongoapi"

    Start-Sleep -Seconds 2

    $check = netstat -an | Select-String "127.0.0.1:$localPort\s+.*LISTENING"
    if ($check) {
        Write-Host "[port-forward] OK - localhost:$localPort -> mongo-0:$localPort"
    } else {
        Write-Host "[port-forward] WARN - port-forward may have failed, check kubectl"
    }
} catch {
    Write-Host "[port-forward] ERROR: $($_.Exception.Message)"
}

exit 0
