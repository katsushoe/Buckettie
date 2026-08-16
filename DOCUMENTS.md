# DOCUMENTS.md Version

2026.08.17

[English](DOCUMENTS.md) | [日本語](DOCUMENTS.ja.md)

This document is the source of truth for Buckettie's directory structure and canonical document locations.

## Placement Policy

Public user and developer documents are tracked by Git. Progress, plans, and operational-test material that may contain environment details stays untracked below `.local/`. Generated binaries are placed in the configured release destination outside the repository.

## Project Directory Structure

| Path | Git tracked | Purpose |
| :--- | :--- | :--- |
| `.` | Yes | Entry documents, configuration example, and solution. |
| `src/` | Yes | Product source code. |
| `tests/` | Yes | Automated tests. |
| `docs/adr/` | Yes | Canonical architecture decisions. |
| `.local/` | No | Internal progress, plans, and operational-test results. |
| `.local/progress/` | No | Progress visualization. |

## Project Document Index

| Document | Canonical path | Git tracked | Purpose |
| :--- | :--- | :--- | :--- |
| `README.md` / `README.ja.md` | `README.md` / `README.ja.md` | Yes | Product overview, quick start, and document entry. |
| `MCP_SETUP.md` / `MCP_SETUP.ja.md` | Same | Yes | MCP server setup and client registration. |
| `CONFIG.md` / `CONFIG.ja.md` | Same | Yes | JSON configuration contract. |
| `COMMANDS.md` / `COMMANDS.ja.md` | Same | Yes | Management CLI and exit codes. |
| `INSTALLATION.md` | `INSTALLATION.md` | Yes | Deployment, token registration, and service setup. |
| `OPERATIONS.md` | `OPERATIONS.md` | Yes | Routine operation, token update, and upgrade. |
| `TROUBLESHOOTING.md` | `TROUBLESHOOTING.md` | Yes | Failure isolation and recovery. |
| `PACKAGES.md` | `PACKAGES.md` | Yes | Release package layout and exclusions. |
| `RELEASE.md` | `RELEASE.md` | Yes | Current release notes and artifacts. |
| `SECURITY.md` | `SECURITY.md` | Yes | Security boundaries and secret handling. |
| `DOCUMENTS.md` / `DOCUMENTS.ja.md` | Same | Yes | Public canonical document index. |
| ADRs | `docs/adr/` | Yes | Design decisions and alternatives. |
| `PROGRESS.md` | `.local/PROGRESS.md` | No | Canonical progress and remaining-work record. |
| `progress-chart.svg` | `.local/progress/progress-chart.svg` | No | Current progress visualization. |

`.local/` contains untracked internal sources of truth and temporary artifacts. Do not store tokens, passwords, authorization headers, or other secret values in documentation.
