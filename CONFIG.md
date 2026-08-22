# Buckettie Configuration

[English](CONFIG.md) | [日本語](CONFIG.ja.md)

## Configuration File

The default file is `..\config\buckettie.json` relative to the executable. Select another file with `--config <path>`. The file is strict UTF-8 JSON: property names are case-sensitive `snake_case`, and unknown properties and comments are rejected. `language` controls interactive UI text: use `ja-JP`, `en-US`, or `auto` to follow the Windows UI language. The MSI writes the language selected during setup.

See [`buckettie.example.json`](buckettie.example.json) for a complete example.

## Settings

| Setting | Required | Type | Default | Constraints and meaning |
| :--- | :--- | :--- | :--- | :--- |
| `mcp_port` | No | integer | `45450` | `1` through `65535`; loopback MCP port. |
| `mcp_path` | No | string | `/mcp` | Starts with `/`, at most 128 characters; no control character, `?`, or `#`. |
| `atlassian_email` | Yes | string | None | Valid email address for Bitbucket REST authentication; not a secret. |
| `bitbucket_username` | Yes | string | None | Case-sensitive Bitbucket Cloud username for Git HTTPS authentication. |
| `repositories` | Yes | object | None | Legacy field, kept for backward compatibility. See [Repository Storage](#repository-storage) below — on a running install this is always `{}` and repository records live in SQLite instead. |

Repository IDs are case-sensitive and unique. They use only ASCII letters, numbers, `.`, `_`, and `-`, with a maximum length of 128. `protected_branches` takes precedence over `direct_push_branches`.

## Repository Storage

Repository records (`workspace`, `slug`, `local_root`, `remote`, `develop_branch`, `main_branch`, `direct_push_branches`, `pull_branches`, `protected_branches`, `tag_target_branch`, `tag_pattern`, `require_clean_working_tree`) are stored in a SQLite database at `..\data\repositories.db` relative to the binary directory, not in `buckettie.json`.

On first startup after upgrading from a version that stored repositories in `buckettie.json`, the service migrates every entry under the old `repositories` key into the database once, then rewrites `buckettie.json` with `repositories` set to `{}`. From then on the database is the sole source of truth; `repositories` in `buckettie.json` is not read again (it stays in the JSON schema only so an already-migrated file still validates).

## Loading and Secrets

There is no override hierarchy. The CLI and service load one file selected by the default path or `--config`. Never store API tokens in this file.

Tokens are DPAPI LocalMachine-encrypted files below `..\data\secrets` relative to the binary directory. Run `buckettie auth set <repository-id>` from an elevated terminal and never edit an encrypted file manually.

## Repository Registration, Update, and Unregistration

The `bitbucket_repository_register` MCP tool (or `buckettie repo register`) adds one repository without the manual stop/edit/restart flow below. It accepts only `repository` (the new Repository ID), `local_root`, and optionally `remote`, `develop_branch`, and `main_branch`. `workspace` and `slug` are always derived from the local repository's actual Git remote; the caller cannot supply them. `direct_push_branches`, `pull_branches`, `protected_branches`, `tag_target_branch`, `tag_pattern`, and `require_clean_working_tree` are entirely server-defaulted from the supplied branch names, matching the same conservative shape as the example above.

The selected Git remote must use `https://bitbucket.org/<workspace>/<repository>.git`. SSH forms such as `git@bitbucket.org:...` and `ssh://...` are rejected with `ssh_remote_not_supported`. For an existing SSH clone, run `git remote set-url origin https://bitbucket.org/<workspace>/<repository>.git` before registration or further Buckettie Git operations.

The `bitbucket_repository_update` MCP tool (or `buckettie repo update`) changes an already-registered repository's `direct_push_branches`, `pull_branches`, `protected_branches`, `tag_target_branch`, `tag_pattern`, and `require_clean_working_tree`. It cannot change `workspace`, `slug`, `local_root`, `remote`, `develop_branch`, or `main_branch` — those are fixed at registration time and re-validated against the Git remote, so changing what repository an ID points to means unregistering and registering again.

Both `register` and `update` require a human to approve a native Dialog on the server machine's interactive desktop session; it cannot be approved from the calling MCP client. See [SECURITY.md](SECURITY.md#repository-registration-approval) for the trust boundary and [ADR 0012](docs/adr/0012-interactive-repository-registration-approval.md) / [ADR 0013](docs/adr/0013-repository-store-and-live-lifecycle.md) for the design.

The `bitbucket_repository_unregister` MCP tool (or `buckettie repo unregister`) removes a repository immediately, with no Dialog — since it only revokes push/PR/tag rights, there is no privilege for a compromised or over-eager caller to gain by calling it.

None of these three operations need `stop`/`restart`. A registration or update that needs a different shape than these tools support still uses the manual edit flow (now against the SQLite database — see [Repository Storage](#repository-storage)).

## Validation Errors

| Code | Meaning |
| :--- | :--- |
| `InvalidJson` | JSON syntax or the strict contract is invalid. |
| `InvalidAtlassianEmail` | `atlassian_email` is not one valid email address. |
| `InvalidBitbucketUsername` | `bitbucket_username` is not a valid username. |
| `DuplicateRepositoryId` | `repositories` contains the same ID more than once. |
| `InvalidRepositoryId` | A Repository ID has unsupported characters or length. |
| `RequiredValueMissing` | A required value is absent, null, empty, or whitespace. |
| `InvalidTagPattern` | `tag_pattern` is not a valid regular expression. |
| `InvalidMcpPort` | `mcp_port` is outside its valid range. |
| `InvalidMcpPath` | `mcp_path` is not a safe absolute HTTP path. |

Filesystem existence, `.git`, symlink or junction, and Git remote checks are separate repository-boundary validations performed after loading.
