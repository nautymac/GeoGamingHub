#!/bin/sh
# Adds a "GeoGamingHub (Linux)" launcher to the app menu and the desktop.
set -eu
here=$(cd "$(dirname "$0")" && pwd)
script="$here/geogaminghub-linux.py"
icon="$here/GeoGamingHub_icon.png"
[ -f "$icon" ] || icon="$here/../GeoGamingHub_icon.png"
[ -f "$icon" ] || icon=applications-games
chmod +x "$script"
entry="[Desktop Entry]
Type=Application
Name=GeoGamingHub (Linux)
Comment=Install geo-11 3D into Steam games
Exec=python3 \"$script\"
Icon=$icon
Terminal=true
Categories=Game;Utility;"
apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
mkdir -p "$apps"
printf '%s\n' "$entry" > "$apps/geogaminghub-linux.desktop"
desktop=$(xdg-user-dir DESKTOP 2>/dev/null || echo "$HOME/Desktop")
if [ -d "$desktop" ]; then
    printf '%s\n' "$entry" > "$desktop/geogaminghub-linux.desktop"
    chmod +x "$desktop/geogaminghub-linux.desktop"
fi
echo "Added GeoGamingHub (Linux) to the app menu${desktop:+ and $desktop}."
