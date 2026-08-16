# Buckettie Configuration

[English](CONFIG.md) | [日本語](CONFIG.ja.md)

## Configuration File

The default file is `..\config\buckettie.json` relative to the executable. Select another file with `--config <path>`. The file is strict UTF-8 JSON: property names are case-sensitive `snake_case`, and unknown properties and comments are rejected.

See [`buckettie.example.json`](buckettie.example.json) for a complete example.

## Settings

| Setting | Required | Type | Default | Constraints and meaning |
| :--- | :--- | :--- | :--- | :--- |
| `mcp_port` | No | integer | `45450` | `1` through `65535`; loopback MCP port. |
| `mcp_path` | No | string | `/mcp` | Starts with `/`, at most 128 characters; no control character, `?`, or `#`. |
| `atlassian_email` | Yes | string | None | Valid email address for Bitbucket REST authentication; not a secret. |
| `bitbucket_username` | Yes | string | None | Case-sensitive Bitbucket Cloud username for Git HTTPS authentication. |
| `repositories` | Yes | object | None | Dictionary with one or more Repository IDs as keys and repository settings as values. |
| `repositories.<id>.workspace` | Yes | string | None | Bitbucket workspace slug. |
| `repositories.<id>.slug` | Yes | string | None | Repository slug at the end of the Bitbucket repository URL. |
| `repositories.<id>.local_root` | Yes | string | None | Absolute path of an existing allowed local Git repository. |
| `repositories.<id>.remote` | Yes | string | None | Git remote used for validation and communication; normally `origin`. |
| `repositories.<id>.develop_branch` | Yes | string | None | Development branch name. |
| `repositories.<id>.main_branch` | Yes | string | None | Main branch name. |
| `repositories.<id>.direct_push_branches` | Yes | string array | None | Exact branch names allowed for direct push. |
| `repositories.<id>.pull_branches` | Yes | string array | None | Exact branch names allowed for pull. |
| `repositories.<id>.protected_branches` | Yes | string array | None | Branches on which direct push is denied. |
| `repositories.<id>.tag_target_branch` | Yes | string | None | Branch on which tag creation is allowed. |
| `repositories.<id>.tag_pattern` | Yes | string | None | Valid .NET regular expression for allowed tag names. |
| `repositories.<id>.require_clean_working_tree` | No | boolean | `true` | Whether applicable operations require a clean working tree. |

Repository IDs are case-sensitive and unique. They use only ASCII letters, numbers, `.`, `_`, and `-`, with a maximum length of 128. `protected_branches` takes precedence over `direct_push_branches`.

## Loading and Secrets

There is no override hierarchy. The CLI and service load one file selected by the default path or `--config`. Never store API tokens in this file.

Tokens are DPAPI LocalMachine-encrypted files below `..\data\secrets` relative to the binary directory. Run `buckettie auth set <repository-id>` from an elevated terminal and never edit an encrypted file manually.

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
