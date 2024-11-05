#!/bin/bash

set -e

if [ -z "$1" ]; then
    echo "<ip_mac.sh> No installation directory specified."
    exit 1
fi

PYTHON_INSTALL_DIR="$1"

if [ ! -d "$PYTHON_INSTALL_DIR" ]; then
    echo "<ip_mac.sh> Python installation not found at $PYTHON_INSTALL_DIR. Proceeding with installation."
else
    echo "<ip_mac.sh> Python installation already exists at $PYTHON_INSTALL_DIR."
    exit 0
fi

echo "<ip_mac.sh> Downloading and installing Python 3.12..."

# download Python 3.12
PYTHON_PKG_URL="https://www.python.org/ftp/python/3.12.0/python-3.12.0-macosx10.9.pkg"
curl -o python.pkg "$PYTHON_PKG_URL"

pkgutil --expand-full python.pkg python_expanded
mkdir -p "$PYTHON_INSTALL_DIR"
cp -R "python_expanded/Python_Framework.pkg/Payload/Library/Frameworks/Python.framework/Versions/3.12" "$PYTHON_INSTALL_DIR/"
rm -rf python.pkg python_expanded

export PATH="$PYTHON_INSTALL_DIR/3.12/bin:$PATH"

echo "<ip_mac.sh> Installing required packages..."
"$PYTHON_INSTALL_DIR/3.12/bin/python3.12" -m ensurepip
"$PYTHON_INSTALL_DIR/3.12/bin/python3.12" -m pip install --upgrade pip
"$PYTHON_INSTALL_DIR/3.12/bin/python3.12" -m pip install openai==1.54 gradio==5.4 requests==2.31

echo "<ip_mac.sh> Python environment installed successfully."