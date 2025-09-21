# List connected Android devices for VS Code input selection
# Returns device IDs that can be used with adb

# Get list of connected devices (excluding header and offline devices)
$devices = adb devices | Where-Object { $_ -match '\tdevice$' } | ForEach-Object { ($_ -split '\t')[0] }

if (-not $devices) {
    Write-Host "No connected Android devices found."
    Write-Host "Please connect an Android device and enable USB debugging."
    exit 1
}

Write-Host "Connected Android devices:"
Write-Host "========================="

# Enhanced device listing with model names when possible
foreach ($device in $devices) {
    # Try to get device model name
    $model = (adb -s $device shell getprop ro.product.model 2>$null) -replace "`r", ""
    $manufacturer = (adb -s $device shell getprop ro.product.manufacturer 2>$null) -replace "`r", ""
    
    if ($model -and $manufacturer) {
        Write-Host "Device ID: $device ($manufacturer $model)"
    } else {
        Write-Host "Device ID: $device"
    }
}

Write-Host ""
Write-Host "To use a specific device, copy the Device ID and use it in the VS Code task prompt."