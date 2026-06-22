# Privacy Policy

**VS Code Projects Dock** (the PowerToys Command Palette extension) and its
**Companion** VS Code extension are designed to run entirely on your own machine.
They collect no personal data, contain no telemetry or analytics, and make no
network connections.

## What the software does with data

The two halves communicate through small local state files only:

- **The companion** (running inside VS Code) writes one JSON file per open window to
  a directory under your user profile (by default
  `%LOCALAPPDATA%\VsCodeProjectsDock\windows`). Each file contains only:
  the folder's URI, its display name, the remote kind (local / WSL / container / SSH),
  the window and process id, and a "last seen" timestamp.
- **The dock** (running inside the PowerToys Command Palette) reads those files to draw
  the project band, and writes your pinned projects to `pinned.json` in the same
  profile directory. It launches or focuses VS Code windows on click.

That is the full extent of the data handled. None of it identifies you personally, and
none of it is transmitted anywhere.

## What we collect

Nothing. The publisher (zwei und eins gmbh) receives no data of any kind from this
software. There are no servers, no accounts, no cookies, no tracking, and no third-party
services involved.

## Where the data lives and how long

All state files stay on your local disk. The dock automatically removes a window's state
file once that window has closed. You can delete the entire
`%LOCALAPPDATA%\VsCodeProjectsDock` directory at any time to clear everything; the
extensions recreate only what they need.

## Permissions

The dock is packaged as a full-trust Command Palette extension (the standard
out-of-process COM activation model that the Command Palette requires). This trust level
is used solely to read and write the local state files described above. The software does
not access the network, your other files, the clipboard, or any device hardware.

## Contact

Questions about this policy: michael@zweiundeins.gmbh

_Last updated: 2026-06-22._
