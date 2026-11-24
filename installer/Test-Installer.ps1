param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath
)

$ErrorActionPreference = 'Stop'
$installer = (Resolve-Path $InstallerPath).Path
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "AstroDay-installer-smoke-$([guid]::NewGuid())"
$installDirectory = Join-Path $testRoot 'app'
$installLog = Join-Path $testRoot 'install.log'
$uninstallLog = Join-Path $testRoot 'uninstall.log'

New-Item -ItemType Directory -Path $testRoot | Out-Null

try {
    $install = Start-Process -FilePath $installer -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/SP-',
        '/TASKS=',
        "/DIR=$installDirectory",
        "/LOG=$installLog"
    ) -Wait -PassThru

    if ($install.ExitCode -ne 0) {
        throw "Installer exited with code $($install.ExitCode). See $installLog."
    }
    if (-not (Test-Path (Join-Path $installDirectory 'AstroDay.exe'))) {
        throw 'Installer completed without installing AstroDay.exe.'
    }
    if (-not (Test-Path (Join-Path $installDirectory 'unins000.exe'))) {
        throw 'Installer completed without creating its uninstaller.'
    }

    $uninstall = Start-Process -FilePath (Join-Path $installDirectory 'unins000.exe') -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        "/LOG=$uninstallLog"
    ) -Wait -PassThru

    if ($uninstall.ExitCode -ne 0) {
        throw "Uninstaller exited with code $($uninstall.ExitCode). See $uninstallLog."
    }
}
finally {
    if (Test-Path $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
