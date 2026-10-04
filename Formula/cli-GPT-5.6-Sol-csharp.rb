class CliGpt56SolCsharp < Formula
  desc "CLI for encrypting and decrypting rclone crypt files"
  homepage "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp"
  version "0.1.0"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp/releases/download/v0.1.0/cli-GPT-5.6-Sol-csharp-darwin-arm64.tar.gz"
      sha256 "5a89d661162ef261700a27f07377a768b4af79e7367e55a90d1368fc206f453d"
    else
      url "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp/releases/download/v0.1.0/cli-GPT-5.6-Sol-csharp-darwin-amd64.tar.gz"
      sha256 "3ea4ed668007c2349fab5d0abea743523c4883f3ae6f15f75f90a0fa9dd18494"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp/releases/download/v0.1.0/cli-GPT-5.6-Sol-csharp-linux-arm64.tar.gz"
      sha256 "0a470a7a7bde2ac68978e1b32f0b3c21b0951aac3b8e27dabf4ab1031d8afe7f"
    else
      url "https://github.com/llm-supermarket/cli-GPT-5.6-Sol-csharp/releases/download/v0.1.0/cli-GPT-5.6-Sol-csharp-linux-amd64.tar.gz"
      sha256 "ea32f846207f069b53c324a41023844841cd1ab78bc8e28dbd1eee46a7119018"
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