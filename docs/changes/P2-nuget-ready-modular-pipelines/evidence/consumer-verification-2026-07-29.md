# Consumer and CI verification — 2026-07-29

- Candidate version: `2026.7.29.3`
- Package contract: passed for all three coordinated packages
- Dependency audit: no known vulnerable or deprecated direct/transitive packages
- Build-only consumer: clean restore, strict Release build, and Validate passed
- GitHub consumer: clean restore, strict Release build, and Validate passed
- GitLab consumer: clean restore, strict Release build, RunBuild Validate, and cross-process ConsumeArtifacts passed
- GitHub Actions dry run: [30461479405](https://github.com/TedToolkit/TedToolkit/actions/runs/30461479405), successful on Ubuntu with read-only contents permission; publication and recovery jobs were skipped
- GitLab CI dry run: pending a User-provided test project/runner
- Secret rotation: pending repository-owner confirmation; the migrated host/workflow contains no plaintext AI credential

The first GitHub run, [30461324997](https://github.com/TedToolkit/TedToolkit/actions/runs/30461324997), correctly exposed an over-broad root `artifacts` ignore rule that excluded the Build manifest-contract source directory on a clean checkout. Commit `949a5c444abf6666793b54f6abd22cd702c4602c` anchored the cache rule to `/artifacts/`; the successful run above verifies the root-cause correction.
