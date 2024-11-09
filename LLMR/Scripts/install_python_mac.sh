#!/bin/bash

set -e

if [ -z "$1" ]; then
    echo "<ip_mac.sh> No installation directory specified."
    exit 1
fi

PYTHON_INSTALL_DIR="$1"

if [ -d "$PYTHON_INSTALL_DIR" ]; then
    echo "<ip_mac.sh> Python installation already exists at $PYTHON_INSTALL_DIR."
    exit 0
else
    echo "<ip_mac.sh> Creating installation directory at $PYTHON_INSTALL_DIR."
    mkdir -p "$PYTHON_INSTALL_DIR"
fi

echo "<ip_mac.sh> Checking for Homebrew..."

if ! command -v brew &>/dev/null; then
    echo "<ip_mac.sh> Homebrew not found. Installing Homebrew..."
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
    if [[ -f ~/.bash_profile ]]; then
        echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.bash_profile
        eval "$(/opt/homebrew/bin/brew shellenv)"
    elif [[ -f ~/.zshrc ]]; then
        echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.zshrc
        eval "$(/opt/homebrew/bin/brew shellenv)"
    else
        echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.profile
        eval "$(/opt/homebrew/bin/brew shellenv)"
    fi
fi

echo "<ip_mac.sh> Installing dependencies..."

brew update
brew install openssl readline sqlite3 xz zlib tcl-tk

echo "<ip_mac.sh> Downloading Python 3.12.0 source code..."

PYTHON_VERSION="3.12.0"
PYTHON_TGZ="Python-$PYTHON_VERSION.tgz"
PYTHON_SRC_DIR="Python-$PYTHON_VERSION"

curl -L -o "$PYTHON_TGZ" "https://www.python.org/ftp/python/$PYTHON_VERSION/$PYTHON_TGZ"

echo "<ip_mac.sh> Extracting Python $PYTHON_VERSION source code..."

tar -xzf "$PYTHON_TGZ"

cd "$PYTHON_SRC_DIR"

echo "<ip_mac.sh> Configuring Python $PYTHON_VERSION build..."

./configure --prefix="$PYTHON_INSTALL_DIR" \
    CPPFLAGS="-I$(brew --prefix openssl)/include -I$(brew --prefix readline)/include -I$(brew --prefix sqlite3)/include -I$(brew --prefix zlib)/include -I$(brew --prefix tcl-tk)/include" \
    LDFLAGS="-L$(brew --prefix openssl)/lib -L$(brew --prefix readline)/lib -L$(brew --prefix sqlite3)/lib -L$(brew --prefix zlib)/lib -L$(brew --prefix tcl-tk)/lib" \
    --enable-optimizations \
    --enable-shared \
    --with-openssl="$(brew --prefix openssl)" \
    --with-tcltk-includes="-I$(brew --prefix tcl-tk)/include" \
    --with-tcltk-libs="-L$(brew --prefix tcl-tk)/lib" \
    MACOSX_DEPLOYMENT_TARGET=$(sw_vers -productVersion)

echo "<ip_mac.sh> Building Python $PYTHON_VERSION..."

make -j$(sysctl -n hw.ncpu)

echo "<ip_mac.sh> Installing Python $PYTHON_VERSION..."

make install

cd ..

rm -rf "$PYTHON_SRC_DIR" "$PYTHON_TGZ"

echo "<ip_mac.sh> Fixing library paths..."

install_name_tool -id "@rpath/libpython3.12.dylib" "$PYTHON_INSTALL_DIR/lib/libpython3.12.dylib"
install_name_tool -add_rpath "@loader_path/../lib" "$PYTHON_INSTALL_DIR/bin/python3.12"

echo "<ip_mac.sh> Installing required Python packages..."

"$PYTHON_INSTALL_DIR/bin/python3.12" -m ensurepip
"$PYTHON_INSTALL_DIR/bin/python3.12" -m pip install --upgrade pip setuptools
"$PYTHON_INSTALL_DIR/bin/python3.12" -m pip install openai==1.54 requests==2.31 gradio==5.1.0

echo "<ip_mac.sh> Python environment installed successfully."
