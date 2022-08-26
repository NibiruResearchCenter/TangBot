$Plugins = New-Object System.Collections.Generic.Dictionary"[String, String]"

$Plugins.Add('src/RoleReaction', 'tb-role-reaction')
$Plugins.Add('src/BilibiliLiveInformer', 'tb-bilibili-live-informer')

$OutputDirectory = [System.IO.Path]::Combine($PSScriptRoot, 'publish')

if ([System.IO.Directory]::Exists($OutputDirectory)) {
    [System.IO.Directory]::Delete($OutputDirectory, $true)
}

[System.IO.Directory]::CreateDirectory($OutputDirectory)

foreach ($Key in $Plugins.Keys) {
    $PluginProjectDir = [System.IO.Path]::Combine($PSScriptRoot, $Key)
    $PluginPublishDir = [System.IO.Path]::Combine($PluginProjectDir, 'bin/publish')
    $PluginOutputFile = [System.IO.Path]::Combine($PSScriptRoot, 'publish', $Plugins[$Key] + '.zip')

    dotnet publish -c Release -o $PluginPublishDir $PluginProjectDir

    Compress-Archive -Path $PluginPublishDir/* -Destination $PluginOutputFile
}
