using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using FluentMigrator.Runner.Processors.Postgres;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using NLog;
using Npgsql;
using NzbDrone.Common.Composition.Extensions;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Exceptions;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore.Extensions;
using NzbDrone.Host;
using PostgresOptions = NzbDrone.Core.Datastore.PostgresOptions;

namespace NzbDrone.Host
{
    public static class Bootstrap
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(Bootstrap));

        public static readonly List<string> ASSEMBLIES = new List<string>
        {
            "Prowlarr.Host",
            "Prowlarr.Core",
            "Prowlarr.SignalR",
            "Prowlarr.Api.V1",
            "Prowlarr.Http"
        };

        public static void Start(string[] args, Action<IHostBuilder> trayCallback = null)
        {
            try
            {
                Logger.Info("Starting Prowlarr - {0} - Version {1}",
                            Environment.ProcessPath,
                            Assembly.GetExecutingAssembly().GetName().Version);

                var startupContext = new StartupContext(args);

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                var appMode = GetApplicationMode(startupContext);
                var config = GetConfiguration(startupContext);

                switch (appMode)
                {
                    case ApplicationModes.Service:
                    {
                        Logger.Debug("Service selected");

                        CreateConsoleHostBuilder(args, startupContext).UseWindowsService().Build().Run();
                        break;
                    }

                    case ApplicationModes.Interactive:
                    {
                        Logger.Debug(trayCallback != null ? "Tray selected" : "Console selected");
                        var builder = CreateConsoleHostBuilder(args, startupContext);

                        if (trayCallback != null)
                        {
                            trayCallback(builder);
                        }

                        builder.Build().Run();
                        break;
                    }

                    // Utility mode
                    default:
                    {
                        new HostBuilder()
                            .UseServiceProviderFactory(new DryIocServiceProviderFactory(new Container(rules => rules.WithNzbDroneRules())))
                            .ConfigureContainer<IContainer>(c =>
                            {
                                c.AutoAddServices(Bootstrap.ASSEMBLIES)
                                    .AddNzbDroneLogger()
                                    .AddDatabase()
                                    .AddStartupContext(startupContext)
                                    .Resolve<UtilityModeRouter>()
                                    .Route(appMode);
                            })
                            .ConfigureServices(services =>
                            {
                                services.Configure<PostgresOptions>(config.GetSection("Prowlarr:Postgres"));
                            }).Build();

                        break;
                    }
                }
            }
            catch (InvalidConfigFileException ex)
            {
                throw new ProwlarrStartupException(ex);
            }
            catch (TerminateApplicationException e)
            {
                Logger.Info(e.Message);
                LogManager.Configuration = null;
            }

            // Make sure there are no lingering database connections
            GC.Collect();
            GC.WaitForPendingFinalizers();
            SQLiteConnection.ClearAllPools();
            NpgsqlConnection.ClearAllPools();
        }

        public static IHostBuilder CreateConsoleHostBuilder(string[] args, StartupContext context)
        {
            var config = GetConfiguration(context);

            var bindAddress = config.GetValue(nameof(ConfigFileProvider.BindAddress), "*");
            var port = config.GetValue(nameof(ConfigFileProvider.Port), ConfigFileProvider.DEFAULT_PORT);
            var sslPort = config.GetValue(nameof(ConfigFileProvider.SslPort), ConfigFileProvider.DEFAULT_SSL_PORT);
            var enableSsl = config.GetValue(nameof(ConfigFileProvider.EnableSsl), false);
            var sslCertPath = config.GetValue<string>(nameof(ConfigFileProvider.SslCertPath));
            var sslCertPassword = config.GetValue<string>(nameof(ConfigFileProvider.SslCertPassword));

            var urls = new List<string> { BuildUrl("http", bindAddress, port) };

            if (enableSsl && sslCertPath.IsNotNullOrWhiteSpace())
            {
                urls.Add(BuildUrl("https", bindAddress, sslPort));
            }

            return new HostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseServiceProviderFactory(new DryIocServiceProviderFactory(new Container(rules => rules.WithNzbDroneRules())))
                .ConfigureContainer<IContainer>(c =>
                {
                    c.AutoAddServices(Bootstrap.ASSEMBLIES)
                        .AddNzbDroneLogger()
                        .AddDatabase()
                        .AddStartupContext(context);
                })
                .ConfigureServices(services =>
                {
                    services.Configure<PostgresOptions>(config.GetSection("Prowlarr:Postgres"));
                    services.Configure<FormOptions>(x =>
                    {
                        //Double the default multipart body length from 128 MB to 256 MB
                        x.MultipartBodyLengthLimit = 268435456;
                    });
                })
                .ConfigureWebHost(builder =>
                {
                    builder.UseConfiguration(config);
                    builder.UseUrls(urls.ToArray());
                    builder.UseKestrel(options =>
                    {
                        if (enableSsl && sslCertPath.IsNotNullOrWhiteSpace())
                        {
                            options.ConfigureHttpsDefaults(configureOptions =>
                            {
                                configureOptions.ServerCertificate = ValidateSslCertificate(sslCertPath, sslCertPassword);
                            });
                        }
                    });
                    builder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.AllowSynchronousIO = false;
                        serverOptions.Limits.MaxRequestBodySize = null;
                    });
                    builder.UseStartup<Startup>();
                });
        }

        public static ApplicationModes GetApplicationMode(IStartupContext startupContext)
        {
            if (startupContext.Help)
            {
                return ApplicationModes.Help;
            }

            if (OsInfo.IsWindows && startupContext.RegisterUrl)
            {
                return ApplicationModes.RegisterUrl;
            }

            if (OsInfo.IsWindows && startupContext.InstallService)
            {
                return ApplicationModes.InstallService;
            }

            if (OsInfo.IsWindows && startupContext.UninstallService)
            {
                return ApplicationModes.UninstallService;
            }

            // IsWindowsService can throw sometimes, so wrap it
            var isWindowsService = false;
            try
            {
                isWindowsService = WindowsServiceHelpers.IsWindowsService();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get service status");
            }

            if (OsInfo.IsWindows && isWindowsService)
            {
                return ApplicationModes.Service;
            }

            return ApplicationModes.Interactive;
        }

        private static IConfiguration GetConfiguration(StartupContext context)
        {
            var appFolder = new AppFolderInfo(context);
            var configPath = appFolder.GetConfigPath();

            try
            {
                return new ConfigurationBuilder()
                    .AddXmlFile(configPath, optional: true, reloadOnChange: false)
                    .AddInMemoryCollection(new List<KeyValuePair<string, string>> { new ("dataProtectionFolder", appFolder.GetDataProtectionPath()) })
                    .AddEnvironmentVariables()
                    .Build();
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidConfigFileException($"{configPath} is corrupt or invalid. Please delete the config file and Prowlarr will recreate it.", ex);
            }
        }

        private static string BuildUrl(string scheme, string bindAddress, int port)
        {
            return $"{scheme}://{bindAddress}:{port}";
        }

        private static X509Certificate2 ValidateSslCertificate(string cert, string password)
        {
            X509Certificate2 certificate;

            try
            {
                certificate = new X509Certificate2(cert, password, X509KeyStorageFlags.DefaultKeySet);
            }
            catch (CryptographicException ex)
            {
                if (ex.HResult == 0x2 || ex.HResult == 0x2006D080)
                {
                    throw new ProwlarrStartupException(ex,
                        $"The SSL certificate file {cert} does not exist");
                }

                throw new ProwlarrStartupException(ex);
            }

            return certificate;
        }
    }
}
