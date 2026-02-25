using FlowNoteMauiApp.Core.Security;
using System.Security.Cryptography;
using System.Text;

namespace FlowNoteMauiApp.Services;

public class EncryptionService : IEncryptionService
{
    private string? _masterKey;
    private const int KeySize = 256;
    private const int BlockSize = 128;

    public byte[] Encrypt(byte[] data, string key)
    {
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.GenerateIV();
        
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        aes.Key = keyBytes;
        
        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
        
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        
        return result;
    }

    public byte[] Decrypt(byte[] encryptedData, string key)
    {
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        
        var iv = new byte[BlockSize / 8];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, iv.Length);
        aes.IV = iv;
        
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        aes.Key = keyBytes;
        
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedData, iv.Length, encryptedData.Length - iv.Length);
    }

    public string GenerateKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public void SetMasterPassword(string password)
    {
        _masterKey = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }

    public bool VerifyPassword(string password)
    {
        if (_masterKey == null) return true;
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        return hash == _masterKey;
    }
}

public class BiometricService : IBiometricService
{
    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(false);
    }

    public Task<bool> AuthenticateAsync(string reason)
    {
        return Task.FromResult(false);
    }

    public BiometricType GetAvailableBiometricType()
    {
        return BiometricType.None;
    }
}
