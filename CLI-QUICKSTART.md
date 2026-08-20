# AxCrypt CLI Quick Start

Build the command-line tool:

```bash
dotnet build src/AxCrypt.Cli/AxCrypt.Cli.csproj -c Release
```

Run it from the project:

```bash
dotnet run --project src/AxCrypt.Cli -- help
```

Encrypt a file:

```bash
dotnet run --project src/AxCrypt.Cli -- encrypt --input file.txt --output file.txt.axx
```

Decrypt a file:

```bash
dotnet run --project src/AxCrypt.Cli -- decrypt --input file.txt.axx --output file.txt
```

Use `AXCRYPT_PASSWORD` for scripts:

```bash
export AXCRYPT_PASSWORD='your-password'
dotnet run --project src/AxCrypt.Cli -- encrypt --input file.txt --force
unset AXCRYPT_PASSWORD
```

Generate a key pair for key sharing:

```bash
dotnet run --project src/AxCrypt.Cli -- keygen --email you@example.com --output ./keys
```

Encrypt for a recipient:

```bash
dotnet run --project src/AxCrypt.Cli -- encrypt --input file.txt --recipient-public-key ./keys/you@example.com-public.json
```

See the full reference in [docs/CLI.md](docs/CLI.md).
