#!/bin/bash

set -e

if [ -z "$1" ]; then
    >&2 echo "<ip_mac.sh> No installation directory specified."
    exit 1
fi

PYTHON_INSTALL_DIR="$1"
PYTHON_EXECUTABLE="$PYTHON_INSTALL_DIR/envs/myenv/bin/python"

if [ -x "$PYTHON_EXECUTABLE" ]; then
    echo "<ip_mac.sh> Python installation already exists at $PYTHON_INSTALL_DIR."
    exit 0
fi

echo "<ip_mac.sh> Proceeding with installation..."

echo "<ip_mac.sh> Determining system architecture..."

ARCH_NAME="$(uname -m)"
if [ "$ARCH_NAME" = "x86_64" ]; then
    MINIFORGE_INSTALLER="Miniforge3-MacOSX-x86_64.sh"
elif [ "$ARCH_NAME" = "arm64" ]; then
    MINIFORGE_INSTALLER="Miniforge3-MacOSX-arm64.sh"
else
    >&2 echo "<ip_mac.sh> Unsupported architecture: $ARCH_NAME"
    exit 1
fi

echo "<ip_mac.sh> Downloading Miniforge installer for $ARCH_NAME..."

curl -L -o "$MINIFORGE_INSTALLER" "https://github.com/conda-forge/miniforge/releases/latest/download/$MINIFORGE_INSTALLER"

echo "<ip_mac.sh> Installing Miniforge to $PYTHON_INSTALL_DIR..."

bash "$MINIFORGE_INSTALLER" -b -u -p "$PYTHON_INSTALL_DIR" || {
    >&2 echo "<ip_mac.sh> Miniforge installation failed."
    exit 1
}

echo "<ip_mac.sh> Initializing Conda environment..."

source "$PYTHON_INSTALL_DIR/bin/activate" || {
    >&2 echo "<ip_mac.sh> Failed to initialize Conda."
    exit 1
}

echo "<ip_mac.sh> Updating Conda..."

conda update -y conda || {
    >&2 echo "<ip_mac.sh> Conda update failed."
    exit 1
}

echo "<ip_mac.sh> Creating Conda environment with Python 3.12.0..."

conda create -y -n myenv python=3.12.0 || {
    >&2 echo "<ip_mac.sh> Conda environment creation failed."
    exit 1
}

echo "<ip_mac.sh> Activating the new environment..."

source "$PYTHON_INSTALL_DIR/bin/activate" myenv || {
    >&2 echo "<ip_mac.sh> Failed to activate Conda environment."
    exit 1
}

echo "<ip_mac.sh> Installing required Python packages..."

conda install -y pip || {
    >&2 echo "<ip_mac.sh> Failed to install pip."
    exit 1
}
"$PYTHON_INSTALL_DIR/envs/myenv/bin/pip" install requests==2.31 openai==1.63.2 gradio==5.16.1 || {
    >&2 echo "<ip_mac.sh> Failed to install required Python packages."
    exit 1
}

echo "<ip_mac.sh> Deactivating environment..."

conda deactivate || {
    >&2 echo "<ip_mac.sh> Failed to deactivate Conda environment."
    exit 1
}

echo "<ip_mac.sh> Cleaning up..."

rm "$MINIFORGE_INSTALLER"

echo "<ip_mac.sh> Python environment installed successfully at $PYTHON_INSTALL_DIR."
