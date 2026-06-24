# start-dev.ps1
# Запускает kubectl port-forward для MongoDB перед стартом приложения в Visual Studio.
# Использование: .\scripts\start-dev.ps1

$namespace = "mongoapi"
$pod       = "mongo-0"
$localPort = 27017
$remotePort = 27017

# Проверяем — порт уже слушает?
$listening = netstat -an 2>$null | Select-String "127.0.0.1:$localPort\s+.*LISTENING"
if ($listening) {
    Write-Host "[OK] Port $localPort already listening — port-forward or local MongoDB is running"
} else {
    Write-Host "Starting kubectl port-forward $pod $localPort`:$remotePort -n $namespace ..."
    Start-Process -WindowStyle Hidden -FilePath "kubectl" `
        -ArgumentList "port-forward", "pod/$pod", "$localPort`:$remotePort", "-n", $namespace
    Start-Sleep -Seconds 2

    $check = netstat -an 2>$null | Select-String "127.0.0.1:$localPort\s+.*LISTENING"
    if ($check) {
        Write-Host "[OK] Port-forward started: localhost:$localPort -> $pod`:$remotePort"
    } else {
        Write-Host "[WARN] Port-forward may have failed. Check: kubectl get pod $pod -n $namespace"
    }
}

Write-Host ""
Write-Host "MongoDB  : localhost:$localPort  ->  K8s $pod"
Write-Host "Profiler : auto-enabled on app start (Development mode)"
Write-Host ""
Write-Host "Open Visual Studio and press F5 (http profile)"
