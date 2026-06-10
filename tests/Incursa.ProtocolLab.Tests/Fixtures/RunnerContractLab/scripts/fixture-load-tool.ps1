param(
    [string]$Mode = "success",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArguments = @()
)

$ErrorActionPreference = "Continue"

function Get-ArgumentValue {
    param(
        [string[]]$Arguments,
        [string]$Name,
        [string]$DefaultValue
    )

    for ($i = 0; $i -lt $Arguments.Count; $i++) {
        if ($Arguments[$i] -eq $Name -and ($i + 1) -lt $Arguments.Count) {
            return $Arguments[$i + 1]
        }
    }

    return $DefaultValue
}

function Write-RawQuicDocument {
    param(
        [bool]$Valid
    )

    $streams = [int](Get-ArgumentValue $RemainingArguments "--streams-per-connection" "1")
    $payloadSize = [int](Get-ArgumentValue $RemainingArguments "--payload-size-bytes" "1024")
    $behavior = Get-ArgumentValue $RemainingArguments "--behavior" "duplex-streams"
    $payloadDirection = Get-ArgumentValue $RemainingArguments "--payload-direction" "bidirectional"
    $bytesSent = [int64]$streams * [int64]$payloadSize
    $bytesReceived = if ($payloadDirection -eq "bidirectional") { $bytesSent } else { 0 }
    $failedRequests = 0
    $timeoutRequests = 0
    $completedStreams = $streams

    if (-not $Valid) {
        $bytesReceived = 0
        $failedRequests = 1
        $timeoutRequests = 1
        $completedStreams = [Math]::Max(0, $streams - 1)
    }

    [Console]::Error.WriteLine("fixture raw quic stderr preserved")
    @"
{
  "tool": "fixture-raw-quic-load",
  "category": "managed-lab",
  "protocol": "quic",
  "behavior": "$behavior",
  "metrics": {
    "requestsPerSecond": 1.0,
    "totalRequests": $streams,
    "successfulRequests": $completedStreams,
    "failedRequests": $failedRequests,
    "timeoutRequests": $timeoutRequests,
    "completedStreams": $completedStreams,
    "bytesReceived": $bytesReceived,
    "bytesSent": $bytesSent,
    "throughputBytesPerSecond": 1.0,
    "latencyMeanMs": 1.0,
    "latencyP50Ms": 1.0,
    "latencyP95Ms": 1.0,
    "latencyP99Ms": 1.0
  },
  "warnings": ["fixture raw QUIC load output"],
  "errors": []
}
"@ | Write-Output
}

switch ($Mode) {
    "version" {
        Write-Output "fixture-load-tool 1.0.0"
    }
    "success" {
        @"
{
  "summary": {
    "successRate": 1.0,
    "total": 1.0,
    "requestsPerSec": 42.0,
    "average": 0.005,
    "sizePerSec": 1024.0
  },
  "responseTimeHistogram": {
    "0.001": 1
  },
  "latencyPercentiles": {
    "p50": 0.001,
    "p95": 0.001,
    "p99": 0.001
  }
}
"@ | Write-Output
    }
    "parsefail" {
        Write-Output "fixture parse failure"
    }
    "fail" {
        Write-Error "fixture load failure"
        exit 1
    }
    "rawquic-success" {
        Write-RawQuicDocument -Valid $true
    }
    "rawquic-validationfail" {
        Write-RawQuicDocument -Valid $false
    }
    "rawquic-parsefail" {
        [Console]::Error.WriteLine("fixture raw quic parse stderr preserved")
        Write-Output "fixture raw quic parse failure"
    }
    default {
        Write-Error "Unknown fixture mode '$Mode'."
        exit 1
    }
}
