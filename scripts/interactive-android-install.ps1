# Interactive Android Install Script (PowerShell)
# Usage: interactive-android-install.ps1 <project_path>

param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath
)

Write-Host "🔍 Checking for Android devices..." -ForegroundColor Cyan

# Get connected devices
$devicesOutput = & adb devices 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error: adb command not found. Please install Android SDK Platform Tools." -ForegroundColor Red
    exit 1
}

$devices = $devicesOutput | Select-String '\tdevice$' | ForEach-Object { ($_ -split '\t')[0] }
$deviceCount = $devices.Count

if ($deviceCount -eq 0) {
    Write-Host "❌ No Android devices connected!" -ForegroundColor Red
    Write-Host "Please connect an Android device and enable USB debugging." -ForegroundColor Yellow
    exit 1
}
elseif ($deviceCount -eq 1) {
    $device = $devices[0]
    Write-Host "✅ Found single device: $device" -ForegroundColor Green
    Write-Host "🚀 Building and installing..." -ForegroundColor Cyan
    & dotnet msbuild $ProjectPath -t:BuildAndroidInstall -p:AndroidDeviceId="$device"
}
else {
    Write-Host "📱 Multiple devices detected ($deviceCount devices):" -ForegroundColor Yellow
    Write-Host "======================================================" -ForegroundColor Yellow
    
    # Display device options
    $deviceArray = @()
    for ($i = 0; $i -lt $devices.Count; $i++) {
        $dev = $devices[$i]
        $deviceArray += $dev
        
        # Try to get device model name
        $model = & adb -s $dev shell getprop ro.product.model 2>$null
        $manufacturer = & adb -s $dev shell getprop ro.product.manufacturer 2>$null
        
        if ($model -and $manufacturer) {
            $model = $model.Trim()
            $manufacturer = $manufacturer.Trim()
            Write-Host "$($i + 1)) $dev ($manufacturer $model)" -ForegroundColor White
        }
        else {
            Write-Host "$($i + 1)) $dev" -ForegroundColor White
        }
    }
    
    Write-Host ""
    
    # Get user selection
    do {
        $selection = Read-Host "Select device (1-$deviceCount)"
        $selectionNum = 0
        
        if ([int]::TryParse($selection, [ref]$selectionNum) -and $selectionNum -ge 1 -and $selectionNum -le $deviceCount) {
            $selectedDevice = $deviceArray[$selectionNum - 1]
            Write-Host "✅ Selected device: $selectedDevice" -ForegroundColor Green
            Write-Host "🚀 Building and installing..." -ForegroundColor Cyan
            & dotnet msbuild $ProjectPath -t:BuildAndroidInstall -p:AndroidDeviceId="$selectedDevice"
            break
        }
        else {
            Write-Host "❌ Invalid selection. Please enter a number between 1 and $deviceCount." -ForegroundColor Red
        }
    } while ($true)
}