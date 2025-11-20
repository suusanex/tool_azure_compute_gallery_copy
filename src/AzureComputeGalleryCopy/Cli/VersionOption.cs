using System.CommandLine;
using System.Reflection;

namespace AzureComputeGalleryCopy.Cli;

/// <summary>
/// バージョン情報オプション
/// </summary>
public class VersionOption
{
    /// <summary>
    /// バージョンオプションを作成
    /// </summary>
    public static Option<bool> CreateOption()
    {
        var option = new Option<bool>("--version", "-v")
        {
            Description = "Show version information",
            Required = false,
            DefaultValueFactory = _ => false
        };

        return option;
    }

    /// <summary>
    /// バージョン情報を取得
    /// </summary>
    public static string GetVersionInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? version;

        return $"Azure Compute Gallery Copy Tool v{informationalVersion}";
    }

    /// <summary>
    /// バージョン情報を出力して終了
    /// </summary>
    public static void PrintAndExit()
    {
        Console.WriteLine(GetVersionInfo());
        Environment.Exit(0);
    }
}
