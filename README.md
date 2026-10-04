# rclone-encrypt-GPT-5.6-Sol
A small CLI tool that encrypts and decrypts using the rclone encryption defaults. 

Rclone uses a custom salt if no salt is provided, which this tool will use by default. A few similar tools:

- https://github.com/rclone/rclone
- https://github.com/mcolatosti/rclonedecrypt
- https://github.com/br0kenpixel/rclone-rcc
- @fyears/rclone-crypt

Rclone encryption uses: 
- NaCl SecretBox (XSalsa20 + Poly1305) for the file contents.
- AES256 for the filenames.
- scrypt for keymaterial.

## Installation

The release binaries are self-contained; neither .NET nor another language runtime needs to be installed.

**Homebrew (macOS/Linux)**

```bash
brew tap llm-supermarket/cli-GPT-5.6-Sol-csharp https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp
brew install cli-GPT-5.6-Sol-csharp
```

**Scoop (Windows)**

```bash
scoop bucket add cli-GPT-5.6-Sol-csharp https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp
scoop install cli-GPT-5.6-Sol-csharp
```

## Example usage

### Encrypt a file

The CLI securely prompts for a required password and an optional salt. Press Enter at the salt prompt to use rclone's default salt. If `--output-file` is omitted, the output filename is encrypted beside the input file.

```bash
cli-GPT-5.6-Sol-csharp encrypt --input-file ./TEST_FILE.txt
```

### Decrypt a file

If `--output-file` is omitted, the encrypted input filename is decrypted and used for the output.

```bash
cli-GPT-5.6-Sol-csharp decrypt -i ./kr9tu4e1da4u3nifdd99g9tf5o
```

### Choose an output file

```bash
cli-GPT-5.6-Sol-csharp decrypt \
  --input-file ./kr9tu4e1da4u3nifdd99g9tf5o \
  --output-file ./restored.txt
```

### Use base64 filename encoding

Rclone's default filename encoding is lowercase, unpadded base32hex. Base64 uses rclone's unpadded URL-safe format and is appropriate only for case-sensitive storage.

```bash
cli-GPT-5.6-Sol-csharp encrypt \
  -i "./TEST_FILE BASE64.txt" \
  --filename-encoding base64

cli-GPT-5.6-Sol-csharp decrypt \
  -i ./Iyxcijgc9bp3o5Y0npW6xqUvwWNcc3MA4SadB0sR6cY \
  --filename-encoding base64
```

### Non-interactive use

Environment variables keep secrets out of process listings and most shell histories.

```bash
export RCLONE_ENCRYPT_PASSWORD='replace-me'
export RCLONE_ENCRYPT_SALT='optional-salt'
cli-GPT-5.6-Sol-csharp encrypt -i ./TEST_FILE.txt
unset RCLONE_ENCRYPT_PASSWORD RCLONE_ENCRYPT_SALT
```

`--password` and `--salt` are also available, but command-line passwords can be exposed through process listings and shell history. The CLI prints a warning whenever `--password` is used. Prefer `RCLONE_ENCRYPT_PASSWORD`; if a command-line password was used, remove that entry from terminal history.

```bash
cli-GPT-5.6-Sol-csharp encrypt \
  -i ./TEST_FILE.txt \
  --password 'replace-me' \
  --salt 'optional-salt'
```

## Flags

| Flag | Default | Description |
|---|---|---|
| `-i`, `--input-file` | *(required)* | Input file |
| `-o`, `--output-file` | decrypted/encrypted input filename | Output file |
| `--password` | secure prompt or `RCLONE_ENCRYPT_PASSWORD` | Password; warns because command-line secrets are unsafe |
| `--salt` | secure prompt or `RCLONE_ENCRYPT_SALT` | Optional salt; an empty value uses the rclone default |
| `--filename-encoding` | `base32` | Filename encoding: `base32` or `base64` |
| `-h`, `--help` | | Show help |
| `-v`, `--version` | | Show version |

## Building from source

Requires the .NET 10 SDK.

```bash
git clone https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp
cd cli-GPT-5.6-Sol-csharp
dotnet test cli-GPT-5.6-Sol-csharp.slnx --configuration Release
dotnet publish src/RcloneEncrypt/RcloneEncrypt.csproj --configuration Release --runtime linux-x64
```

## Releases

Pushing a `vX.Y.Z` tag triggers the [Build and Release workflow](.github/workflows/build-release.yml). It tests and publishes self-contained single-file binaries for Linux and macOS (x64/arm64) and Windows (x64), creates a GitHub Release, then updates the Scoop manifest and Homebrew formula in this repository.
