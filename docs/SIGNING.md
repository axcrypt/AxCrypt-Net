# Code Signing — Official AxCrypt AB Releases

Official AxCrypt releases are digitally signed by AxCrypt AB. **No
certificates, private keys, keystores, or provisioning profiles are stored
in this repository**. Signing material lives outside the repository in
protected release infrastructure. Community builds are unsigned; they work
identically but must not be presented as official (see ../TRADEMARK.md).

## Windows Authenticode

Official Windows artifacts are signed with an AxCrypt AB code-signing
certificate.

| Setting | Source | Purpose |
|---|---|---|
| `AX_SIGNING_CERT_THUMBPRINT` | release secret / env | SHA-1 thumbprint of the AxCrypt AB certificate in the machine certificate store |
| `AX_SIGNTOOL_PATH` | env, optional | Full path to `signtool.exe`; defaults to `signtool.exe` on `PATH` |
| `AX_TIMESTAMP_URL` | env, optional | RFC 3161 timestamp server |

Official release flow: import the PFX from protected secret storage into
the ephemeral build machine's certificate store, set
`AX_SIGNING_CERT_THUMBPRINT`, build, sign, verify with `signtool`, and
delete the imported key material afterwards.

Verification by users: `signtool verify /pa <file>` or the
file's Digital Signatures tab — signer must be **AxCrypt AB**.

## macOS Developer ID

Required release secrets, never committed:

| Secret | Purpose |
|---|---|
| `APPLE_DEVELOPER_ID_APPLICATION_P12` (base64) + `APPLE_P12_PASSWORD` | Developer ID Application certificate |
| `APPLE_ID`, `APPLE_TEAM_ID`, `APPLE_APP_SPECIFIC_PASSWORD` | Notarization credentials for `notarytool` |

Release flow: create an ephemeral keychain → import the P12 →
`codesign --timestamp --options runtime` all binaries →
`xcrun notarytool submit --wait` → `xcrun stapler staple`.

Verification by users: `codesign -dv --verbose=2` (Developer ID: AxCrypt
AB) and `spctl -a -vv` (accepted, notarized).

## Rules

1. Signing secrets exist only in protected release environments with required
   reviewers; never in the repository, issues, or logs.
2. Ephemeral runners must delete imported keys/keychains in an `always()`
   cleanup step.
3. Community forks: leave all signing variables unset — builds succeed
   unsigned. Do not sign forks in a way that implies AxCrypt AB origin.
4. Rotation: on any suspicion of compromise, revoke the certificate,
   rotate secrets, and publish an advisory.
