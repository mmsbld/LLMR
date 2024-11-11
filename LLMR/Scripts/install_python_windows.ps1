# install_python_windows.ps1

Param(
    [Parameter(Mandatory = $true)]
    [string]$pythonInstallDir
)

Write-Host "<ip_win.ps1> Starting Python environment setup..."

$pythonExecutable = Join-Path -Path $pythonInstallDir -ChildPath "envs\myenv\python.exe"

if (Test-Path $pythonExecutable) {
    Write-Host "<ip_win.ps1> Python installation already exists at $pythonInstallDir."
    exit 0
}

Write-Host "<ip_win.ps1> Proceeding with installation..."

# Determine system architecture
$arch = (Get-WmiObject Win32_OperatingSystem).OSArchitecture
if ($arch -eq "64-bit") {
    $miniforgeInstaller = "Miniforge3-Windows-x86_64.exe"
} else {
    Write-Error "<ip_win.ps1> Unsupported architecture: $arch"
    exit 1
}

$installerUrl = "https://github.com/conda-forge/miniforge/releases/latest/download/$miniforgeInstaller"
$installerPath = Join-Path -Path $env:TEMP -ChildPath $miniforgeInstaller

Write-Host "<ip_win.ps1> Downloading Miniforge installer for $arch..."

Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing

Write-Host "<ip_win.ps1> Installing Miniforge to $pythonInstallDir..."

& $installerPath /InstallationType=JustMe /RegisterPython=0 /AddToPath=0 /S /D=$pythonInstallDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "<ip_win.ps1> Miniforge installation failed."
    exit 1
}

Write-Host "<ip_win.ps1> Creating Conda environment with Python 3.12.0..."

$condaExe = Join-Path -Path $pythonInstallDir -ChildPath "Scripts\conda.exe"

& $condaExe create -y -n myenv python=3.12.0

if ($LASTEXITCODE -ne 0) {
    Write-Error "<ip_win.ps1> Conda environment creation failed."
    exit 1
}

Write-Host "<ip_win.ps1> Installing required Python packages..."

$activateScript = Join-Path -Path $pythonInstallDir -ChildPath "Scripts\activate.bat"

$installPackagesCmd = "$activateScript myenv && pip install openai==1.54 requests==2.31 gradio==5.1.0"

cmd.exe /c $installPackagesCmd

if ($LASTEXITCODE -ne 0) {
    Write-Error "<ip_win.ps1> Failed to install required Python packages."
    exit 1
}

Write-Host "<ip_win.ps1> Deactivating environment..."

$deactivateScript = Join-Path -Path $pythonInstallDir -ChildPath "Scripts\deactivate.bat"

& $deactivateScript

Write-Host "<ip_win.ps1> Cleaning up..."

Remove-Item $installerPath -Force

Write-Host "<ip_win.ps1> Python environment installed successfully at $pythonInstallDir."
