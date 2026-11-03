# Ultimate (FR33THY, MIT) · Advanced/6 MMAgent Features.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "MMAgent Features:"



Write-Host "MMAgent Features: Off"


# force disable applicationlaunchprefetching & operationapi
cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters`" /v `"EnablePrefetcher`" /t REG_DWORD /d `"0`" /f >nul 2>&1"

# disable applicationlaunchprefetching
Disable-MMAgent -ApplicationLaunchPrefetching -ErrorAction SilentlyContinue | Out-Null

# disable applicationprelaunch
Disable-MMAgent -ApplicationPreLaunch -ErrorAction SilentlyContinue | Out-Null

# disable maxoperationapifiles
Set-MMAgent -MaxOperationAPIFiles 1 -ErrorAction SilentlyContinue | Out-Null

# disable memorycompression
Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue | Out-Null

# disable operationapi
Disable-MMAgent -OperationAPI -ErrorAction SilentlyContinue | Out-Null

# disable pagecombining
Disable-MMAgent -PageCombining -ErrorAction SilentlyContinue | Out-Null
