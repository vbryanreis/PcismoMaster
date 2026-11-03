# Ultimate (FR33THY, MIT) · Advanced/12 Hardware Legacy Flip.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# fullscreen exclusive fse
reg add "HKCU\System\GameConfigStore" /v "GameDVR_DXGIHonorFSEWindowsCompatible" /t REG_DWORD /d "1" /f | Out-Null
reg add "HKCU\System\GameConfigStore" /v "GameDVR_FSEBehaviorMode" /t REG_DWORD /d "2" /f | Out-Null
reg add "HKCU\System\GameConfigStore" /v "GameDVR_FSEBehavior" /t REG_DWORD /d "2" /f | Out-Null
reg add "HKCU\System\GameConfigStore" /v "GameDVR_HonorUserFSEBehaviorMode" /t REG_DWORD /d "1" /f | Out-Null

Write-Host "May also be required:`n"
Write-Host "- game.exe"
Write-Host "- properties"
Write-Host "- compatibility"
Write-Host "- disable fullscreen optimizations"
Write-Host "- apply`n"
Write-Host "DX12 does not support fullscreen exclusive mode`n"
