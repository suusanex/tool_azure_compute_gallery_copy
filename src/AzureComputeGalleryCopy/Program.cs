// Entry point for Azure Compute Gallery Copy CLI tool
// エントリーポイント: Azure Compute Gallery クロスサブスクリプション コピー ツール

using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AzureComputeGalleryCopy.Configuration;
using AzureComputeGalleryCopy.Logging;
using AzureComputeGalleryCopy.Services.Authentication;
using AzureComputeGalleryCopy.Services.Filtering;
using AzureComputeGalleryCopy.Validation;
using AzureComputeGalleryCopy.Services.Gallery;
using AzureComputeGalleryCopy.Cli;
using AzureComputeGalleryCopy.Cli.Output;
using Azure.Identity;

namespace AzureComputeGalleryCopy;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // 設定を読み込み
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("ACG_COPY_");

            var configuration = configBuilder.Build();

            // 設定ローダーで設定を取得
            var configLoader = new ConfigurationLoader(configuration);
            var toolConfig = await configLoader.LoadAsync();

            // バリデーション
            var validator = new ConfigurationValidator();
            var validationErrors = await validator.ValidateAsync(toolConfig);
            if (validationErrors.Count > 0)
            {
                Console.Error.WriteLine("[ERROR] Configuration validation failed");
                foreach (var error in validationErrors)
                {
                    Console.Error.WriteLine($"  {error}");
                }
                return 3; // 設定エラー
            }

            // ロギングレベルを解析
            var logLevel = LogLevelExtensions.ParseLogLevel(toolConfig.LogLevel);

            // ロガーファクトリを構築
            var loggerFactoryBuilder = new LoggerFactoryBuilder();
            var loggerFactory = loggerFactoryBuilder.Build(logLevel);
            var logger = loggerFactory.CreateLogger<Program>();

            // DIコンテナを構築
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton(toolConfig);
            services.AddSingleton<IConfigurationLoader>(new ConfigurationLoader(configuration));
            services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
            services.AddSingleton<IAuthenticator>(_ => 
                new WebView2Authenticator(toolConfig.Authentication));
            services.AddSingleton<ILoggerFactoryBuilder, LoggerFactoryBuilder>();
            
            // T025: Filter matcher service
            services.AddSingleton<IFilterMatcher, FilterMatcher>();
            
            // T020, T021: Gallery services DI登録
            services.AddSingleton<IGalleryClientFactory, GalleryClientFactory>();
            services.AddSingleton<IGalleryQueryService, GalleryQueryService>();
            services.AddSingleton<IGalleryCopyService, GalleryCopyService>();
            services.AddSingleton<CopyCommand>();
            services.AddSingleton<SummaryPrinter>();
            services.AddSingleton<DryRunPrinter>();

            var serviceProvider = services.BuildServiceProvider();

            logger.LogInformation("Azure Compute Gallery Copy Tool started");

            // T020: CLI root command setup
            var rootCommand = new RootCommand("Azure Compute Gallery Cross-Subscription Copy Tool");
            
            // Global options
            var configOption = new Option<string?>("--config", "-c")
            {
                Description = "Path to configuration file (default: appsettings.json)",
                Required = false
            };
            
            var logLevelOption = new Option<string?>("--log-level", "-l")
            {
                Description = "Log level (Trace, Debug, Information, Warning, Error, Critical)",
                Required = false
            };

            rootCommand.Add(configOption);
            rootCommand.Add(logLevelOption);

            // T019: Register copy command
            var copyCommandHandler = serviceProvider.GetRequiredService<CopyCommand>();
            rootCommand.Add(copyCommandHandler.CreateCommand());

            logger.LogInformation("CLI root command initialized");

          // Execute the command (System.CommandLine beta pattern: Parse -> InvokeAsync)
          return await rootCommand.Parse(args).InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] Unexpected error: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
