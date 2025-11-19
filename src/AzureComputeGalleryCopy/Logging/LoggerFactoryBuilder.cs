using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace AzureComputeGalleryCopy.Logging;

/// <summary>
/// ロガーファクトリを構築するためのビルダー
/// </summary>
public interface ILoggerFactoryBuilder
{
    /// <summary>
    /// ロガーファクトリを構築します。
    /// </summary>
    /// <param name="minimumLevel">最小ログレベル</param>
    /// <param name="useConsole">コンソール出力を使用するか</param>
    /// <returns>構築されたロガーファクトリ</returns>
    ILoggerFactory Build(LogLevel minimumLevel, bool useConsole = true);
}

/// <inheritdoc />
public class LoggerFactoryBuilder : ILoggerFactoryBuilder
{
    /// <inheritdoc />
    public ILoggerFactory Build(LogLevel minimumLevel, bool useConsole = true)
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);

            if (useConsole)
            {
                // 非推奨オプション (ConsoleLoggerOptions) を避けるため SimpleConsoleFormatter を使用
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.UseUtcTimestamp = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
                });
            }
        });

        return factory;
    }
}

/// <summary>
/// ログレベルの文字列表現を解析します。
/// </summary>
public static class LogLevelExtensions
{
    /// <summary>
    /// 文字列からLogLevelに変換します。
    /// </summary>
    /// <param name="logLevelString">ログレベルの文字列表現（"Trace", "Debug", "Information", "Warning", "Error", "Critical"）</param>
    /// <returns>対応するLogLevel。不正な値の場合はInformation</returns>
    public static LogLevel ParseLogLevel(string logLevelString)
    {
        if (string.IsNullOrEmpty(logLevelString))
        {
            return LogLevel.Information;
        }

        return logLevelString.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }
}
