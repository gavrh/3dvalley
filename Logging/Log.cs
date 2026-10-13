using StardewModdingAPI;

namespace ThreeDValley;

internal static class Log {
    private static IMonitor? monitor;

    internal static void Init(IMonitor monitor) {
        Log.monitor = monitor;
    }

    internal static void Trace(string message) => Log.monitor?.Log(message, LogLevel.Trace);

    internal static void Debug(string message) => Log.monitor?.Log(message, LogLevel.Debug);

    internal static void Info(string message) => Log.monitor?.Log(message, LogLevel.Info);

    internal static void Warn(string message) => Log.monitor?.Log(message, LogLevel.Warn);

    internal static void Error(string message) => Log.monitor?.Log(message, LogLevel.Error);
}
