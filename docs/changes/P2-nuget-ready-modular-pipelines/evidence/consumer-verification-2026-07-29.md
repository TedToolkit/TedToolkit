# Consumer and CI verification — 2026-07-29

- Latest local candidate version: `2026.7.30.3`
- Package contract: passed for all three coordinated packages
- Dependency audit: no known vulnerable or deprecated direct/transitive packages
- Build-only consumer: clean restore, strict Release build, and Validate passed
- GitHub consumer: clean restore, strict Release build, and Validate passed
- GitLab consumer: clean restore, strict Release build, RunBuild Validate, and cross-process ConsumeArtifacts passed
- EverythingButTheSink-derived consumer: package-only host ran a no-adapter Build, a neutral fake description adapter, Build/TUnit/Pack, two RID publish archives, copied-manifest Message/Publish, fake NuGet/GitLab MR/Generic Package/Release boundaries, neutral notification adapters, and exact event/extension ordering
- GitHub Actions dry run: [30508915616](https://github.com/TedToolkit/TedToolkit/actions/runs/30508915616), successful at commit `84c25a520320c45c3b2a96460db557ce913c209a` on Ubuntu with read-only contents permission; repository Validate, candidate generation, package-only restore/strict build, the complete `produce → consume → produce` consumer flow, and the second-process GitLab artifact handoff passed, while publication and recovery jobs were skipped
- GitHub template SHA-256: `183ff1d87bffda7bc45e6d8a7d1dc574d823c3bd56b6060952c25c9ab34a484a`
- GitLab template SHA-256: `a927b543810140ae9c017e2a4f02123ce7cb36d94300d79c791ba7034cabd3b3`
- User-approved GitLab verification substitution: 2026-07-30
- GitLab self-test: passed by executing the canonical template's package-only Build and Combine command flow in separate processes with copied artifact handoff, full typed-manifest validation, fake provider boundaries, and zero real remote mutation
- Secret rotation: the User confirmed server-side revocation on 2026-07-30; gitignored `Build/appsettings.json` is absent, and tracked files, packages, evidence, logs, and the migrated host/workflow contain no plaintext AI credential

The full hosted run resolved candidate `2026.7.29`, generated the three coordinated `.nupkg` files, restored all four package-only hosts from that source, strictly built them, and completed every side-effect-free host invocation. Its derived compatibility fixture generated and copied a strict typed manifest containing one passed TUnit test and distinct `linux-x64`/`win-x64` archives. Process B executed no Build command and validated fake MR, two NuGet pushes, four Generic Package uploads across the no-adapter/adapter runs, two Releases, and the exact Message/Publish event sequences. Logs contain only synthetic credentials and method/path records.

The User stated on 2026-07-30 that a real GitLab project/runner cannot be provided and explicitly accepted repository-owned self-test evidence. The retained GitLab template remains a supported consumer example, but production runner/executor/proxy policy is a consumer rollout responsibility rather than a release gate for this work package.

The first GitHub run, [30461324997](https://github.com/TedToolkit/TedToolkit/actions/runs/30461324997), correctly exposed an over-broad root `artifacts` ignore rule that excluded the Build manifest-contract source directory on a clean checkout. Commit `949a5c444abf6666793b54f6abd22cd702c4602c` anchored the cache rule to `/artifacts/`; later successful runs verify the root-cause correction.
