# Consumer and CI verification — 2026-07-29

- Candidate version: `2026.7.29.3`
- Package contract: passed for all three coordinated packages
- Dependency audit: no known vulnerable or deprecated direct/transitive packages
- Build-only consumer: clean restore, strict Release build, and Validate passed
- GitHub consumer: clean restore, strict Release build, and Validate passed
- GitLab consumer: clean restore, strict Release build, RunBuild Validate, and cross-process ConsumeArtifacts passed
- EverythingButTheSink-derived consumer: package-only host ran a no-adapter Build, a neutral fake description adapter, Build/TUnit/Pack, two RID publish archives, copied-manifest Message/Publish, fake NuGet/GitLab MR/Generic Package/Release boundaries, neutral notification adapters, and exact event/extension ordering
- GitHub Actions dry run: [30464312313](https://github.com/TedToolkit/TedToolkit/actions/runs/30464312313), successful at commit `dc9a3660a006188aca4e40bb4d5fecdc7367631f` on Ubuntu with read-only contents permission; repository Validate and the complete candidate-package consumer job passed, while publication and recovery jobs were skipped
- GitLab CI dry run: pending a User-provided test project/runner
- Secret rotation: pending repository-owner confirmation; the migrated host/workflow contains no plaintext AI credential

The full hosted run resolved candidate `2026.7.29`, generated the three coordinated `.nupkg` files, restored all four package-only hosts from that source, strictly built them, and completed every side-effect-free host invocation. Its derived compatibility fixture generated and copied a strict typed manifest containing one passed TUnit test and distinct `linux-x64`/`win-x64` archives. Process B executed no Build command and validated fake MR, two NuGet pushes, four Generic Package uploads across the no-adapter/adapter runs, two Releases, and the exact Message/Publish event sequences. Logs contain only synthetic credentials and method/path records.

The first GitHub run, [30461324997](https://github.com/TedToolkit/TedToolkit/actions/runs/30461324997), correctly exposed an over-broad root `artifacts` ignore rule that excluded the Build manifest-contract source directory on a clean checkout. Commit `949a5c444abf6666793b54f6abd22cd702c4602c` anchored the cache rule to `/artifacts/`; later successful runs verify the root-cause correction.
