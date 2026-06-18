#!/usr/bin/env bash
# Regenerate every icon PNG (companion + dock) from the canonical brand/icon.svg.
#
# Requires: rsvg-convert (apt: librsvg2-bin) and convert (ImageMagick).
# ImageMagick's built-in SVG renderer drops gradients, so we render with librsvg and
# only use convert to compose the non-square tiles.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
svg="$root/brand/icon.svg"
screen="$root/brand/screen.png"
dock="$root/dock/VsCodeProjectsDockExtension/VsCodeProjectsDockExtension/Assets"

# --- companion: VS Code Marketplace icon (128x128) ---
rsvg-convert -w 128 -h 128 "$svg" -o "$root/companion/images/icon.png"

# --- dock: square MSIX assets (fill the frame) ---
rsvg-convert -w 50  -h 50  "$svg" -o "$dock/StoreLogo.png"
rsvg-convert -w 88  -h 88  "$svg" -o "$dock/Square44x44Logo.scale-200.png"
rsvg-convert -w 24  -h 24  "$svg" -o "$dock/Square44x44Logo.targetsize-24_altform-unplated.png"
rsvg-convert -w 300 -h 300 "$svg" -o "$dock/Square150x150Logo.scale-200.png"
rsvg-convert -w 48  -h 48  "$svg" -o "$dock/LockScreenLogo.scale-200.png"

# --- dock: wide tile + splash (screenshot background + centered logo) ---
# screen.png has the dock band along the top, so a centered logo sits over the
# wallpaper, clear of the band. Background is scaled to fill (^) then centre-cropped;
# the band stays because only the sides are trimmed.
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

rsvg-convert -w 150 -h 150 "$svg" -o "$tmp/logo-wide.png"
convert "$screen" -resize "620x300^" -gravity center -extent 620x300 \
  "$tmp/logo-wide.png" -gravity center -composite -strip \
  "$dock/Wide310x150Logo.scale-200.png"

rsvg-convert -w 300 -h 300 "$svg" -o "$tmp/logo-splash.png"
convert "$screen" -resize "1240x600^" -gravity center -extent 1240x600 \
  "$tmp/logo-splash.png" -gravity center -composite -strip \
  "$dock/SplashScreen.scale-200.png"

echo "Regenerated companion + dock icons from brand/icon.svg"
