namespace FlowNoteMauiApp.Core.Security;

public interface IEncryptionService
{
    byte[] Encrypt(byte[] data, string key);
    byte[] Decrypt(byte[] encryptedData, string key);
    string GenerateKey();
    void SetMasterPassword(string password);
    bool VerifyPassword(string password);
}

public interface IBiometricService
{
    Task<bool> IsAvailableAsync();
    Task<bool> AuthenticateAsync(string reason);
    BiometricType GetAvailableBiometricType();
}

public enum BiometricType
{
    None,
    Fingerprint,
    Face,
    Iris
}
