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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_initializeSandbox) return;

            _logger.LogInformation("Initializing DataLinq Guide sandbox...");

            var assembly = typeof(SandboxInitializer).Assembly;
            var resourceStream = assembly.GetManifestResourceStream(
                "E.DataLinq.Web.datalinq_guide.zip"
            );

            if (resourceStream is null)
            {
                return;
            }

            using var zipFile = new ZipArchive(resourceStream, ZipArchiveMode.Read);

            var sourceVersionEntry = zipFile.GetEntry("datalinq-guide/version.txt");
            var targetVersionFile = Path.Combine(_storagePath, "datalinq-guide", "version.txt");

            Version sourceVersion = ReadVersionFromEntry(sourceVersionEntry);
            Version targetVersion = ReadVersionFromFile(targetVersionFile);
            _logger.LogDebug("Source version: {SourceVersion}", sourceVersion);
            _logger.LogDebug("Target version: {TargetVersion}", targetVersion);

            if (sourceVersion <= targetVersion)
            {
                return;
            }

            var destDbFile = Path.Combine(_storagePath, "datalinq_guide.db");
            var destinationDir = Path.Combine(_storagePath, "datalinq-guide");

            if (Directory.Exists(destinationDir))
            {
                Directory.Delete(destinationDir, recursive: true);
            }
            if (File.Exists(destDbFile))
            {
                File.Delete(destDbFile);
            }

            foreach (var entry in zipFile.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var destPath = Path.Combine(_storagePath, entry.FullName);
                var destDir = Path.GetDirectoryName(destPath)!;

                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                using var entryStream = entry.Open();
                using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920);
                await entryStream.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

            var blbFilePath = Path.Combine(_storagePath, "datalinq-guide", "datalinq-guide.blb");

            if (File.Exists(blbFilePath))
            {
                string content = await File.ReadAllTextAsync(blbFilePath, cancellationToken);
                string connectionString = $"sqlite:DataSource={destDbFile.Replace("\\", "/")}";
                content = content.Replace("{{connectionstring}}", connectionString);
                await File.WriteAllTextAsync(blbFilePath, content, cancellationToken);
            }
        }

        private Version ReadVersionFromFile(string filePath)
        {
            if (File.Exists(filePath)
                && Version.TryParse(File.ReadAllText(filePath).Trim(), out var version))
            {
                return version;
            }
            return new Version(0, 0, 0);
        }

        private Version ReadVersionFromEntry(ZipArchiveEntry versionEntry)
        {
            if (versionEntry is not null)
            {
                using var reader = new StreamReader(versionEntry.Open());
                var versionText = reader.ReadToEnd().Trim();
                if (Version.TryParse(versionText, out var version))
                {
                    return version;
                }
            }
            return new Version(0, 0, 0);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}