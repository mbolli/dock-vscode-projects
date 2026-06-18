# brand/

The single source of truth for the project's mark, shared by both halves so neither
extension "owns" it (same spirit as `contract/`).

- `icon.svg` — the canonical icon. Dark rounded backplate with a VS Code-blue dock band,
  echoing the real app: a white favourite star followed by project entries (name row +
  dimmer type tag).
- `render.sh` — regenerates every PNG (the companion's Marketplace icon and all of the
  dock's MSIX tile/logo assets) from `icon.svg`.

Edit `icon.svg`, then:

```bash
./brand/render.sh
```

Requires `rsvg-convert` (`apt install librsvg2-bin`) and ImageMagick `convert`.
