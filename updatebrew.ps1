param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$name = "cli-GPT-5.6-Sol-csharp"
$baseUrl = "https://github.com/llm-supermarket/$name/releases/download/v$Version"
$platforms = @("darwin-amd64", "darwin-arm64", "linux-amd64", "linux-arm64")
$hashes = @{}

foreach ($platform in $platforms) {
    $archive = "$name-$platform.tar.gz"
    $artifactPath = Join-Path $PSScriptRoot "artifacts" $archive
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Unable to locate $artifactPath"
    }

    $hashes[$platform] = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

$formula = @"
class CliGpt56SolCsharp < Formula
  desc "CLI for encrypting and decrypting rclone crypt files"
  homepage "https://github.com/llm-supermarket/$name"
  version "$Version"

  on_macos do
    if Hardware::CPU.arm?
      url "$baseUrl/$name-darwin-arm64.tar.gz"
      sha256 "$($hashes['darwin-arm64'])"
    else
      url "$baseUrl/$name-darwin-amd64.tar.gz"
      sha256 "$($hashes['darwin-amd64'])"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "$baseUrl/$name-linux-arm64.tar.gz"
      sha256 "$($hashes['linux-arm64'])"
    else
      url "$baseUrl/$name-linux-amd64.tar.gz"
      sha256 "$($hashes['linux-amd64'])"
    end
  end

  def install
    bin.install "$name-darwin-arm64" => "$name" if OS.mac? && Hardware::CPU.arm?
    bin.install "$name-darwin-amd64" => "$name" if OS.mac? && !Hardware::CPU.arm?
    bin.install "$name-linux-arm64" => "$name" if OS.linux? && Hardware::CPU.arm?
    bin.install "$name-linux-amd64" => "$name" if OS.linux? && !Hardware::CPU.arm?
  end

  test do
    assert_match "$name #{version}", shell_output("#{bin}/$name --version")
  end
end
"@

$formulaPath = Join-Path $PSScriptRoot "Formula" "$name.rb"
Set-Content -LiteralPath $formulaPath -Value $formula -NoNewline
