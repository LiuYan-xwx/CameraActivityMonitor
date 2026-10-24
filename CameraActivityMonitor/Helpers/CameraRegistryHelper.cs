using Microsoft.Win32;

namespace CameraActivityMonitor.Helpers;

internal static class CameraRegistryHelper
{
    internal const string WebcamRegistryBasePath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

    internal const string LastUsedTimeStopValueName = "LastUsedTimeStop";

    /// <summary>
    /// 将用户填写的路径规范化为 HKEY_CURRENT_USER 下的子项路径。
    /// 支持完整 HKCU 路径、HKCU\ 前缀，以及相对于 webcam 项的 NonPackaged\... / Packaged\... 路径。
    /// </summary>
    public static bool TryNormalizeKeyPath(
        string? rawPath,
        out string subKey,
        out string? processName,
        out string? error)
    {
        subKey = "";
        processName = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            error = "路径为空";
            return false;
        }

        var path = rawPath.Trim().Trim('"').Trim();
        if (path.StartsWith(@"Computer\", StringComparison.OrdinalIgnoreCase))
        {
            path = path[@"Computer\".Length..];
        }

        if (path.StartsWith(@"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase))
        {
            path = path[@"HKEY_CURRENT_USER\".Length..];
        }
        else if (path.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
        {
            path = path[@"HKCU\".Length..];
        }
        else if (IsOtherRegistryHive(path))
        {
            error = "仅支持 HKEY_CURRENT_USER 下的注册表项";
            return false;
        }
        else if (path.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase))
        {
            error = "HKEY_CURRENT_USER 路径不完整";
            return false;
        }

        if (path.StartsWith(@"webcam\", StringComparison.OrdinalIgnoreCase))
        {
            path = path[@"webcam\".Length..];
        }

        if (path.StartsWith(@"NonPackaged\", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(@"Packaged\", StringComparison.OrdinalIgnoreCase))
        {
            path = $@"{WebcamRegistryBasePath}\{path}";
        }

        path = path.Trim().TrimStart('\\').TrimEnd('\\');
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "路径为空";
            return false;
        }

        subKey = path;
        processName = GetProcessName(path);
        return true;
    }

    /// <summary>
    /// 读取指定项的 LastUsedTimeStop。0 表示正在使用，非 0 表示未使用。
    /// 读取失败不会抛出异常，而是返回带错误信息的结果。
    /// </summary>
    public static RegistryValueReadResult ReadLastUsedTimeStop(string subKey)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: false);
            if (key is null)
            {
                return new RegistryValueReadResult(false, "注册表项不存在", false, null);
            }

            var value = key.GetValue(
                LastUsedTimeStopValueName,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);

            if (value is null)
            {
                return new RegistryValueReadResult(false, "未找到 LastUsedTimeStop", false, null);
            }

            long? stopTime = value switch
            {
                long l => l,
                int i => i,
                uint ui => (long)ui,
                ulong ul when ul <= long.MaxValue => (long)ul,
                _ => null
            };

            if (stopTime is null)
            {
                return new RegistryValueReadResult(
                    false,
                    $"LastUsedTimeStop 类型异常（{value.GetType().Name}）",
                    true,
                    null);
            }

            return stopTime.Value == 0
                ? new RegistryValueReadResult(true, "正常（使用中）", false, null)
                : new RegistryValueReadResult(false, "正常（未使用）", false, null);
        }
        catch (Exception ex)
        {
            return new RegistryValueReadResult(false, "读取失败", true, ex);
        }
    }

    private static bool IsOtherRegistryHive(string path)
    {
        return path.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("HKCR", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("HKEY_USERS", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("HKU", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("HKCC", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProcessName(string subKey)
    {
        try
        {
            var leaf = subKey.Split('\\').LastOrDefault();
            if (string.IsNullOrWhiteSpace(leaf))
            {
                return null;
            }

            var executableName = leaf.Split('#').LastOrDefault() ?? leaf;
            executableName = executableName.Trim();
            if (string.IsNullOrWhiteSpace(executableName))
            {
                executableName = leaf.Trim();
            }

            if (string.IsNullOrWhiteSpace(executableName))
            {
                return null;
            }

            if (executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                executableName = executableName[..^4].Trim();
                var lastSeparator = executableName.LastIndexOfAny(new[] { '\\', '/' });
                if (lastSeparator >= 0)
                {
                    executableName = executableName[(lastSeparator + 1)..].Trim();
                }
            }

            return string.IsNullOrWhiteSpace(executableName) ? null : executableName;
        }
        catch
        {
            return null;
        }
    }
}

internal readonly record struct RegistryValueReadResult(
    bool InUse,
    string Status,
    bool HasError,
    Exception? Error);
