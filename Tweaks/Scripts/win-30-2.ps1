# Ultimate (FR33THY, MIT) · Windows/30 Timer Resolution.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# stop disable delete service
Set-Service -Name "Set Timer Resolution Service" -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null
Set-Service -Name "Set Timer Resolution Service" -Status Stopped -ErrorAction SilentlyContinue | Out-Null
sc.exe delete "Set Timer Resolution Service" | Out-Null

# delete file
Remove-Item "$env:SystemDrive\Windows\SetTimerResolutionService.exe" -Force -ErrorAction SilentlyContinue | Out-Null

# disable global timer resolution requests
cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel`" /v `"GlobalTimerResolutionRequests`" /f >nul 2>&1"

# start taskmanager
Start-Process taskmgr.exe
