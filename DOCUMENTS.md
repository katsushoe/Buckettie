# DOCUMENTS.md Version

2026.08.16

この文書はBuckettieのディレクトリ構成とドキュメント正本配置を管理する。

## 配置方針

公開可能な利用者・開発者向け文書はGit管理し、実環境情報を含み得る進捗・計画・実機試験資料は`.local/`でGit管理外とする。生成バイナリはリポジトリ外の設定済みリリース先へ配置する。

## プロジェクト内ディレクトリ構成

| パス | Git管理 | 用途 |
| :--- | :--- | :--- |
| `.` | Yes | 入口文書、設定サンプル、Solutionを置く。 |
| `src/` | Yes | 製品ソースコードを置く。 |
| `tests/` | Yes | 自動テストを置く。 |
| `docs/adr/` | Yes | 設計判断の正本を置く。 |
| `.local/` | No | 進捗、計画、実機試験結果など内部資料を置く。 |
| `.local/progress/` | No | 進捗グラフを置く。 |

## プロジェクト内ドキュメント一覧

| 文書名 | 正本パス | Git管理 | 用途 |
| :--- | :--- | :--- | :--- |
| `README.md` | `README.md` | Yes | 製品概要、Quick Start、文書入口。 |
| `INSTALLATION.md` | `INSTALLATION.md` | Yes | 配置、設定、Token登録、Service導入手順。 |
| `OPERATIONS.md` | `OPERATIONS.md` | Yes | 日常運用、Token更新、Upgrade手順。 |
| `TROUBLESHOOTING.md` | `TROUBLESHOOTING.md` | Yes | 障害切り分けと復旧手順。 |
| `PACKAGES.md` | `PACKAGES.md` | Yes | Release Package構成と除外対象。 |
| `RELEASE.md` | `RELEASE.md` | Yes | Version 1の内容、Artifact、Release手順。 |
| `DOCUMENTS.md` | `DOCUMENTS.md` | Yes | 文書正本配置一覧。 |
| `CONFIG.md` | `CONFIG.md` | Yes | JSON設定仕様。 |
| `SECURITY.md` | `SECURITY.md` | Yes | セキュリティ境界と秘密情報管理。 |
| `COMMANDS.md` | `COMMANDS.md` | Yes | Management CLIのCommandと終了コード。 |
| ADR | `docs/adr/` | Yes | 設計判断と代替案。 |
| `PROGRESS.md` | `.local/PROGRESS.md` | No | 機能別進捗率、完了内容、残作業の正本。 |
| `progress-chart.svg` | `.local/progress/progress-chart.svg` | No | 最新進捗率の可視化。 |

`.local/`はGit管理外の内部正本配置であり、Token、パスワード、Authorization Headerなどの秘密値は保存しない。
