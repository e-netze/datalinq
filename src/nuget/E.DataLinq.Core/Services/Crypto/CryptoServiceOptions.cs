namespace E.DataLinq.Core.Services.Crypto;

public enum CryptoStrength
{
    AES128 = 1,
    AES192 = 2,
    AES256 = 3
}

public enum CryptoResultStringType
{
    Base64 = 0,
    Hex = 1
}

public class CryptoServiceOptions
{
    public CryptoServiceOptions()
    {
        this.UseRandomSalt = true;
        this.Strength = CryptoStrength.AES128;
        this.DefaultResultType = CryptoResultStringType.Base64;
        this.Saltsize = 4;
    }

    public string DefaultPassword { get; set; }
    public int Saltsize { get; set; }
    public byte[] HashBytesSalt { get; set; }

    public CryptoStrength Strength { get; set; }

    public CryptoResultStringType DefaultResultType { get; set; }

    public bool UseRandomSalt { get; set; }

    public CryptoServiceOptions[] LegacyOptions { get; set; }
}
