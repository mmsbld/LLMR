@echo off
setlocal

REM TODO: Needs to be updated to *.dylib copy approach!
if "%~1"=="" (
    echo <ip_win.bat> No installation directory specified.
    exit /b 1
)

set "PYTHON_INSTALL_DIR=%~1"

if exist "%PYTHON_INSTALL_DIR%" (
    echo <ip_win.bat> Python virtual environment already exists at %PYTHON_INSTALL_DIR%.
    exit /b 0
) else (
    echo <ip_win.bat> Python virtual environment not found at %PYTHON_INSTALL_DIR%. Proceeding with installation.
)

echo <ip_win.bat> Installing Python 3.12 and required packages...

REM check if Python 3.12 is installed
for /f "tokens=2 delims==" %%a in ('py -3.12 --version 2^>nul') do (
    set "PYTHON_VERSION=%%a"
)

if "%PYTHON_VERSION%"=="" (
    echo <ip_win.bat> Python 3.12 is not installed. Downloading installer...
    set "PYTHON_INSTALLER=python-3.12.0-amd64.exe"
    powershell -Command "(New-Object Net.WebClient).DownloadFile('https://www.python.org/ftp/python/3.12.0/python-3.12.0-amd64.exe', '%PYTHON_INSTALLER%')"
    echo <ip_win.bat> Installing Python 3.12...
    start /wait "" "%PYTHON_INSTALLER%" /quiet InstallAllUsers=1 PrependPath=1 Include_test=0
    del "%PYTHON_INSTALLER%"
) else (
    echo <ip_win.bat> Python 3.12 is already installed.
)

echo <ip_win.bat> Creating virtual environment...
py -3.12 -m venv "%PYTHON_INSTALL_DIR%"

echo <ip_win.bat> Activating virtual environment...
call "%PYTHON_INSTALL_DIR%\Scripts\activate.bat"

echo <ip_win.bat> Upgrading pip...
python -m pip install --upgrade pip

echo <ip_win.bat> Installing required packages...
pip install openai==1.54 gradio==5.4

echo <ip_win.bat> Python environment installed successfully.

endlocal
