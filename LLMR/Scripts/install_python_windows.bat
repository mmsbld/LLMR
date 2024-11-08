@echo off
setlocal

set "LOG_FILE=%~dp0install_python_admin.log"

echo Installing Python environment... > "%LOG_FILE%"
echo Timestamp: %DATE% %TIME% >> "%LOG_FILE%"

for /f "tokens=2 delims==" %%a in ('py -3.12 --version 2^>nul') do (
    set "PYTHON_VERSION=%%a"
)

if "%PYTHON_VERSION%"=="" (
    echo Python 3.12 is not installed. Downloading installer... >> "%LOG_FILE%"
    set "PYTHON_INSTALLER=python-3.12.0-amd64.exe"

    powershell -Command "try { Invoke-WebRequest -Uri 'https://www.python.org/ftp/python/3.12.0/python-3.12.0-amd64.exe' -OutFile '%PYTHON_INSTALLER%' -UseBasicParsing -ErrorAction Stop } catch { exit 1 }" >> "%LOG_FILE%" 2>&1

    if %ERRORLEVEL% neq 0 (
        echo Failed to download Python installer. >> "%LOG_FILE%"
        exit /b 1
    )

    echo Installing Python 3.12... >> "%LOG_FILE%"
    start /wait "" "%PYTHON_INSTALLER%" /quiet InstallAllUsers=1 PrependPath=1 Include_test=0 >> "%LOG_FILE%" 2>&1

    timeout /t 5 /nobreak >nul

    set "PYTHON_DEFAULT_DIR=C:\Program Files\Python312"
    set "PYTHON_ALT_DIR=C:\Users\%USERNAME%\AppData\Local\Programs\Python\Python312"

    if exist "%PYTHON_DEFAULT_DIR%\python.exe" (
        set "INSTALLED_PYTHON_PATH=%PYTHON_DEFAULT_DIR%"
        echo Python installed successfully at %PYTHON_DEFAULT_DIR%. >> "%LOG_FILE%"
    ) else if exist "%PYTHON_ALT_DIR%\python.exe" (
        set "INSTALLED_PYTHON_PATH=%PYTHON_ALT_DIR%"
        echo Python installed successfully at %PYTHON_ALT_DIR%. >> "%LOG_FILE%"
    ) else (
        echo Python installation failed: executable not found. >> "%LOG_FILE%"
        if exist "%PYTHON_INSTALLER%" (
            del /f /q "%PYTHON_INSTALLER%" >> "%LOG_FILE%" 2>&1
        )
        exit /b 1
    )

    if exist "%PYTHON_INSTALLER%" (
        del /f /q "%PYTHON_INSTALLER%" >> "%LOG_FILE%" 2>&1
    )
) else (
    echo Python 3.12 is already installed. >> "%LOG_FILE%"
)

echo Verifying Python installation... >> "%LOG_FILE%"
"%INSTALLED_PYTHON_PATH%\python.exe" --version >> "%LOG_FILE%" 2>&1
if %ERRORLEVEL% neq 0 (
    echo Python verification failed. >> "%LOG_FILE%"
    exit /b 1
)

echo Installing required packages... >> "%LOG_FILE%"
"%INSTALLED_PYTHON_PATH%\python.exe" -m pip install --upgrade pip setuptools >> "%LOG_FILE%" 2>&1
"%INSTALLED_PYTHON_PATH%\python.exe" -m pip install openai==1.54 requests==2.31 gradio==5.1.0 >> "%LOG_FILE%" 2>&1

if %ERRORLEVEL% neq 0 (
    echo Failed to install required Python packages. >> "%LOG_FILE%"
    exit /b 1
)

echo Python and required packages installed successfully. >> "%LOG_FILE%"
echo Timestamp: %DATE% %TIME% >> "%LOG_FILE%"

endlocal