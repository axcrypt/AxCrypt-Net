# Releasing AxCrypt

This document describes how releases are produced. Only AxCrypt AB can
produce **official** (signed) releases; anyone can produce community
builds, which must not be presented as official (see TRADEMARK.md).

## Versioning

- Semantic versioning: `MAJOR.MINOR.PATCH`.
- Releases are tagged `vX.Y.Z` from `master`.
- The CLI version is driven by `<Version>` in
  `src/AxCrypt.Cli/AxCrypt.Cli.csproj`.

## Release checklist

1. [ ] `master` is green: build, tests, format, and secret scan.
2. [ ] Dependency review: no known vulnerable or GPL-incompatible
       packages; `THIRD-PARTY-NOTICES.md` is up to date.
3. [ ] Version numbers bumped (CLI csproj, app build targets).
4. [ ] CHANGELOG/release notes drafted, including security fixes and
       credits.
5. [ ] No secrets in the tree: secret scan (gitleaks) clean.
6. [ ] Tag `vX.Y.Z` and build release artifacts from that tag.
7. [ ] SBOMs generated and attached (see below).
8. [ ] SHA-256 checksums generated and attached.
9. [ ] **Official only:** artifacts signed by AxCrypt AB — see
       docs/SIGNING.md.
10. [ ] GitHub release created from the draft, artifacts + checksums +
        SBOM attached, release notes include the "verifying official
        builds" section.
11. [ ] Post-release: verify downloads, signatures, and checksums from a
        clean machine.

## Packaging

Release packaging should produce:

- `axcrypt-cli-<version>-win-x64.zip`
- `axcrypt-cli-<version>-osx-arm64.tar.gz`
- `axcrypt-cli-<version>-linux-x64.tar.gz`

## Checksums

```bash
sha256sum axcrypt-cli-* > SHA256SUMS.txt
```

`SHA256SUMS.txt` is attached to the GitHub release. Users verify with
`sha256sum -c SHA256SUMS.txt` (or `Get-FileHash` on Windows).

## SBOM generation

Generate SPDX SBOMs per artifact, e.g. with Microsoft's `sbom-tool`
or Syft:

```bash
sbom-tool generate -b publish/linux-x64 -bc . -pn AxCrypt.Cli -pv X.Y.Z -ps "AxCrypt AB"
```

SBOM files are attached to the release.

## Verifying official builds (include in release notes)

Official AxCrypt releases are digitally signed by **AxCrypt AB**:

- **Windows**: right-click → Properties → Digital Signatures, or
  `signtool verify /pa <file>`; the signer must be "AxCrypt AB".
- **macOS**: `codesign -dv --verbose=2 <file>` shows a Developer ID of
  AxCrypt AB when official macOS artifacts are provided.
- **Checksums**: compare SHA-256 with the `SHA256SUMS.txt` attached to the
  release.

A build that is not signed by AxCrypt AB is not an official build, even if
it is functionally identical.
