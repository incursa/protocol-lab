param(
    [string]$Mode = "success"
)

$ErrorActionPreference = "Continue"

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
    default {
        Write-Error "Unknown fixture mode '$Mode'."
        exit 1
    }
}
