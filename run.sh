#!/bin/bash
# Run SymatoIME from WSL2

set -e

EXE_PATH="bin/Release/net8.0-windows/win-x64/publish/SymatoIME.exe"

# Function to kill existing process
kill_existing() {
    powershell.exe -Command "Stop-Process -Name 'SymatoIME' -Force -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 300" 2>/dev/null || true
}

# Function to build
build() {
    echo "Building SymatoIME..."
    dotnet publish -c Release -r win-x64 --self-contained -p:EnableWindowsTargeting=true 2>&1 | tail -5
}

# Main
case "${1:-}" in
    test|--test)
        echo "Running SymatoIME Engine Tests..."
        dotnet run -c Test -- --test
        ;;
    build|--build)
        build
        ;;
    *)
        # Check if we need to rebuild
        if [ ! -f "$EXE_PATH" ] || [ -n "$(find . -name '*.cs' -newer "$EXE_PATH" 2>/dev/null)" ]; then
            build
        fi
        
        echo "Starting SymatoIME..."
        kill_existing
        
        # Run using wsl.exe to properly launch Windows exe
        WSL_ROOT=$(wslpath -w /)
        powershell.exe -Command "Start-Process '\$WSL_ROOT\home\t\mx\symato_qoder\$EXE_PATH'" 2>/dev/null || \
            cmd.exe /c start "" "\\\\wsl.localhost\\Ubuntu\\home\\t\\mx\\symato_qoder\\$EXE_PATH" 2>/dev/null || \
            echo "Failed to start. Try manual: powershell.exe -Command \"Start-Process '\$(wslpath -w /home/t/mx/symato_qoder/$EXE_PATH)'\""
        
        sleep 1
        powershell.exe -Command "Get-Process -Name 'SymatoIME' -ErrorAction SilentlyContinue | Select-Object Name, Id, StartTime"
        ;;
esac
