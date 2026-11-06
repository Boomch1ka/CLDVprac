<#
.SYNOPSIS
    AgriStream IoT - Concurrent Load Test & Telemetry Surge Simulator (Task e, f, g)
.DESCRIPTION
    Simulates peak daylight traffic against your deployed Azure App Service (or local server):
      1. Concurrent HTTP GET requests to the home page and CPU-intensive /api/telemetry/forecast
         endpoint to drive CPU usage above the auto-scale threshold and record which App Service
         instances respond (proving horizontal scale-out).
      2. High-frequency POST /api/telemetry sensor readings into Azure Table Storage.
.EXAMPLE
    .\load-test.ps1 -BaseUrl "https://12345678.azurewebsites.net" -ConcurrentUsers 30 -DurationSeconds 180
#>
param(
    [string]$BaseUrl = "https://st10376245.azurewebsites.net",
    [int]$ConcurrentUsers = 25,
    [int]$DurationSeconds = 120,
    [string]$IngestKey = "change-me-sensor-key",
    [int]$Simulations = 35000
)

$BaseUrl = $BaseUrl.TrimEnd('/')
Write-Host "==============================================================" -ForegroundColor Green
Write-Host " AgriStream IoT Load Test" -ForegroundColor Green
Write-Host " Target URL       : $BaseUrl"
Write-Host " Concurrent Users : $ConcurrentUsers"
Write-Host " Duration         : $DurationSeconds seconds"
Write-Host "==============================================================" -ForegroundColor Green

$stopAt = (Get-Date).AddSeconds($DurationSeconds)
$jobs = @()

$workerScript = {
    param($url, $stopTime, $key, $sims, $workerId)
    Add-Type -AssemblyName System.Net.Http
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    $rnd = [System.Random]::new($workerId * 997)
    $devices = @("SENSOR-001", "SENSOR-002", "SENSOR-003", "SENSOR-004", "SENSOR-005")

    $ok = 0; $fail = 0
    $latencies = [System.Collections.Generic.List[double]]::new()
    $instances = @{ }

    while ((Get-Date) -lt $stopTime) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $roll = $rnd.Next(0, 10)
            if ($roll -lt 6) {
                # 60%: CPU-intensive Monte-Carlo soil moisture forecast (triggers CPU scale-out)
                $m = $rnd.Next(20, 70)
                $resp = $client.GetAsync("$url/api/telemetry/forecast?moisture=$m&days=7&simulations=$sims").GetAwaiter().GetResult()
                if ($resp.IsSuccessStatusCode) {
                    $json = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
                    if ($json.instance) { $instances[$json.instance] = ($instances[$json.instance] + 1) }
                    $ok++
                } else { $fail++ }
            }
            elseif ($roll -lt 8) {
                # 20%: Sensor telemetry ingestion into Azure Table Storage
                $dev = $devices[$rnd.Next(0, $devices.Length)]
                $payload = @{
                    deviceId        = $dev
                    soilMoisture    = [math]::Round(25 + $rnd.NextDouble() * 45, 1)
                    soilTemperature = [math]::Round(15 + $rnd.NextDouble() * 12, 1)
                    airTemperature  = [math]::Round(18 + $rnd.NextDouble() * 15, 1)
                    humidity        = [math]::Round(40 + $rnd.NextDouble() * 45, 1)
                    ph              = [math]::Round(6.0 + $rnd.NextDouble() * 1.2, 2)
                    batteryLevel    = [math]::Round(60 + $rnd.NextDouble() * 40, 0)
                } | ConvertTo-Json
                $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "$url/api/telemetry")
                $req.Headers.Add("X-Device-Key", $key)
                $req.Content = [System.Net.Http.StringContent]::new($payload, [System.Text.Encoding]::UTF8, "application/json")
                $resp = $client.SendAsync($req).GetAwaiter().GetResult()
                if ($resp.IsSuccessStatusCode) { $ok++ } else { $fail++ }
            }
            else {
                # 20%: Landing page + instance probe
                $resp = $client.GetAsync("$url/api/telemetry/instance").GetAwaiter().GetResult()
                if ($resp.IsSuccessStatusCode) {
                    $json = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
                    if ($json.instance) { $instances[$json.instance] = ($instances[$json.instance] + 1) }
                    $ok++
                } else { $fail++ }
            }
        }
        catch {
            $fail++
        }
        finally {
            $sw.Stop()
            $latencies.Add($sw.Elapsed.TotalMilliseconds)
        }
    }

    $client.Dispose()
    [PSCustomObject]@{
        Ok        = $ok
        Fail      = $fail
        Latencies = $latencies.ToArray()
        Instances = $instances
    }
}

for ($i = 1; $i -le $ConcurrentUsers; $i++) {
    $jobs += Start-Job -ScriptBlock $workerScript -ArgumentList $BaseUrl, $stopAt, $IngestKey, $Simulations, $i
}

while (($jobs | Where-Object { $_.State -eq 'Running' }).Count -gt 0) {
    $remaining = [math]::Max(0, [int]($stopAt - (Get-Date)).TotalSeconds)
    Write-Host "`rRunning load test... $remaining s remaining   " -NoNewline -ForegroundColor Yellow
    Start-Sleep -Seconds 2
}
Write-Host ""

$results = $jobs | Receive-Job
$jobs | Remove-Job

$totalOk = ($results | Measure-Object -Property Ok -Sum).Sum
$totalFail = ($results | Measure-Object -Property Fail -Sum).Sum
$allLat = @($results | ForEach-Object { $_.Latencies } | Sort-Object)
$avgLat = if ($allLat.Count -gt 0) { [math]::Round(($allLat | Measure-Object -Average).Average, 1) } else { 0 }
$p95Lat = if ($allLat.Count -gt 0) { [math]::Round($allLat[[int]([math]::Min($allLat.Count - 1, $allLat.Count * 0.95))], 1) } else { 0 }
$rps = [math]::Round(($totalOk + $totalFail) / $DurationSeconds, 2)

$mergedInstances = @{ }
foreach ($r in $results) {
    foreach ($k in $r.Instances.Keys) {
        $mergedInstances[$k] = ($mergedInstances[$k] + $r.Instances[$k])
    }
}

Write-Host ""
Write-Host "================ LOAD TEST SUMMARY ================" -ForegroundColor Cyan
Write-Host ("Total Successful Requests : {0}" -f $totalOk) -ForegroundColor Green
Write-Host ("Total Failed Requests     : {0}" -f $totalFail) -ForegroundColor ($(if ($totalFail -gt 0) { "Red" } else { "Green" }))
Write-Host ("Throughput                : {0} req/s" -f $rps)
Write-Host ("Average Latency           : {0} ms" -f $avgLat)
Write-Host ("95th Percentile Latency   : {0} ms" -f $p95Lat)
Write-Host "App Service Instances Hit :" -ForegroundColor Cyan
foreach ($k in $mergedInstances.Keys) {
    Write-Host ("  - Instance {0} : {1} requests" -f $k, $mergedInstances[$k])
}
Write-Host "===================================================" -ForegroundColor Cyan
