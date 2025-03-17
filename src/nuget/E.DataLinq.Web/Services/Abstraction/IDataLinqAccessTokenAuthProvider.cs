namespace E.DataLinq.Web.Services.Abstraction;

public interface IDataLinqAccessTokenAuthProvider
{
    void SetAccessToken(string accessToken);
    string GetAccessToken();
    void DeleteToken();
}
