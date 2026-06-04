// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Incursa.ProtocolLab.Runner;

internal static class QuicSendRetry
{
    private const string CongestionControllerExhaustedMessage = "The congestion controller cannot send another ordinary packet.";
    private const string FlowControlCreditExhaustedMessage = "Writes that wait for additional flow-control credit are not supported by this slice.";
    private static readonly TimeSpan CongestionRetryDelay = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan CongestionRetryTimeout = TimeSpan.FromSeconds(30);

    internal static Task RetryTransientSendCreditAsync(
        Func<CancellationToken, ValueTask> operation,
        string congestionTimeoutMessage,
        string? flowControlTimeoutMessage = null,
        TimeSpan? retryTimeout = null,
        TimeSpan? operationAttemptTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return RetryTransientSendCreditAsync(
            async cancellationToken =>
            {
                await operation(cancellationToken).ConfigureAwait(false);
                return true;
            },
            congestionTimeoutMessage,
            flowControlTimeoutMessage,
            retryTimeout,
            operationAttemptTimeout);
    }

    internal static async Task<T> RetryTransientSendCreditAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        string congestionTimeoutMessage,
        string? flowControlTimeoutMessage = null,
        TimeSpan? retryTimeout = null,
        TimeSpan? operationAttemptTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(congestionTimeoutMessage);

        TimeSpan effectiveRetryTimeout = retryTimeout ?? CongestionRetryTimeout;
        TimeSpan effectiveAttemptTimeout = operationAttemptTimeout ?? TimeSpan.FromSeconds(5);
        if (effectiveAttemptTimeout > effectiveRetryTimeout)
        {
            effectiveAttemptTimeout = effectiveRetryTimeout;
        }

        long startedAt = Stopwatch.GetTimestamp();

        while (true)
        {
            using CancellationTokenSource attemptTimeout = new(effectiveAttemptTimeout);

            try
            {
                return await operation(attemptTimeout.Token).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (IsTransientCongestionExhaustion(ex))
            {
                if (Stopwatch.GetElapsedTime(startedAt) >= effectiveRetryTimeout)
                {
                    throw new TimeoutException(congestionTimeoutMessage, ex);
                }

                await Task.Delay(CongestionRetryDelay).ConfigureAwait(false);
            }
            catch (NotSupportedException ex) when (flowControlTimeoutMessage is not null && IsTransientFlowControlCreditExhaustion(ex))
            {
                if (Stopwatch.GetElapsedTime(startedAt) >= effectiveRetryTimeout)
                {
                    throw new TimeoutException(flowControlTimeoutMessage, ex);
                }

                await Task.Delay(CongestionRetryDelay).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (attemptTimeout.IsCancellationRequested)
            {
                if (Stopwatch.GetElapsedTime(startedAt) >= effectiveRetryTimeout)
                {
                    throw new TimeoutException(congestionTimeoutMessage, ex);
                }

                await Task.Delay(CongestionRetryDelay).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransientCongestionExhaustion(InvalidOperationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return string.Equals(exception.Message, CongestionControllerExhaustedMessage, StringComparison.Ordinal);
    }

    private static bool IsTransientFlowControlCreditExhaustion(NotSupportedException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return string.Equals(exception.Message, FlowControlCreditExhaustedMessage, StringComparison.Ordinal);
    }
}
