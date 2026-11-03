# Ultimate (FR33THY, MIT) · Windows/25 Device Manager Power Savings & Wake.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Device Manager Power Savings & Wake: Default..."

# revert disable acpi power savings on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\ACPI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"SeleactiveSuspendEnabled`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"SelectiveSuspendOn`" /f >nul 2>&1"
}
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\ACPI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "WDF" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"IdleInWorkingState`" /f >nul 2>&1"
}

# revert disable hid power savings on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\HID" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"SeleactiveSuspendEnabled`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"SelectiveSuspendOn`" /f >nul 2>&1"
}
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\HID" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "WDF" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"IdleInWorkingState`" /f >nul 2>&1"
}

# revert disable pci power savings on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\PCI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"SeleactiveSuspendEnabled`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"SelectiveSuspendOn`" /f >nul 2>&1"
}
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\PCI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "WDF" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"IdleInWorkingState`" /f >nul 2>&1"
}

# revert disable usb power savings on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\USB" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"SeleactiveSuspendEnabled`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"SelectiveSuspendOn`" /f >nul 2>&1"
}
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\USB" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "WDF" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"IdleInWorkingState`" /f >nul 2>&1"
}

# revert disable acpi wake on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\ACPI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"WaitWakeEnabled`" /f >nul 2>&1"
}

# revert disable hid wake on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\HID" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"WaitWakeEnabled`" /f >nul 2>&1"
}

# revert disable pci wake on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\PCI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"WaitWakeEnabled`" /f >nul 2>&1"
}

# revert disable usb wake on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\USB" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg delete `"$regPath`" /v `"WaitWakeEnabled`" /f >nul 2>&1"
}
