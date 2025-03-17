namespace E.DataLinq.Core.Services.Crypto.Abstraction;

public interface ICryptoService
{
    string EncryptTextDefault(string str);
    string EncryptTextDefault(string str, CryptoResultStringType resultType);
    string DecryptTextDefault(string str);

    string StaticEncryptDefault(string str, string password = "");
    string StaticDecryptDefault(string str, string password = "");
}
