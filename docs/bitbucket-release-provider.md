# Bitbucket Release Provider contract

Buckettie maps Moyai's Release lifecycle to the Bitbucket Cloud Downloads API because Bitbucket Cloud has no GitHub-style Release resource.

- `buckettie_release_create(repository, version, notes?)` uploads or replaces `buckettie-release-{version}.json` with `state=draft`.
- `buckettie_release_publish(repository, version, artifactPath?, notes?)` uploads or replaces the manifest with `state=published`; when supplied, the local artifact is uploaded in the same request.
- `buckettie_release_get(repository, version)` reads the manifest.
- `buckettie_release_withdraw(repository, version)` deletes the manifest. Uploaded artifacts are intentionally retained because Moyai does not pass an artifact path to withdraw.

Create and publish are idempotent replacement operations. Withdraw returns `release_not_found` when the manifest is absent. Invalid versions, notes, or artifact paths return `invalid_release`; authentication, authorization, rate-limit, network, and repository allowlist failures use the existing structured error contract.

The manifest is Buckettie's portable Release record. It contains `version`, `state`, `notes`, `artifactName`, and `updatedAt`. It is not a native Bitbucket Release object and does not create, move, or delete a Git tag.
