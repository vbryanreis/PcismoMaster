# Ultimate (FR33THY, MIT) · 3 Setup/5 Updates Drivers Block.ps1 (opção 3)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Unblocked: Driver Updates"

# revert block all windows driver updates
reg delete "HKLM\Software\Policies\Microsoft\Windows\Device Metadata" /v "PreventDeviceMetadataFromNetwork" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\DeviceInstall\Settings" /v "DisableSendGenericDriverNotFoundToWER" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\DeviceInstall\Settings" /v "DisableSendRequestAdditionalSoftwareToWER" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\DriverSearching" /v "SearchOrderConfig" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "SetAllowOptionalContent" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "AllowTemporaryEnterpriseFeatureControl" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "ExcludeWUDriversInQualityUpdate" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "IncludeRecommendedUpdates" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "EnableFeaturedSoftware" /f | Out-Null
