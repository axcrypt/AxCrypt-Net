# Building AxCrypt

## Prerequisites

### .NET 10 SDK

Install the .NET 10 SDK for your platform from
https://dotnet.microsoft.com/download/dotnet/10.0 :

- **Windows**: `winget install Microsoft.DotNet.SDK.10`
- **macOS**: `brew install --cask dotnet-sdk` (or the official installer)
- **Linux**: distribution packages or
  `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0`

Verify: `dotnet --version` prints `10.x`.

## Restore, build, test

```bash
# Cross-platform (Windows, macOS, Linux): CLI and shared libraries
dotnet restore src/AxCrypt.Cli/AxCrypt.Cli.csproj
dotnet build   src/AxCrypt.Cli/AxCrypt.Cli.csproj -c Release

# Run the cross-platform tests
dotnet test tests/AxCrypt.Cli.Test/AxCrypt.Cli.Test.csproj  -c Release
dotnet test tests/AxCrypt.Core.Test/AxCrypt.Core.Test.csproj -c Release
dotnet test tests/AxCrypt.Common.Test/AxCrypt.Common.Test.csproj -c Release
dotnet test tests/AxCrypt.Mono.Test/AxCrypt.Mono.Test.csproj -c Release

# Full solution
dotnet build src/AxCrypt.Net.App.sln -c Release
```

## Publishing the CLI

```bash
# Framework-dependent, single folder
dotnet publish src/AxCrypt.Cli/AxCrypt.Cli.csproj -c Release -o publish/cli

# Self-contained single file per platform
dotnet publish src/AxCrypt.Cli/AxCrypt.Cli.csproj -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o publish/win-x64
dotnet publish src/AxCrypt.Cli/AxCrypt.Cli.csproj -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish/osx-arm64
dotnet publish src/AxCrypt.Cli/AxCrypt.Cli.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64
```

The binary is named `axcrypt` (`axcrypt.exe` on Windows).

## Platform-specific notes

- **CLI settings**: the CLI stores non-secret settings (e.g. calibrated
  key-wrap iteration counts) under the per-user application data folder;
  override with the `AXCRYPT_CLI_WORKFOLDER` environment variable.
- **Legacy projects**: `shared/AxCrypt.Reports*` are legacy and not part
  of the solution; they are retained for reference only.

## Formatting and analysis

```bash
dotnet format --verify-no-changes
```

## Signing

Community builds are unsigned; that is expected and fine. No
certificates, private keys, or provisioning profiles exist in this
repository. See [docs/SIGNING.md](docs/SIGNING.md) for official-build
signing notes.
