namespace E.DataLinq.Core.Services.Persistance;

public enum EncryptionLevel
{
    None = 0,
    DefaultStaticEncryption = 1,
    RandomSaltedPasswordEncryption = 2
}
