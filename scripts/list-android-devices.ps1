# List Android Devices Script (PowerShell)
Write-Host "🔍 Checking for connected Android devices..." -ForegroundColor Cyan

# Check if adb is available
try {
    $null = & adb version 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "adb not found"
    }
}
catch {
    Write-Host "❌ Error: adb command not found." -ForegroundColor Red
    Write-Host "Please install Android SDK Platform Tools and add them to your PATH." -ForegroundColor Yellow
    exit 1
}

# Get device list
$devicesOutput = & adb devices 2>$null
$devices = $devicesOutput | Select-String '\tdevice$' | ForEach-Object { ($_ -split '\t')[0] }

if ($devices.Count -eq 0) {
    Write-Host "❌ No Android devices connected!" -ForegroundColor Red
    Write-Host "Please connect an Android device and enable USB debugging." -ForegroundColor Yellow
}
else {
    Write-Host "✅ Found $($devices.Count) Android device$(if ($devices.Count -gt 1) { 's' }):" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Cyan
    
    foreach ($device in $devices) {
        # Get device information
        $model = & adb -s $device shell getprop ro.product.model 2>$null
        $manufacturer = & adb -s $device shell getprop ro.product.manufacturer 2>$null
        $androidVersion = & adb -s $device shell getprop ro.build.version.release 2>$null
        $apiLevel = & adb -s $device shell getprop ro.build.version.sdk 2>$null
        
        Write-Host ""
        Write-Host "Device ID: $device" -ForegroundColor White
        
        if ($model -and $manufacturer) {
            $model = $model.Trim()
            $manufacturer = $manufacturer.Trim()
            Write-Host "  Model: $manufacturer $model" -ForegroundColor Gray
        }
        
        if ($androidVersion -and $apiLevel) {
            $androidVersion = $androidVersion.Trim()
            $apiLevel = $apiLevel.Trim()
            Write-Host "  Android: $androidVersion (API $apiLevel)" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
}