# brand/

The single source of truth for the project's mark, shared by both halves so neither
extension "owns" it (same spirit as `contract/`).

- `icon.svg` — the canonical icon. Dark rounded backplate; the dock-band motif of two
  grey pinned projects flanking one blue "open" project with a liveness dot.
- `render.sh` — regenerates every PNG (the companion's Marketplace icon and all of the
  dock's MSIX tile/logo assets) from `icon.svg`.

Edit `icon.svg`, then:

```bash
./brand/render.sh
```

Requires `rsvg-convert` (`apt install librsvg2-bin`) and ImageMagick `convert`.
