#!/usr/bin/env bash
# Regenerate every icon PNG (companion + dock) from the canonical brand/icon.svg.
#
# Requires: rsvg-convert (apt: librsvg2-bin) and convert (ImageMagick).
# ImageMagick's built-in SVG renderer drops gradients, so we render with librsvg and
# only use convert to center the non-square tiles.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
svg="$root/brand/icon.svg"
dock="$root/dock/VsCodeProjectsDockExtension/VsCodeProjectsDockExtension/Assets"

# --- companion: VS Code Marketplace icon (128x128) ---
rsvg-convert -w 128 -h 128 "$svg" -o "$root/companion/images/icon.png"

# --- dock: square MSIX assets (fill the frame) ---
rsvg-convert -w 50  -h 50  "$svg" -o "$dock/StoreLogo.png"
rsvg-convert -w 88  -h 88  "$svg" -o "$dock/Square44x44Logo.scale-200.png"
rsvg-convert -w 24  -h 24  "$svg" -o "$dock/Square44x44Logo.targetsize-24_altform-unplated.png"
rsvg-convert -w 300 -h 300 "$svg" -o "$dock/Square150x150Logo.scale-200.png"
rsvg-convert -w 48  -h 48  "$svg" -o "$dock/LockScreenLogo.scale-200.png"

# --- dock: wide tile + splash (icon centered on transparent) ---
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
rsvg-convert -w 220 -h 220 "$svg" -o "$tmp/wide.png"
convert "$tmp/wide.png" -background none -gravity center -extent 620x300  -strip "$dock/Wide310x150Logo.scale-200.png"
rsvg-convert -w 360 -h 360 "$svg" -o "$tmp/splash.png"
convert "$tmp/splash.png" -background none -gravity center -extent 1240x600 -strip "$dock/SplashScreen.scale-200.png"

echo "Regenerated companion + dock icons from brand/icon.svg"
