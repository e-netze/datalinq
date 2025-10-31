using E.DataLinq.Core.Services.Persistance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services
{
    public class SandboxInitializer : IHostedService
    {
        //private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly string _storagePath;

        public SandboxInitializer(
            //IConfiguration config, 
            IWebHostEnvironment env,
            IOptions<PersistanceProviderServiceOptions> options)
        {
            //_config = config;
            _env = env;
            _storagePath = options.Value.ConnectionString;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var sourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", "datalinq-guide");

            var sourceVersionFile = Path.Combine(sourcePath, "version.txt");
            var targetVersionFile = Path.Combine(_storagePath, "datalinq-guide", "version.txt");

            Version sourceVersion = ReadVersionOrDefault(sourceVersionFile);
            Version targetVersion = ReadVersionOrDefault(targetVersionFile);

            if (sourceVersion > targetVersion)
            {
                var destinationDir = Path.Combine(_storagePath, "datalinq-guide");
                if (Directory.Exists(destinationDir))
                    Directory.Delete(destinationDir, recursive: true);

                CopyDirectory(sourcePath, destinationDir);
            }

            var sourceDbFile = Path.Combine(AppContext.BaseDirectory, "Resources", "datalinq_guide.db");
            var destDbFile = Path.Combine(_storagePath, "datalinq_guide.db");

            if (File.Exists(sourceDbFile))
            {
                Directory.CreateDirectory(_storagePath);
                File.Copy(sourceDbFile, destDbFile, overwrite: true);
            }

            var blbFileName = "datalinq-guide\\datalinq-guide.blb";
            var blbFilePath = Path.Combine(_storagePath, blbFileName);

            if (File.Exists(blbFilePath))
            {
                string content = File.ReadAllText(blbFilePath);
                string connectionString = $"DataSource={destDbFile.Replace("\\", "/")}";
                content = content.Replace("placeholder", connectionString);
                File.WriteAllText(blbFilePath, content);
            }

            return Task.CompletedTask;
        }

        private Version ReadVersionOrDefault(string filePath)
        {
            if (File.Exists(filePath) && Version.TryParse(File.ReadAllText(filePath).Trim(), out var version))
                return version;
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