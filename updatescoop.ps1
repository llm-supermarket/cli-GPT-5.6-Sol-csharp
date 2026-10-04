param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$name = "cli-GPT-5.6-Sol-csharp"
$exePath = Join-Path $PSScriptRoot "artifacts" "$name-windows-amd64.exe"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Unable to locate $exePath"
}

$manifestPath = Join-Path $PSScriptRoot "$name.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.version = $Version
$manifest.architecture."64bit".url = "https://github.com/llm-supermarket/$name/releases/download/v$Version/$name-windows-amd64.exe"
$manifest.architecture."64bit".hash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -NoNewline
