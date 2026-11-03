# Ultimate (FR33THY, MIT) · Windows/25 Device Manager Power Savings & Wake.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Device Manager Power Savings & Wake: Off..."

# disable acpi power savings on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\ACPI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
cmd /c "reg add `"$regPath`" /v `"SeleactiveSuspendEnabled`" /t REG_BINARY /d `"00`" /f >nul 2>&1"
cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendOn`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\ACPI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "WDF" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"IdleInWorkingState`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}

# disable hid power savings on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\HID" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendEnabled`" /t REG_BINARY /d `"00`" /f >nul 2>&1"
cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendOn`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\HID" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "WDF" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"IdleInWorkingState`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}

# disable pci power savings on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\PCI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendEnabled`" /t REG_BINARY /d `"00`" /f >nul 2>&1"
cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendOn`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\PCI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "WDF" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"IdleInWorkingState`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}

# disable usb power savings on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\USB" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendEnabled`" /t REG_BINARY /d `"00`" /f >nul 2>&1"
cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendOn`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\USB" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "WDF" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"IdleInWorkingState`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}

# disable acpi wake on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\ACPI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"WaitWakeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}

# disable hid wake on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\HID" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"WaitWakeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}

# disable pci wake on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\PCI" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"WaitWakeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}

# disable usb wake on all connected devices
$usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\USB" -Recurse -ErrorAction SilentlyContinue |
Where-Object { $_.PSChildName -eq "Device Parameters" }
foreach ($key in $usbKeys) {
$regPath = $key.Name
cmd /c "reg add `"$regPath`" /v `"WaitWakeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
}
