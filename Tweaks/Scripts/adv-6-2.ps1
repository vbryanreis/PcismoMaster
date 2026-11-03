# Ultimate (FR33THY, MIT) · Advanced/6 MMAgent Features.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "MMAgent Features:"



Write-Host "MMAgent Features: Default"


# enable applicationlaunchprefetching & operationapi
cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters`" /v `"EnablePrefetcher`" /t REG_DWORD /d `"3`" /f >nul 2>&1"

# enable applicationlaunchprefetching
Enable-MMAgent -ApplicationLaunchPrefetching -ErrorAction SilentlyContinue | Out-Null

# enable applicationprelaunch
Enable-MMAgent -ApplicationPreLaunch -ErrorAction SilentlyContinue | Out-Null

# enable maxoperationapifiles
Set-MMAgent -MaxOperationAPIFiles 512 -ErrorAction SilentlyContinue | Out-Null

# disable memorycompression
Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue | Out-Null

# enable operationapi
Enable-MMAgent -OperationAPI -ErrorAction SilentlyContinue | Out-Null

# disable pagecombining
Disable-MMAgent -PageCombining -ErrorAction SilentlyContinue | Out-Null
