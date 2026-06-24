# start-dev.ps1
# Запускается автоматически перед билдом через MSBuild target.
# Пробрасывает порт mongo-0 -> localhost:27017 если он ещё не слушает.

$localPort = 27017

$listening = netstat -an 2>$null | Select-String "127.0.0.1:$localPort\s+.*LISTENING"
if ($listening) {
    Write-Host "[port-forward] localhost:$localPort already listening — skipping"
    exit 0
}

Write-Host "[port-forward] Starting kubectl port-forward pod/mongo-0 $localPort`:$localPort -n mongoapi ..."
Start-Process -WindowStyle Hidden -FilePath "kubectl" `
    -ArgumentList "port-forward", "pod/mongo-0", "$localPort`:$localPort", "-n", "mongoapi"

Start-Sleep -Seconds 2

$check = netstat -an 2>$null | Select-String "127.0.0.1:$localPort\s+.*LISTENING"
if ($check) {
    Write-Host "[port-forward] OK — localhost:$localPort -> mongo-0:$localPort"
} else {
    Write-Host "[port-forward] WARN — port-forward may have failed, check kubectl"
}
