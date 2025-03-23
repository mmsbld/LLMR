# install_python_windows.ps1

Param(
    [Parameter(Mandatory = $true)]
    [string]$pythonInstallDir
)

Write-Host "<ip_win.ps1> Starting Python environment setup..."
Write-Host "<ip_win.ps1> Python Install Directory: $pythonInstallDir"

$pythonExecutable = Join-Path -Path $pythonInstallDir -ChildPath "envs\myenv\python.exe"

if (Test-Path $pythonExecutable) {
    Write-Host "<ip_win.ps1> Python installation already exists at $pythonInstallDir."
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
try {
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing -ErrorAction Stop
    Write-Host "<ip_win.ps1> Downloaded Miniforge installer to $installerPath."
}
catch {
    Write-Error "<ip_win.ps1> Failed to download Miniforge installer: $_"
    exit 1
}

Write-Host "<ip_win.ps1> Installing Miniforge to $pythonInstallDir..."
$pythonInstallDir = $pythonInstallDir.TrimEnd('\')

$installArgs = @(
    '/InstallationType=JustMe',
    '/RegisterPython=0',
    '/AddToPath=0',
    '/S',
    "/D=$pythonInstallDir"
)

$process = Start-Process -FilePath $installerPath -ArgumentList $installArgs -Wait -NoNewWindow -PassThru

if ($process.ExitCode -ne 0) {
    Write-Error "<ip_win.ps1> Miniforge installation failed with exit code $($process.ExitCode)."
    $logFile = Join-Path -Path $env:TEMP -ChildPath "Miniforge3.log"
    if (Test-Path $logFile) {
        Write-Host "<ip_win.ps1> Installer log content:"
        Get-Content $logFile | Write-Host
    }
    exit 1
}

Write-Host "<ip_win.ps1> Creating Conda environment with Python 3.12.0..."
$condaExe = Join-Path -Path $pythonInstallDir -ChildPath "Scripts\conda.exe"

try {
    & $condaExe create -y -n myenv python=3.12.0
    Write-Host "<ip_win.ps1> Conda environment 'myenv' created successfully."
    if (-not (& $condaExe info --envs | Select-String "myenv")) {
        Write-Error "<ip_win.ps1> Conda environment 'myenv' was not created successfully."
        exit 1
    }
}
catch {
    Write-Error "<ip_win.ps1> Conda environment creation failed: $_"
    exit 1
}

Write-Host "<ip_win.ps1> Installing required Python packages..."
& "$condaExe" run -n myenv pip install --upgrade pip
& "$condaExe" run -n myenv pip install requests==2.31 openai==1.63.2 gradio==5.16.1

Write-Host "<ip_win.ps1> Cleaning up..."
Remove-Item $installerPath -Force

Write-Host "<ip_win.ps1> Python environment installed successfully at $pythonInstallDir."

# Alternative to manual path construction: directly setting environment variable
# ! needs this in PES.cs: _pythonPath = Environment.GetEnvironmentVariable("LLMR_PYTHON_PATH") ?? throw new ArgumentNullException("LLMR_PYTHON_PATH environment variable is not set.");

#[Environment]::SetEnvironmentVariable("LLMR_PYTHON_PATH", "$pythonInstallDir\envs\myenv\python.exe", "User")