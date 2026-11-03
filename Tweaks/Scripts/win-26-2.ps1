# Ultimate (FR33THY, MIT) · Windows/26 Network Adapter Power Savings & Wake.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Network Adapter Power Savings & Wake: Default..."

# revert disable network adapter powersaving & wake on all connected devices
$basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}"
$adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
foreach ($key in $adapterKeys) {
if ($key.PSChildName -match '^\d{4}$') {
$regPath = $key.Name

# revert disable adapter powersaving & wake
cmd /c "reg delete `"$regPath`" /v `"PnPCapabilities`" /f >nul 2>&1"

# revert disable advanced energy efficient ethernet
cmd /c "reg delete `"$regPath`" /v `"AdvancedEEE`" /f >nul 2>&1"

# revert disable energy-efficient ethernet
cmd /c "reg delete `"$regPath`" /v `"*EEE`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"EEELinkAdvertisement`" /f >nul 2>&1"

# revert system idle power saver
cmd /c "reg delete `"$regPath`" /v `"SipsEnabled`" /f >nul 2>&1"

# revert ultra low power mode
cmd /c "reg delete `"$regPath`" /v `"ULPMode`" /f >nul 2>&1"

# revert disable gigabit lite
cmd /c "reg delete `"$regPath`" /v `"GigaLite`" /f >nul 2>&1"

# revert disable green ethernet
cmd /c "reg delete `"$regPath`" /v `"EnableGreenEthernet`" /f >nul 2>&1"

# revert disable power saving mode
cmd /c "reg delete `"$regPath`" /v `"PowerSavingMode`" /f >nul 2>&1"

# revert disable all wake
cmd /c "reg delete `"$regPath`" /v `"S5WakeOnLan`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"*WakeOnMagicPacket`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"*ModernStandbyWoLMagicPacket`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"*WakeOnPattern`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"WakeOnLink`" /f >nul 2>&1"
cmd /c "reg delete `"$regPath`" /v `"*ModernStandbyWoLMagicPacket`" /f >nul 2>&1"
}
}
