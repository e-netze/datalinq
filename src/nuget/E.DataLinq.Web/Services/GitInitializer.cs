using E.DataLinq.Web.Services.Abstraction;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace E.DataLinq.Web.Services
{
    public class GitInitializer : IHostedService
    {
        private readonly ILogger<GitInitializer> _logger;
        private readonly DataLinqVersionControlOptions _codeOptions;
        private readonly IServiceScopeFactory _serviceScopeFactory;


        public GitInitializer(
            ILogger<GitInitializer> logger,
            IOptions<DataLinqVersionControlOptions> codeOptions,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _codeOptions = codeOptions.Value;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_codeOptions.UseVersionControl) return;
            if (!_codeOptions.AutoCreateRepository) return;
            if (Repository.IsValid(_codeOptions.LocalRepositoryPath)) return;

            _logger.LogInformation("Initializing DataLinq GIT");

            Directory.CreateDirectory(_codeOptions.LocalRepositoryPath);

            _logger.LogInformation($"Initializing repository in: {_codeOptions.LocalRepositoryPath}");
            string repoPath = Repository.Init(_codeOptions.LocalRepositoryPath);

            using (var repo = new Repository(repoPath))
            {
                string readmePath = Path.Combine(repo.Info.WorkingDirectory, "README.md");
                File.WriteAllText(readmePath, "# DataLinq Version Control\n\nadd additional information here");
                _logger.LogInformation("Created README.md");

                Commands.Stage(repo, "*");
                _logger.LogInformation("Staged files");

                var signature = new Signature("DataLinq GitInitializer", _codeOptions.DefaultAuthorEmail, DateTimeOffset.Now);
                repo.Commit("Initial commit", signature, signature);
                _logger.LogInformation("Created initial commit");

                var currentBranch = repo.Head;
                if (currentBranch.FriendlyName != "main")
                {
                    repo.Branches.Rename(currentBranch, "main");
                    _logger.LogInformation("Renamed branch to 'main'");
                }

                repo.Network.Remotes.Add("origin", _codeOptions.RemoteUrl);
                _logger.LogInformation($"Added remote 'origin': {_codeOptions.RemoteUrl}");

                var mainBranch = repo.Branches["main"];
                repo.Branches.Update(mainBranch,
                    b => b.Remote = "origin",
                    b => b.UpstreamBranch = "refs/heads/main");
                _logger.LogInformation("Set upstream tracking branch");

                _logger.LogInformation("\nRepository ready! You can now commit and push.");
                _logger.LogInformation($"Current branch: {repo.Head.FriendlyName}");
                _logger.LogInformation($"Remote: {repo.Network.Remotes["origin"].Url}");
            }

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var gitService = scope.ServiceProvider.GetRequiredService<IGitService>();

                var push = await gitService.PushAsync();
                if (!push.Success)
                {
                    _logger.LogError($"Error while pushing initializing commit: {push.Error.Details}");
                    return;
                }

                _logger.LogInformation(push.Message);
                _logger.LogInformation("Auto GIT Initialization completed");
            }
                
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}