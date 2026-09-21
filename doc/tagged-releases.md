# Tagged releases

Pushing a stable tag such as `v0.2.0` starts `.github/workflows/release.yml`.
It builds the mod in Release, runs unit/integration and E2E tests, signs the
binaries with the existing Azure signing account, and uploads
`BannerlordCoop-v0.2.0.zip` to a **draft** GitHub release. The archive contains
`Coop/`, ready to extract into Bannerlord's `Modules/` directory. The tag sets
`CoopVersion` for this build and the module manifest; no source version edit is
required. Nightly and manual builds are unchanged.

The private `Bannerlord-Coop-Team/BannerlordCoop.DedicatedServer` repository
receives `build_release`, downloads that draft asset, builds/tests the server,
uploads `BannerlordCoop-DedicatedServer-Linux64-v0.2.0.tar.zst` to the same draft,
and pushes `ghcr.io/bannerlord-coop-team/bannerlordcoop-dedicatedserver:v0.2.0`.
Only a successful completed server run sends `server_complete`.
The public repository checks both workflow runs, the tag SHA, the server's
result artifact, and both archive checksums before publishing. It then sends
`promote_stable` with the verified image digest. Per the production owner's
instruction, this final workflow **only prints the verified promotion command**.
An operator must approve and execute it separately; neither workflow pushes
`stable`. A server build failure leaves the release draft.

## Setup before the first tag

- Merge the workflows/scripts in **both repositories' default branches**.
  `repository_dispatch` and `workflow_run` receivers must exist there. The
  tagged client commit must also contain the release workflow and script.
- Add `RELEASE_DISPATCH_TOKEN` to both repositories: a fine-grained PAT with
  access to both repositories, **Contents: read/write** and **Actions: read**.
  Draft releases require authenticated access. Each repository's ordinary
  `GITHUB_TOKEN` cannot dispatch to or read the other private repository.
- Keep the existing Azure signing secrets and `SIGNING_*` variables configured.
  Allow the tag workflow's OIDC subject in Azure (the existing development
  branch credential alone does not authorize `refs/tags/v0.2.0`).
- Configure the private server workflow's build inputs and GHCR package write
  permission as described in that repository. Its CI boot test requires explicit
  terms acceptance via `RELEASE_CI_EULA_SHA256`; the pipeline does not accept
  terms on the operator's behalf. Do not grant the client workflow package write
  permission. The Linux archive must fit GitHub's 2 GiB asset limit.
- Verify that the pinned server game inputs support the client's game version.
  At implementation time the client declares `v1.4.8` while the private workflow
  references `v1.4.7` input keys. Compatibility is not established by local
  workflow tests; verify or update those inputs before the first release.
- Protect `v*` tags against unauthorized creation, deletion and replacement.
  Treat the cross-repository token as a release credential.

Creating/pushing a tag is an actual publication request, not a dry run. This
implementation does not create `v0.2.0` or change secrets automatically.

## Dispatch contract

All events use `repository_dispatch`. IDs and attempts are JSON numbers.

`build_release` fields:

```json
{
  "tag": "v0.2.0",
  "release_id": 123,
  "client_sha": "0123456789012345678901234567890123456789",
  "client_run_id": 456,
  "client_run_attempt": 1
}
```

`server_complete` and `promote_stable` add `server_run_id`,
`server_run_attempt`, and `image_digest` (`sha256:` followed by 64 lowercase
hexadecimal digits). The server build workflow path is
`.github/workflows/release.yml`; it uploads the Actions artifact `release-result`
containing `release-result.json`. That JSON contains the completion fields plus
`client_asset_sha256` and `server_asset_sha256` (64 lowercase hexadecimal digits,
without a prefix). The completion notifier runs after the build succeeds, not
inside the still-running build workflow.

The client draft description retains a `release-coordination` HTML comment
with the original request. Leave this comment intact; it prevents a callback
from an older client run/attempt from publishing a replacement draft.

## Recovery and checks

- Failed client build/sign/upload/dispatch: rerun the client release workflow.
  Only draft client assets are replaced; published releases cannot be rebuilt.
- Failed server build before upload: rerun the private server workflow. If a
  previous attempt already uploaded the server archive, deliberately remove that
  incomplete draft asset before rebuilding; server uploads do not overwrite it.
  Its completion must reference the current run attempt and result artifact.
- Failed server notification: rerun the private notification workflow, not the
  already successful server build.
- Failed publication callback: rerun **Publish release** after correcting the
  cause. Verification runs again. If publication succeeded but promotion
  dispatch failed, it only resends the promotion after verifying the assets.
- Failed promotion-command verification: rerun the private promotion workflow.
  It prints the command only, so a successful run does not mean `stable` changed.
- Never move a release tag to retry a build. Use a new version for changed code.

Local targeted checks (no game deployment or GitHub mutations):

```sh
node --test .github/scripts/tag-release.test.cjs
```

The first end-to-end release needs CI validation with the configured credentials.
Check that the draft remains unpublished while the server is building, both
assets exist and their GitHub `digest` fields match the result checksums, and
the final command names the verified image digest. Confirm `stable` remains
unchanged until an operator explicitly runs that command.
