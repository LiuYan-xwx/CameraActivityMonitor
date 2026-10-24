using System.Text;
using CameraActivityMonitor.Helpers;
using Microsoft.Extensions.Logging;

namespace CameraActivityMonitor;

/// <summary>
/// 基于 CapabilityAccessManager 注册表项的多路径摄像头活动监控器。
/// 轮询在后台线程执行，状态变化通过 <see cref="StateChanged"/> 通知调用方。
/// </summary>
internal sealed class RegistryCameraActivityMonitor : IDisposable
{
    private readonly IReadOnlyList<RegistryWatchTarget> _targets;
    private readonly TimeSpan _interval;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, string?> _lastLoggedErrors = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cancellationTokenSource;
    private bool _started;
    private bool _disposed;
    private string? _lastFingerprint;

    public RegistryCameraActivityMonitor(
        IEnumerable<string> configuredPaths,
        int intervalSeconds,
        ILogger? logger = null)
    {
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Clamp(intervalSeconds, 1, 3600));
        _targets = BuildTargets(configuredPaths);

        var initialPathStates = _targets.Select(CreateInitialState).ToList();
        PathStates = initialPathStates;
        ActiveProcessNames = Array.Empty<string>();
        InitialSnapshot = new RegistryMonitorSnapshot(false, initialPathStates, ActiveProcessNames);
    }

    public bool HasValidTargets => _targets.Any(target => target.SubKey is not null);

    public bool IsInUse { get; private set; }

    public IReadOnlyList<RegistryPathState> PathStates { get; private set; }

    public IReadOnlyList<string> ActiveProcessNames { get; private set; }

    public RegistryMonitorSnapshot InitialSnapshot { get; }

    public event Action<RegistryMonitorSnapshot>? StateChanged;

    public void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RegistryCameraActivityMonitor));
        }

        if (_started)
        {
            return;
        }

        _started = true;
        _lastFingerprint = null;
        var cancellationTokenSource = new CancellationTokenSource();
        _cancellationTokenSource = cancellationTokenSource;
        _ = Task.Run(() => PollLoopAsync(cancellationTokenSource.Token));
    }

    public void Stop()
    {
        var cancellationTokenSource = _cancellationTokenSource;
        _cancellationTokenSource = null;
        _started = false;

        if (cancellationTokenSource is null)
        {
            return;
        }

        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已被另一个停止路径释放。
        }

        cancellationTokenSource.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private static IReadOnlyList<RegistryWatchTarget> BuildTargets(IEnumerable<string> configuredPaths)
    {
        var targets = new List<RegistryWatchTarget>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawPath in configuredPaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var configuredPath = rawPath.Trim().Trim('"').Trim();
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                continue;
            }

            if (!CameraRegistryHelper.TryNormalizeKeyPath(
                    configuredPath,
                    out var subKey,
                    out var processName,
                    out var error))
            {
                targets.Add(new RegistryWatchTarget(configuredPath, null, null, error ?? "路径无效"));
                continue;
            }

            if (!seenKeys.Add(subKey))
            {
                continue;
            }

            targets.Add(new RegistryWatchTarget(configuredPath, subKey, processName, null));
        }

        return targets;
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "轮询注册表摄像头状态时发生未处理异常");
                }

                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止。
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "注册表摄像头轮询循环意外退出");
        }
    }

    private void PollOnce()
    {
        var states = new List<RegistryPathState>(_targets.Count);
        var activeProcessNames = new List<string>();
        var anyInUse = false;

        foreach (var target in _targets)
        {
            if (target.SubKey is null)
            {
                states.Add(CreateInitialState(target));
                continue;
            }

            var result = CameraRegistryHelper.ReadLastUsedTimeStop(target.SubKey);
            states.Add(new RegistryPathState(
                target.ConfiguredPath,
                target.ProcessName,
                result.InUse,
                result.HasError,
                result.Status));

            if (result.InUse)
            {
                anyInUse = true;
                if (!string.IsNullOrWhiteSpace(target.ProcessName))
                {
                    activeProcessNames.Add(target.ProcessName);
                }
            }

            LogReadError(target, result.Error);
        }

        var distinctProcessNames = activeProcessNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var snapshot = new RegistryMonitorSnapshot(anyInUse, states, distinctProcessNames);
        var fingerprint = BuildFingerprint(snapshot);
        if (string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _lastFingerprint = fingerprint;
        IsInUse = snapshot.InUse;
        PathStates = snapshot.PathStates;
        ActiveProcessNames = snapshot.ActiveProcessNames;
        StateChanged?.Invoke(snapshot);
    }

    private void LogReadError(RegistryWatchTarget target, Exception? error)
    {
        if (_logger is null)
        {
            return;
        }

        var errorMessage = error?.Message;
        _lastLoggedErrors.TryGetValue(target.ConfiguredPath, out var lastErrorMessage);
        if (string.Equals(errorMessage, lastErrorMessage, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedErrors[target.ConfiguredPath] = errorMessage;
        if (error is not null)
        {
            _logger.LogWarning(error, "读取注册表摄像头状态失败：{Path}", target.ConfiguredPath);
        }
    }

    private static RegistryPathState CreateInitialState(RegistryWatchTarget target)
    {
        return target.SubKey is null
            ? new RegistryPathState(
                target.ConfiguredPath,
                null,
                false,
                true,
                $"路径无效：{target.PathError}")
            : new RegistryPathState(
                target.ConfiguredPath,
                target.ProcessName,
                false,
                false,
                "等待读取");
    }

    private static string BuildFingerprint(RegistryMonitorSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.Append(snapshot.InUse ? '1' : '0');

        foreach (var state in snapshot.PathStates)
        {
            builder.Append('\u001e')
                .Append(state.ConfiguredPath)
                .Append('\u001f')
                .Append(state.InUse ? '1' : '0')
                .Append('\u001f')
                .Append(state.HasError ? '1' : '0')
                .Append('\u001f')
                .Append(state.Status);
        }

        foreach (var processName in snapshot.ActiveProcessNames)
        {
            builder.Append('\u001d').Append(processName);
        }

        return builder.ToString();
    }

    private sealed record RegistryWatchTarget(
        string ConfiguredPath,
        string? SubKey,
        string? ProcessName,
        string? PathError);
}

internal sealed record RegistryPathState(
    string ConfiguredPath,
    string? ProcessName,
    bool InUse,
    bool HasError,
    string Status);

internal sealed record RegistryMonitorSnapshot(
    bool InUse,
    IReadOnlyList<RegistryPathState> PathStates,
    IReadOnlyList<string> ActiveProcessNames);
