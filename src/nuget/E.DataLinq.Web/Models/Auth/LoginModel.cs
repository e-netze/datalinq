namespace E.DataLinq.Web.Models.Auth;

public class LoginModel
{
    public string Name { get; set; }
    public string Password { get; set; }

    public string Redirect { get; set; }

    public string ErrorMessage { get; set; }
}
