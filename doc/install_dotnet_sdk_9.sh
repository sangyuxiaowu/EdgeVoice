#!/bin/bash

# 定义变量
DOTNET_VERSION="9.0.200"
INSTALL_DIR="/opt/dotnet"
DOTNET_ROOT="$INSTALL_DIR"
DOTNET_BIN="$INSTALL_DIR"
PROFILE_FILE="/etc/profile.d/dotnet.sh"

ARCHITECTURE=$(uname -m)

case $ARCHITECTURE in
    x86_64)
        RUNTIME="x64"
        ;;
    aarch64)
        RUNTIME="arm64"
        ;;
    arm*)
        RUNTIME="arm"
        ;;
    *)
        echo "不支持的架构: $ARCHITECTURE"
        exit 1
        ;;
esac

DOTNET_TAR_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/$DOTNET_VERSION/dotnet-sdk-$DOTNET_VERSION-linux-$RUNTIME.tar.gz"


# 检查是否已经安装了 .NET SDK 9.0.200
if [ -d "$INSTALL_DIR" ] && [ -x "$INSTALL_DIR/dotnet" ]; then
    INSTALLED_VERSION=$("$INSTALL_DIR/dotnet" --version)
    if [ "$INSTALLED_VERSION" == "$DOTNET_VERSION" ]; then
        echo ".NET SDK $DOTNET_VERSION 已经安装。"
        exit 0
    fi
fi

# 下载并解压 .NET SDK
echo "正在下载 .NET SDK $DOTNET_VERSION..."
wget -q $DOTNET_TAR_URL -O dotnet-sdk-$DOTNET_VERSION.tar.gz

if [ $? -ne 0 ]; then
    echo "下载 .NET SDK 失败。"
    exit 1
fi

echo "正在解压 .NET SDK..."
sudo mkdir -p $INSTALL_DIR
sudo tar -zxf dotnet-sdk-$DOTNET_VERSION.tar.gz -C $INSTALL_DIR

if [ $? -ne 0 ]; then
    echo "解压 .NET SDK 失败。"
    exit 1
fi

# 配置环境变量
echo "正在配置环境变量..."
sudo bash -c "cat > $PROFILE_FILE << EOL
export DOTNET_ROOT=$DOTNET_ROOT
export PATH=\$PATH:$DOTNET_BIN
EOL"

# 确保环境变量立即生效
source $PROFILE_FILE

# 验证安装
echo "验证 .NET SDK 安装..."
INSTALLED_VERSION=$("$INSTALL_DIR/dotnet" --version)
if [ "$INSTALLED_VERSION" == "$DOTNET_VERSION" ]; then
    echo ".NET SDK $DOTNET_VERSION 安装成功。"
else
    echo ".NET SDK 安装失败。"
    exit 1
fi

# 清理临时文件
rm dotnet-sdk-$DOTNET_VERSION.tar.gz

echo "所有操作完成。"
