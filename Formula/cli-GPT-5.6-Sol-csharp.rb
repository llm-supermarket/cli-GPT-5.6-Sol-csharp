class CliGpt56SolCsharp < Formula
  desc "CLI for encrypting and decrypting rclone crypt files"
  homepage "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp"
  version "0.1.0"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp/releases/download/v0.1.0/cli-GPT-5.6-Sol-csharp-darwin-arm64.tar.gz"
      sha256 "RELEASE_WORKFLOW_UPDATES_THIS_HASH"
    else
      url "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp/releases/download/v0.1.0/cli-GPT-5.6-Sol-csharp-darwin-amd64.tar.gz"
      sha256 "RELEASE_WORKFLOW_UPDATES_THIS_HASH"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp/releases/download/v0.1.0/cli-GPT-5.6-Sol-csharp-linux-arm64.tar.gz"
      sha256 "RELEASE_WORKFLOW_UPDATES_THIS_HASH"
    else
      url "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp/releases/download/v0.1.0/cli-GPT-5.6-Sol-csharp-linux-amd64.tar.gz"
      sha256 "RELEASE_WORKFLOW_UPDATES_THIS_HASH"
    end
  end

  def install
    bin.install "cli-GPT-5.6-Sol-csharp-darwin-arm64" => "cli-GPT-5.6-Sol-csharp" if OS.mac? && Hardware::CPU.arm?
    bin.install "cli-GPT-5.6-Sol-csharp-darwin-amd64" => "cli-GPT-5.6-Sol-csharp" if OS.mac? && !Hardware::CPU.arm?
    bin.install "cli-GPT-5.6-Sol-csharp-linux-arm64" => "cli-GPT-5.6-Sol-csharp" if OS.linux? && Hardware::CPU.arm?
    bin.install "cli-GPT-5.6-Sol-csharp-linux-amd64" => "cli-GPT-5.6-Sol-csharp" if OS.linux? && !Hardware::CPU.arm?
  end

  test do
    assert_match "cli-GPT-5.6-Sol-csharp #{version}", shell_output("#{bin}/cli-GPT-5.6-Sol-csharp --version")
  end
end
