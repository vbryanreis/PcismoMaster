# Ultimate (FR33THY, MIT) · Windows/27 Network IPv4 Only.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        # SCRIPT SILENT
        $progresspreference = 'silentlycontinue'




Write-Host "Network: Default..`n"

# enable all network adapters
$adapterstoenable = @('ms_lldp', 'ms_lltdio', 'ms_implat', 'ms_tcpip', 'ms_rspndr', 'ms_tcpip6', 'ms_server', 'ms_msclient', 'ms_pacer')
foreach ($adapterbinding in $adapterstoenable) {
Enable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
}

# again
$adapterstoenable = @('ms_lldp', 'ms_lltdio', 'ms_implat', 'ms_tcpip', 'ms_rspndr', 'ms_tcpip6', 'ms_server', 'ms_msclient', 'ms_pacer')
foreach ($adapterbinding in $adapterstoenable) {
Enable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
}

# open settings
get-netadapterbinding | select-object name, displayname, componentid, enabled | format-table -autosize
