#!/bin/bash

set -e

BASE_DIR="$(cd "$(dirname "$0")"; pwd)"
PYTHON_INSTALL_DIR="$BASE_DIR/Python/312"

if [ ! -d "$PYTHON_INSTALL_DIR" ]; then
    echo "<ip_mac.sh> Python virtual environment not found. Proceeding with installation."
else
    echo "<ip_mac.sh> Python virtual environment already exists at $PYTHON_INSTALL_DIR."
    exit 0
fi

echo "<ip_mac.sh> Installing Python 3.12 and required packages..."

if ! command -v brew &> /dev/null; then
    echo "<ip_mac.sh> Installing Homebrew..."
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
fi

if ! brew ls --versions python@3.12 > /dev/null; then
    echo "<ip_mac.sh> Installing Python 3.12..."
    brew install python@3.12
else
    echo "<ip_mac.sh> Python 3.12 is already installed."
fi

export PATH="/opt/homebrew/opt/python@3.12/bin:$PATH"

echo "<ip_mac.sh> Creating virtual environment..."
python3.12 -m venv "$PYTHON_INSTALL_DIR"

echo "<ip_mac.sh> Activating virtual environment..."
source "$PYTHON_INSTALL_DIR/bin/activate"

echo "<ip_mac.sh> Upgrading pip..."
pip install --upgrade pip

echo "<ip_mac.sh> Installing required packages..."
pip install openai==1.54 gradio==5.4

echo "<ip_mac.sh> Python environment installed successfully."