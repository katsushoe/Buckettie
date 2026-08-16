# DOCUMENTS.ja.md Version

2026.08.17

[English](DOCUMENTS.md) | [日本語](DOCUMENTS.ja.md)

この文書はBuckettieのディレクトリ構成とドキュメント正本配置を管理します。

## 配置方針

公開可能な利用者・開発者向け文書はGit管理し、実環境情報を含み得る進捗・計画・実機試験資料は`.local/`でGit管理外とします。生成バイナリはリポジトリ外の設定済みリリース先へ配置します。

## プロジェクト内ディレクトリ構成

| パス | Git管理 | 用途 |
| :--- | :--- | :--- |
| `.` | Yes | 入口文書、設定サンプル、Solutionを置きます。 |
| `src/` | Yes | 製品ソースコードを置きます。 |
| `tests/` | Yes | 自動テストを置きます。 |
| `docs/adr/` | Yes | 設計判断の正本を置きます。 |
| `.local/` | No | 進捗、計画、実機試験結果などの内部資料を置きます。 |
| `.local/progress/` | No | 進捗グラフを置きます。 |

## プロジェクト内ドキュメント一覧

| 文書名 | 正本パス | Git管理 | 用途 |
| :--- | :--- | :--- | :--- |
| `README.md` / `README.ja.md` | 同左 | Yes | 製品概要、Quick Start、文書入口。 |
| `MCP_SETUP.md` / `MCP_SETUP.ja.md` | 同左 | Yes | MCP Server導入とClient登録。 |
| `CONFIG.md` / `CONFIG.ja.md` | 同左 | Yes | JSON設定仕様。 |
| `COMMANDS.md` / `COMMANDS.ja.md` | 同左 | Yes | Management CLIと終了Code。 |
| `INSTALLATION.md` | `INSTALLATION.md` | Yes | 配置、Token登録、Service導入手順。 |
| `OPERATIONS.md` | `OPERATIONS.md` | Yes | 日常運用、Token更新、Upgrade手順。 |
| `TROUBLESHOOTING.md` | `TROUBLESHOOTING.md` | Yes | 障害切り分けと復旧手順。 |
| `PACKAGES.md` | `PACKAGES.md` | Yes | Release Package構成と除外対象。 |
| `RELEASE.md` | `RELEASE.md` | Yes | 現行Release NotesとArtifact。 |
| `SECURITY.md` | `SECURITY.md` | Yes | セキュリティ境界と秘密情報管理。 |
| `DOCUMENTS.md` / `DOCUMENTS.ja.md` | 同左 | Yes | 公開文書の正本配置一覧。 |
| ADR | `docs/adr/` | Yes | 設計判断と代替案。 |
| `PROGRESS.md` | `.local/PROGRESS.md` | No | 進捗と残作業の正本。 |
| `progress-chart.svg` | `.local/progress/progress-chart.svg` | No | 最新進捗率の可視化。 |

`.local/`にはGit管理外の内部正本と一時成果物を置きます。ドキュメントへToken、パスワード、Authorization Headerなどの秘密値を保存しません。
