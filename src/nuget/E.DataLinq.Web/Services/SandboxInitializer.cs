using E.DataLinq.Core.Services.Persistance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services
{
    public class SandboxInitializer : IHostedService
    {
        private readonly ILogger<SandboxInitializer> _logger;
        private readonly bool _initializeSandbox;
        private readonly string _storagePath;

        public SandboxInitializer(
            ILogger<SandboxInitializer> logger,
            IWebHostEnvironment env,
            IOptions<DataLinqCodeApiOptions> codeOptions,
            IOptions<PersistanceProviderServiceOptions> persistanceOptions)
        {
            _logger = logger;
            _initializeSandbox = codeOptions.Value.InitializeSandboxOnStartup;
            _storagePath = persistanceOptions.Value.ConnectionString;
        }

        async public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_initializeSandbox) return; 

            _logger.LogInformation("Initializing DataLinq Guide sandbox...");

            var zipData = E.DataLinq.Web.Properties.Resources.datalinq_guide;
            var zipFile = new ZipArchive(new MemoryStream(zipData));

            var sourceVersionFile = zipFile.GetEntry("datalinq-guide/version.txt");
            var targetVersionFile = Path.Combine(_storagePath, "datalinq-guide", "version.txt");

            Version sourceVersion = ReadVersionOrDefault(sourceVersionFile);
            Version targetVersion = ReadVersionOrDefault(targetVersionFile);
            _logger.LogDebug("Source version: {SourceVersion}", sourceVersion);
            _logger.LogDebug("Target version: {TargetVersion}", targetVersion);

            if (sourceVersion <= targetVersion)
            {
                _logger.LogInformation("DataLinq Guide sandbox is up to date. No action needed.");
                return;
            }

            var destDbFile = Path.Combine(_storagePath, "datalinq_guide.db");
            var destinationDir = Path.Combine(_storagePath, "datalinq-guide");

            if (Directory.Exists(destinationDir))
            {
                _logger.LogDebug("Deleting existing DataLinq Guide directory at {DestinationDir}", destinationDir);
                Directory.Delete(destinationDir, recursive: true);
            }
            if (File.Exists(destDbFile))
            {
                _logger.LogDebug("Deleting existing DataLinq Guide database file at {DestDbFile}", destDbFile);
                File.Delete(destDbFile);
            }

            _logger.LogInformation("Extracting DataLinq Guide sandbox to {StoragePath}", _storagePath);
            await zipFile.ExtractToDirectoryAsync(_storagePath);

            var blbFilePath = Path.Combine(_storagePath, "datalinq-guide", "datalinq-guide.blb");

            if (File.Exists(blbFilePath))
            {
                string content = File.ReadAllText(blbFilePath);
                string connectionString = $"sqlite:DataSource={destDbFile.Replace("\\", "/")}";
                content = content.Replace("{{connectionstring}}", connectionString);

                _logger.LogDebug("Updating connection string in {BlbFilePath} to {ConnectionString}", blbFilePath, connectionString);

                await File.WriteAllTextAsync(blbFilePath, content);
            }

            return;
        }

        private Version ReadVersionOrDefault(string filePath)
        {
            if (File.Exists(filePath) && Version.TryParse(File.ReadAllText(filePath).Trim(), out var version))
                return version;
            return new Version(0, 0, 0);
        }

        private Version ReadVersionOrDefault(ZipArchiveEntry versionEntry)
        {
            if (versionEntry != null)
            {
                using (var reader = new StreamReader(versionEntry.Open()))
                {
                    var versionText = reader.ReadToEnd().Trim();
                    if (Version.TryParse(versionText, out var version))
                    {
                        return version;
                    }
                }
            }
            return new Version(0, 0, 0);
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}