$pythonVersion = "3.12.0"
$pythonInstallerUrl = "https://www.python.org/ftp/python/$pythonVersion/python-$pythonVersion-amd64.exe"
$pythonInstallerPath = "$env:TEMP\python-$pythonVersion-amd64.exe"
$pythonInstallDir = "$env:ProgramFiles\Python$($pythonVersion.Replace('.', ''))"
$logFile = "$env:TEMP\install_python_powershell.log"

function Log-Message {
    param([string]$message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $message" | Out-File -FilePath $logFile -Append
}

Log-Message "Starting Python $pythonVersion installation."

Log-Message "Downloading Python installer from $pythonInstallerUrl to $pythonInstallerPath."
try {
    Invoke-WebRequest -Uri $pythonInstallerUrl -OutFile $pythonInstallerPath -UseBasicParsing -ErrorAction Stop
    Log-Message "Download completed."
}
catch {
    Log-Message "Failed to download Python installer: $_"
    exit 1
}

Log-Message "Installing Python to $pythonInstallDir."
Start-Process -FilePath $pythonInstallerPath -ArgumentList "/quiet", "InstallAllUsers=1", "PrependPath=1", "TargetDir=$pythonInstallDir" -Wait

$pythonExePath = "$pythonInstallDir\python.exe"
if (Test-Path -Path $pythonExePath) {
    Log-Message "Python installed successfully at $pythonInstallDir."
}
else {
    Log-Message "Python installation failed: python.exe not found at $pythonInstallDir."
    Remove-Item -Path $pythonInstallerPath -Force
    exit 1
}

$envPath = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)
if ($envPath -notlike "*$pythonInstallDir*") {
    Log-Message "Adding Python to system PATH."
    [System.Environment]::SetEnvironmentVariable("Path", "$envPath;$pythonInstallDir", [System.EnvironmentVariableTarget]::Machine)
    Log-Message "Python added to system PATH."
}
else {
    Log-Message "Python is already in the system PATH."
}

Log-Message "Installing required Python packages (openai, requests, gradio)."
try {
    Start-Process -FilePath $pythonExePath -ArgumentList "-m pip install --upgrade pip setuptools" -Wait
    Start-Process -FilePath $pythonExePath -ArgumentList "-m pip install openai==1.54 requests==2.31 gradio==5.1.0" -Wait
    Log-Message "Python packages installed successfully."
}
catch {
    Log-Message "Failed to install required Python packages: $_"
    exit 1
}

Log-Message "Cleaning up: Removing Python installer."
Remove-Item -Path $pythonInstallerPath -Force

Log-Message "Python installation and setup completed successfully."

exit 0