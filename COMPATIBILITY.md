# URTC Windows compatibility

URTC is an **editor plug-in**, not a player/runtime package. The supported configuration is Windows 10/11 (64-bit) with Unity 6 LTS. The project currently uses Unity Editor `6000.4.1f1`; use that version for the team baseline.

New Unity 6 releases should be tested before the team upgrades. Unity Hub's own version does not affect project compatibility; the installed Unity Editor version does. The plug-in source uses Unity 6 editor APIs that remain compatible across newer Unity 6 patch releases; new major Unity versions always need a fresh import and test.

## Team setup

1. In Unity Hub, add the directory that contains `Assets`, `Packages`, and `ProjectSettings`:
   `URTC-main-Frontend/URTC-main-clean`.
2. Install Unity `6000.4.1f1` for consistent team results.
3. Run the Go backend and PostgreSQL on a reachable server.
4. In **Window > URTC Panel**, set **Server URL** to the backend URL. Use `https://` in shared/production environments; the plug-in automatically uses `wss://` for its WebSocket.
5. Log in with GitHub, then copy the session ID shown by the callback page into the panel. GitHub credentials remain only in Unity memory and must be refreshed after restarting Unity.

## Windows-only note

The bundled `git2-3f4182d.dll` is a Windows x64 native dependency. macOS and Linux need their own LibGit2Sharp native binaries before they can be supported.

## Backend deployment

Copy the backend `.env.example` to `.env`, set the PostgreSQL and GitHub OAuth values, and start the backend. For a shared server, terminate TLS at a reverse proxy and use the public `https://` URL in the Unity panel. The GitHub OAuth callback URL must be the same public URL followed by `/github/callback`.

## Safe sync behavior

URTC never force-checks out or hard-resets during Pull. If a conflict occurs, it stops and keeps local files unchanged; resolve the conflict in Git, then pull again.
