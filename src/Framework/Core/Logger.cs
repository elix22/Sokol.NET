using System;

namespace GameEditor.Framework.Core
{
    public enum LogLevel { Info, Warning, Error }

    public static class Logger
    {
        public static event Action<LogLevel, string>? OnLog;

        public static void Info(string msg)    => OnLog?.Invoke(LogLevel.Info, msg);
        public static void Warning(string msg) => OnLog?.Invoke(LogLevel.Warning, msg);
        public static void Error(string msg)   => OnLog?.Invoke(LogLevel.Error, msg);
    }
}
