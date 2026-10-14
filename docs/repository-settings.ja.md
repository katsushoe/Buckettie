# Repository登録設定

`bitbucket_repository_settings_get(repository)` はBuckettieのローカル登録情報から非秘密設定を読み取ります。Git設定・Git状態を参照せず、Bitbucketにも接続しません。`commit_author_name` と `commit_author_email` は登録作者の値です。`null` は登録作者が未設定であることを示します。ローカルGit設定から解決した有効作者は返しません。

`bitbucket_repository_settings_update(repository, changes)` は空でないオブジェクトを受け取り、指定された設定だけを部分更新します。

| フィールド | 値 | 更新時の扱い |
| --- | --- | --- |
| `direct_push_branches` | 文字列配列 | 指定時に置換。空配列も明示値として既存ポリシー検証を行います。 |
| `protected_branches` | 文字列配列 | 同上。 |
| `pull_branches` | 文字列配列 | 同上。 |
| `tag_pattern` | 空でない文字列 | 指定時に置換します。 |
| `tag_target_branch` | 空でない文字列 | 指定時に置換します。 |
| `require_clean_working_tree` | 真偽値 | 指定時に置換し、明示的な `false` を保持します。 |
| `commit_author_name` | 空でない文字列 | メールと同時に指定します。 |
| `commit_author_email` | 有効なメールアドレス | 名前と同時に指定します。 |

省略項目は更新時点の最新登録値を保持します。明示的な `null`、未知フィールド、不正な型、重複プロパティ、空の `changes`、不完全または不正な作者ペアは承認要求前に拒否します。このToolでは作者を解除できません。変更対象外の識別情報、Remote、Moyai Binding、資格情報、履歴書換ポリシーは保持されます。

対応能力は `bitbucket_provider_capabilities.data.operations` の `repository_settings_read` と `repository_settings_update` で表明します。Assertion scopeはそれぞれ `repository.read` と `repository.settings.write` です。Moyai連携では登録ProjectとのBindingを検証します。更新は既存の対話承認・監査付き永続化経路を使い、承認後かつ保存前にAssertionを再検証します。承認画面での拒否は `repository_settings_approval_cancelled`、保存失敗は `repository_settings_write_failed` です。成功応答は保存後の `settings`、実際の `changed_fields`、`approved: true` を返します。拒否・失敗は成功扱いにしません。

API Token、パスワード、秘密鍵、暗号化資格情報、解決済みのローカルGit作者値は応答へ含めません。
