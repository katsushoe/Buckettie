# Repository settings

`bitbucket_repository_settings_get(repository)` reads the registered non-secret settings from Buckettie's
local repository registry. It does not inspect Git configuration or state and does not contact Bitbucket.
`commit_author_name` and `commit_author_email` are the registered author values; `null` means no registered
author. The effective author resolved from local Git configuration is not returned by this operation.

`bitbucket_repository_settings_update(repository, changes)` accepts a non-empty object with these fields:

| Field | Value | Update behavior |
| --- | --- | --- |
| `direct_push_branches` | string array | Replace when supplied; an empty array is explicit and undergoes the existing policy validation. |
| `protected_branches` | string array | Same. |
| `pull_branches` | string array | Same. |
| `tag_pattern` | non-empty string | Replace when supplied. |
| `tag_target_branch` | non-empty string | Replace when supplied. |
| `require_clean_working_tree` | boolean | Replace when supplied; explicit `false` is preserved. |
| `commit_author_name` | non-empty string | Must be supplied together with email. |
| `commit_author_email` | valid email string | Must be supplied together with name. |

Omitted fields keep their latest registered values. Explicit `null`, unknown fields, invalid types, duplicate
properties, an empty `changes` object, or an incomplete/invalid author pair are rejected before approval.
Author removal is not supported by this operation. The request changes only the listed settings; identity,
remote, Moyai binding, credentials, and history-rewrite policy are retained.

Both operations are listed under `repository_settings_read` and `repository_settings_update` in
`bitbucket_provider_capabilities.data.operations`. Their assertion scopes are `repository.read` and
`repository.settings.write`, respectively. Integration assertions must match the registered Project binding.
Updates use the existing interactive approval and audited persistence path. A declined prompt returns
`repository_settings_approval_cancelled`; a failed write returns `repository_settings_write_failed`.
After approval, Buckettie revalidates the assertion before writing. The update response contains the persisted
`settings`, actual `changed_fields`, and `approved: true`; denied or failed updates do not report success.

The returned object never includes API tokens, passwords, private keys, encrypted credentials, or resolved
local Git author values.
