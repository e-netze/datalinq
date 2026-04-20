using System;
using System.Collections.Generic;
using System.Text;

namespace E.DataLinq.Web.Services;

public class DataLinqVersionControlOptions
{
    public const string Key = "VersionControl";

    public bool UseVersionControl { get; set; } = false;
    public string LocalRepositoryPath { get; set; } = string.Empty;
    public string RemoteUrl { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";

    public GitCredentialType CredentialType { get; set; } = GitCredentialType.Token;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; 
    public string PersonalAccessToken { get; set; } = string.Empty;

    public string DefaultAuthorName { get; set; } = "DataLinq Bot";
    public string DefaultAuthorEmail { get; set; } = "bot@datalinq.com";

    public bool AutoCreateRepository { get; set; } = true;

    public enum GitCredentialType
    {
        None,
        Token,
        UsernamePassword
    }
}
