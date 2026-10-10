# Tagged releases

Pushing a stable tag such as `v0.2.0` starts `.github/workflows/release.yml`.
It builds the mod in Release, runs unit/integration and E2E tests, signs the
binaries with the existing Azure signing account, and uploads
`BannerlordCoop-v0.2.0.zip` to a **draft** GitHub release. The archive contains
`Coop/`, ready to extract into Bannerlord's `Modules/` directory. The tag sets
`CoopVersion` for this build and the module manifest; no source version edit is
required. Nightly and manual builds still read `CoopVersion` from
`source/Directory.Build.props`.

The `Validate release tag` step fails unless the tag is a stable
`vMAJOR.MINOR.PATCH` without leading zeros, such as `v0.2.0`.

`createDraft` in `.github/scripts/tag-release.cjs` reuses an existing draft for
the tag, keeping its title and notes, or creates one named after the tag with
generated release notes. If the updated draft no longer matches the tag, the
pushed commit or the draft state, it stops before uploading or dispatching.

The draft job ends by sending `build_release` to the private
`Bannerlord-Coop-Team/BannerlordCoop.DedicatedServer` repository. What that
repository does next is maintained there. No workflow in this repository
publishes the draft.

## Setup

- The tagged commit must contain `.github/workflows/release.yml` and
  `.github/scripts/tag-release.cjs`, because a tag push runs the workflow file
  from the pushed commit.
- The `release` environment's deployment rule must allow `v*` tags. The sign
  and draft jobs both use that environment.
- Store `RELEASE_DISPATCH_TOKEN` as a `release` environment secret (a
  repository secret also works), so only jobs allowed into that environment can
  read it. This repository's draft job needs at least Contents read/write on
  this repository and Contents write on the dedicated server repository. It
  also needs Workflows write here when the tagged commit adds or changes files
  under `.github/workflows` relative to `development`, because GitHub requires
  it to create or update that release. Nothing in this repository needs
  Actions: read on this token anymore. Check the dedicated server repository
  before narrowing a shared token.
- Keep the existing Azure signing secrets and `SIGNING_*` variables configured.
  The sign job names the `release` environment, so its OIDC subject names that
  environment, not the tag. The Azure app needs a federated credential for the
  `release` environment in addition to the development-branch credential that
  nightly and manual builds use.
- Keep `release.yml`'s `GITHUB_TOKEN` at `contents: read` (plus
  `id-token: write` in the sign job). The draft job's release and dispatch
  calls use `RELEASE_DISPATCH_TOKEN`.
- Protect `v*` tags against unauthorized creation, deletion and replacement.
  Treat the cross-repository token as a release credential.

Pushing a tag is not a dry run. It creates or updates the draft and sends
`build_release`.

## Dispatch contract

The draft job sends one `repository_dispatch` event, `build_release`. IDs and
attempts are JSON numbers.

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

A successful draft job means GitHub accepted `build_release`, not that the
dedicated server repository started or finished a build.

The draft description carries a `release-coordination` HTML comment.
`createDraft` writes the latest `build_release` payload into it on every run.
Leave it intact.

## Recovery and checks

- Failed build, test, sign, upload or dispatch: rerun the release workflow.
  Only the draft's client zip is replaced.
- Prefer **Re-run failed jobs** while the job artifacts last (one day). Jobs
  that passed are not run again, so a rerun of a failed draft job reuses the
  signed package and the zip keeps its bytes. **Re-run all jobs** signs again
  and replaces the zip. After a day, re-run all jobs.
- Every rerun leaves other draft assets in place and sends `build_release`
  again. If the dedicated server build has started, check that repository's
  recovery steps first.
- A 401, 403 or 404 in the draft job points at `RELEASE_DISPATCH_TOKEN`;
  check its expiry and the permissions under Setup before replacing it.
- Create tags with `git push`. If a release for the tag is already published,
  the draft job stops after signing.
- Never move a release tag to retry a build. Use a new version for changed code.

Local targeted checks (no game deployment or GitHub mutations):

```sh
node --test .github/scripts/tag-release.test.cjs
```
