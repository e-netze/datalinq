namespace E.DataLinq.Core.Services.Persistance;

public class PersistanceProviderServiceOptions
{
    public string ConnectionString { get; set; }

    public EncryptionLevel SecureStringEncryptionLevel { get; set; } = EncryptionLevel.DefaultStaticEncryption;
}
