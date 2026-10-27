namespace E.DataLinq.Web.Services.VersionControl;

/// <summary>
/// Git version control for DataLinq content (configuration section "VersionControl").
/// One shared working copy per DataLinq instance; authentication with an API/access token only.
/// </summary>
public class DataLinqVersionControlOptions
{
    public const string Key = "VersionControl";

    public bool UseVersionControl { get; set; } = false;

    /// <summary>
    /// Local working copy (readable mirror of the DataLinq storage).
    /// Must be a dedicated folder - never the DataLinq storage path.
    /// </summary>
    public string LocalRepositoryPath { get; set; } = string.Empty;

    public string RemoteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Branches can only be created from this branch and merged back into it.
    /// </summary>
    public string MainBranch { get; set; } = "main";

    /// <summary>
    /// API/access token (Gitea, GitHub, GitLab, Azure DevOps). Sent as HTTP basic auth password.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Basic auth user name sent together with the token. Most servers ignore it.
    /// </summary>
    public string AccessTokenUserName { get; set; } = "datalinq";

    /// <summary>
    /// Commit author e-mail. The author name is the logged-in DataLinq Code user.
    /// </summary>
    public string AuthorEmail { get; set; } = "datalinq@localhost";

    public bool IsConfigured =>
        UseVersionControl &&
        !string.IsNullOrWhiteSpace(LocalRepositoryPath) &&
        !string.IsNullOrWhiteSpace(RemoteUrl);
}
