# Buckettie configuration

## Configuration file

Buckettie reads UTF-8 JSON through `IBuckettieOptionsLoader`. The executable host will supply the file location; the library does not contain a fixed path.

The JSON contract is strict:

- Property names use `snake_case` and are case-sensitive.
- Unknown properties and JSON comments are rejected.
- Repository IDs are case-sensitive and must be unique.
- Repository IDs use only ASCII letters, numbers, `.`, `_`, and `-`, with a maximum length of 128.
- Each repository requires every field shown in `buckettie.example.json` except `require_clean_working_tree`.
- `require_clean_working_tree` defaults to `true`.
- `tag_pattern` must be a valid .NET regular expression.
- API tokens and other secrets must not be stored in this file.

## Validation errors

| Code | Meaning |
| --- | --- |
| `InvalidJson` | JSON syntax or the strict JSON contract is invalid. |
| `DuplicateRepositoryId` | The `repositories` object contains the same ID more than once. |
| `InvalidRepositoryId` | A Repository ID contains unsupported characters or is too long. |
| `RequiredValueMissing` | A required property is absent, null, empty, or whitespace. |
| `InvalidTagPattern` | `tag_pattern` is not a valid regular expression. |

Filesystem existence, `.git`, symlink/junction, and Git Remote checks are separate repository-boundary validations performed after loading.
