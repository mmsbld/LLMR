@echo off
setlocal enabledelayedexpansion

if "%~1"=="" (
    echo <install_python_windows.bat> No installation directory specified.
    exit /b 1
)

set "PYTHON_INSTALL_DIR=%~1"

if exist "%PYTHON_INSTALL_DIR%" (
    echo <install_python_windows.bat> Python installation already exists at %PYTHON_INSTALL_DIR%.
    exit /b 0
) else (
    echo <install_python_windows.bat> Creating installation directory at %PYTHON_INSTALL_DIR%.
    mkdir "%PYTHON_INSTALL_DIR%"
)

echo <install_python_windows.bat> Downloading Python 3.12.0 installer...

set "PYTHON_VERSION=3.12.0"
set "PYTHON_INSTALLER=python-%PYTHON_VERSION%-amd64.exe"
curl -L -o "%PYTHON_INSTALLER%" "https://www.python.org/ftp/python/%PYTHON_VERSION%/%PYTHON_INSTALLER%"

echo <install_python_windows.bat> Installing Python 3.12.0...

"%PYTHON_INSTALLER%" /quiet InstallAllUsers=1 TargetDir="%PYTHON_INSTALL_DIR%" PrependPath=0 Include_pip=1 Include_test=0

del "%PYTHON_INSTALLER%"

echo <install_python_windows.bat> Installing required Python packages...

"%PYTHON_INSTALL_DIR%\python.exe" -m pip install --upgrade pip setuptools
"%PYTHON_INSTALL_DIR%\python.exe" -m pip install openai==1.54 requests==2.31 gradio==5.1.0

echo <install_python_windows.bat> Python environment installed successfully.