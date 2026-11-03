# Ultimate (FR33THY, MIT) · Advanced/11 Mpo.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "Multiplane Overlay & Optimizations For Windowed Games:"



# enable multiplane overlay
cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\Dwm`" /v `"OverlayTestMode`" /f >nul 2>&1"

# enable optimizations for windowed games
reg add "HKCU\Software\Microsoft\DirectX\UserGpuPreferences" /v "DirectXUserGlobalSettings" /t REG_SZ /d "VRROptimizeEnable=0;SwapEffectUpgradeEnable=1;" /f | Out-Null
