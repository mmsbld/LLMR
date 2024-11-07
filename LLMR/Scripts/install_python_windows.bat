@echo off
setlocal enabledelayedexpansion

if "%~1"=="" (
    echo <install_python_windows.bat> No installation directory specified.
    exit /b 1
)

set "PYTHON_INSTALL_DIR=%~1"
set "LOG_FILE=%~dp0install_python_windows.log"

echo Installing Python environment to %PYTHON_INSTALL_DIR% > "%LOG_FILE%"

if exist "%PYTHON_INSTALL_DIR%" (
    echo <install_python_windows.bat> Python installation already exists at %PYTHON_INSTALL_DIR%. >> "%LOG_FILE%"
    exit /b 0
) else (
    echo <install_python_windows.bat> Creating installation directory at %PYTHON_INSTALL_DIR%. >> "%LOG_FILE%"
    mkdir "%PYTHON_INSTALL_DIR%" >> "%LOG_FILE%" 2>&1
    if errorlevel 1 (
        echo <install_python_windows.bat> Failed to create installation directory. >> "%LOG_FILE%"
        exit /b 1
    )
)

echo <install_python_windows.bat> Downloading Python 3.12.0 installer... >> "%LOG_FILE%"

set "PYTHON_VERSION=3.12.0"
set "PYTHON_INSTALLER=python-%PYTHON_VERSION%-amd64.exe"
curl -L -o "%PYTHON_INSTALLER%" "https://www.python.org/ftp/python/%PYTHON_VERSION%/%PYTHON_INSTALLER%" >> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo <install_python_windows.bat> Failed to download Python installer. >> "%LOG_FILE%"
    exit /b 1
)

echo <install_python_windows.bat> Installing Python 3.12.0... >> "%LOG_FILE%"

"%PYTHON_INSTALLER%" /quiet InstallAllUsers=1 TargetDir="%PYTHON_INSTALL_DIR%" PrependPath=0 Include_pip=1 Include_test=0 >> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo <install_python_windows.bat> Python installation failed. >> "%LOG_FILE%"
    exit /b 1
)

echo <install_python_windows.bat> Python installed successfully. >> "%LOG_FILE%"

del "%PYTHON_INSTALLER%" >> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo <install_python_windows.bat> Failed to delete installer. >> "%LOG_FILE%"
)

echo <install_python_windows.bat> Installing required Python packages... >> "%LOG_FILE%"

"%PYTHON_INSTALL_DIR%\python.exe" -m pip install --upgrade pip setuptools >> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo <install_python_windows.bat> Failed to upgrade pip and setuptools. >> "%LOG_FILE%"
    exit /b 1
)

"%PYTHON_INSTALL_DIR%\python.exe" -m pip install openai==1.54 requests==2.31 gradio==5.1.0 >> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo <install_python_windows.bat> Failed to install required Python packages. >> "%LOG_FILE%"
    exit /b 1
)

echo <install_python_windows.bat> Python environment installed successfully. >> "%LOG_FILE%"
echo Installation complete. >> "%LOG_FILE%"
pause