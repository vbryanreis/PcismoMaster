# Ultimate (FR33THY, MIT) · Windows/4 Context Menu.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# context menu
cmd /c "reg delete `"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}`" /f >nul 2>&1"

# customize this folder
cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer`" /v `"NoCustomizeThisFolder`" /f >nul 2>&1"

# pin to quick access
# add to favorites
$ContextMenuDefault = @"
Windows Registry Editor Version 5.00

; pin to quick access
[HKEY_CLASSES_ROOT\Folder\shell\pintohome]
"AppliesTo"="System.ParsingName:<>\"::{f874310e-b6b7-47dc-bc84-b9e6b38f5903}\" AND System.ParsingName:<>\"::{679f85cb-0220-4080-b29b-5540cc05aab6}\" AND System.IsFolder:=System.StructuredQueryType.Boolean#True"
"CommandStateHandler"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"CommandStateSync"=""
"MUIVerb"="@shell32.dll,-51601"
"SkipCloudDownload"=dword:00000000

[HKEY_CLASSES_ROOT\Folder\shell\pintohome\command]
"DelegateExecute"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"

; add to favorites
[HKEY_CLASSES_ROOT\*\shell\pintohomefile]
"CommandStateHandler"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"CommandStateSync"=""
"MUIVerb"="@shell32.dll,-51608"
"NeverDefault"=""
"SkipCloudDownload"=dword:00000000

[HKEY_CLASSES_ROOT\*\shell\pintohomefile\command]
"DelegateExecute"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"@
Set-Content -Path "$env:SystemRoot\Temp\contextmenudefault.reg" -Value $ContextMenuDefault -Force

# import reg file
Regedit.exe /S "$env:SystemRoot\Temp\contextmenudefault.reg"

# troubleshoot compatibility
cmd /c "reg add `"HKCR\exefile\shellex\ContextMenuHandlers\Compatibility`" /ve /t REG_SZ /d `"{1d27f844-3a1f-4410-85ac-14651078412d}`" /f >nul 2>&1"

# open in terminal
# scan with defender
# give access to
cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /f >nul 2>&1"

# include in library
cmd /c "reg add `"HKCR\Folder\ShellEx\ContextMenuHandlers\Library Location`" /ve /t REG_SZ /d `"{3dad6c5d-2167-4cae-9914-f99e41c12cfa}`" /f >nul 2>&1"

# share
cmd /c "reg add `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\ModernSharing`" /ve /t REG_SZ /d `"{e2bf9676-5f8f-435c-97eb-11607a5bedf7}`" /f >nul 2>&1"

# restore previous versions
cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"NoPreviousVersionsPage`" /f >nul 2>&1"

# send to
cmd /c "reg add `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo`" /ve /t REG_SZ /d `"{7BA4C740-9E81-11CF-99D3-00AA004AE837}`" /f >nul 2>&1"
cmd /c "reg add `"HKCR\UserLibraryFolder\shellex\ContextMenuHandlers\SendTo`" /ve /t REG_SZ /d `"{7BA4C740-9E81-11CF-99D3-00AA004AE837}`" /f >nul 2>&1"
