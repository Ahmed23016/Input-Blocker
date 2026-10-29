param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$toolPath = Join-Path $root '.tools/wix'
if (!(Test-Path "$toolPath/wix.exe")) {
    & $DotNet tool install wix --version 6.0.2 --tool-path $toolPath
    if ($LASTEXITCODE -ne 0) { throw 'WiX installation failed' }
}
$publish = Join-Path $root 'artifacts/win-x64'
$components = [System.Text.StringBuilder]::new()
$refs = [System.Text.StringBuilder]::new()
function StableId([string]$path) { 'C' + [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($path))).Substring(0,24) }
function AddDirectory([string]$path, [string]$directoryId) {
    Get-ChildItem -LiteralPath $path -File | Sort-Object Name | ForEach-Object {
        $id = StableId ([System.IO.Path]::GetRelativePath($publish,$_.FullName))
        $guid = [Guid]::new([System.Security.Cryptography.MD5]::HashData([System.Text.Encoding]::UTF8.GetBytes('InputBlocker.Component.' + $id))).ToString()
        $source = [System.Security.SecurityElement]::Escape($_.FullName)
        $shortcut = if ($_.Name -eq 'InputBlocker.exe') { '<Shortcut Id="StartMenuShortcut" Directory="ProgramMenuFolder" Name="Input Blocker" WorkingDirectory="INSTALLFOLDER" Advertise="no"/>' } else { '' }
        [void]$components.AppendLine("<Component Id=`"$id`" Guid=`"$guid`"><File Id=`"${id}Data`" Source=`"$source`">$shortcut</File><RegistryValue Root=`"HKCU`" Key=`"Software\InputBlocker\Components`" Name=`"$id`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`"/></Component>")
        [void]$refs.AppendLine("<ComponentRef Id=`"$id`"/>")
    }
    $cleanupId = StableId ($directoryId + 'cleanup')
    [void]$components.AppendLine("<Component Id=`"$cleanupId`" Guid=`"*`"><RemoveFolder Id=`"${cleanupId}Folder`" On=`"uninstall`"/><RegistryValue Root=`"HKCU`" Key=`"Software\InputBlocker\Components`" Name=`"$cleanupId`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`"/></Component>")
    [void]$refs.AppendLine("<ComponentRef Id=`"$cleanupId`"/>")
    Get-ChildItem -LiteralPath $path -Directory | Sort-Object Name | ForEach-Object {
        $childId = StableId ([System.IO.Path]::GetRelativePath($publish,$_.FullName))
        $name = [System.Security.SecurityElement]::Escape($_.Name)
        [void]$components.AppendLine("<Directory Id=`"$childId`" Name=`"$name`">")
        AddDirectory $_.FullName $childId
        [void]$components.AppendLine('</Directory>')
    }
}
AddDirectory $publish 'INSTALLFOLDER'
$xml = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
 <Package Name="Input Blocker" Manufacturer="Input Blocker" Version="1.0.0" UpgradeCode="5C74B52F-102B-4B1C-8319-56180303456E" Scope="perUser">
  <MajorUpgrade DowngradeErrorMessage="A newer version is already installed."/>
  <MediaTemplate EmbedCab="yes" CompressionLevel="low"/>
  <Icon Id="AppIcon" SourceFile="$root/src/InputBlocker/Assets/app.ico"/>
  <Property Id="ARPPRODUCTICON" Value="AppIcon"/>
  <StandardDirectory Id="LocalAppDataFolder"><Directory Id="INSTALLFOLDER" Name="InputBlockerApp">
   $components
   <Component Id="StartupCleanup" Guid="*"><RegistryValue Root="HKCU" Key="Software\InputBlocker" Name="Installed" Type="integer" Value="1" KeyPath="yes"/><RegistryValue Root="HKCU" Key="Software\Microsoft\Windows\CurrentVersion\Run" Name="InputBlocker" Type="string" Value=""/></Component>
  </Directory></StandardDirectory>
  <StandardDirectory Id="ProgramMenuFolder"/>
  <Feature Id="Main" Title="Input Blocker" Level="1">$refs<ComponentRef Id="StartupCleanup"/></Feature>
 </Package>
</Wix>
"@
$sourceFile = Join-Path $root 'artifacts/installer.wxs'
[System.IO.File]::WriteAllText($sourceFile,$xml)
& "$toolPath/wix.exe" build $sourceFile -arch x64 -o "$root/artifacts/InputBlocker-1.0.0-x64.msi"
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
