using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QRCoder;
using Aetheris.Schema;

namespace Aetheris.Studio.Core;

/// <summary>
/// Manages profile storage, loading, saving, and versioning with encryption.
/// </summary>
public interface IProfileRepository
{
    Task<IReadOnlyList<ProfileSummary>> GetAllAsync(CancellationToken ct = default);
    Task<AetherisProfile?> LoadAsync(Guid profileId, CancellationToken ct = default);
    Task SaveAsync(AetherisProfile profile, CancellationToken ct = default);
    Task DeleteAsync(Guid profileId, CancellationToken ct = default);
    Task<Guid> CreateNewAsync(string name, CancellationToken ct = default);
}

public record ProfileSummary(Guid Id, string Name, DateTime UpdatedAt, int MeasurementCount, int FindingCount);

/// <summary>
/// Dialog service interface for UI interactions (implemented in UI project).
/// </summary>
public interface IDialogService
{
    Task<string?> PromptForNameAsync(string title);
    Task<bool> ConfirmAsync(string message);
    Task ShowErrorAsync(string title, string message);
}

/// <summary>
/// File picker service interface for save/load dialogs (implemented in UI project).
/// </summary>
public interface IFilePickerService
{
    Task<string?> SaveFileAsync(string suggestedFileName, string defaultExtension, IEnumerable<(string Name, string[] Extensions)> fileTypeChoices);
    Task<string?> OpenFileAsync(string defaultExtension, IEnumerable<(string Name, string[] Extensions)> fileTypeChoices);
}

/// <summary>
/// Encryption settings for profile storage.
/// </summary>
public sealed class EncryptionSettings
{
    public bool Enabled { get; set; } = true;
    public bool UseDpapi { get; set; } = true;
    public string? PasswordHint { get; set; }
    public byte[]? Salt { get; set; }
    public int Argon2Iterations { get; set; } = 3;
    public int Argon2MemoryKb { get; set; } = 65536;
    public int Argon2Parallelism { get; set; } = 4;
}

/// <summary>
/// File-system based profile repository with encryption support.
/// </summary>
public sealed class FileProfileRepository : IProfileRepository
{
    private readonly string _storagePath;
    private readonly ILogger<FileProfileRepository> _logger;
    private readonly EncryptionSettings _encryptionSettings;
    private readonly IKeyProvider _keyProvider;

    public FileProfileRepository(string storagePath, ILogger<FileProfileRepository> logger, EncryptionSettings? encryptionSettings = null, IKeyProvider? keyProvider = null)
    {
        _storagePath = storagePath;
        _logger = logger;
        _encryptionSettings = encryptionSettings ?? new EncryptionSettings();
        _keyProvider = keyProvider ?? new DpapiKeyProvider();
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<IReadOnlyList<ProfileSummary>> GetAllAsync(CancellationToken ct = default)
    {
        var summaries = new List<ProfileSummary>();
        var files = Directory.GetFiles(_storagePath, "*.json");

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var profile = await LoadAndDecryptAsync(file, ct);
                summaries.Add(new ProfileSummary(
                    profile.Profile.Id,
                    profile.Profile.Name,
                    profile.Profile.UpdatedAt,
                    profile.Measurements.Count,
                    profile.Findings.Count
                ));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load profile from {File}", file);
            }
        }

        return summaries.OrderByDescending(s => s.UpdatedAt).ToList();
    }

    public async Task<AetherisProfile?> LoadAsync(Guid profileId, CancellationToken ct = default)
    {
        var file = GetProfilePath(profileId);
        if (!File.Exists(file)) return null;

        try
        {
            return await LoadAndDecryptAsync(file, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile {ProfileId}", profileId);
            throw;
        }
    }

    private async Task<AetherisProfile> LoadAndDecryptAsync(string file, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(file, ct);
        var encryptedProfile = JsonSerializer.Deserialize<EncryptedProfileContainer>(json, JsonOptions.Default);

        if (encryptedProfile == null)
            throw new InvalidDataException("Invalid profile file format");

        if (encryptedProfile.IsEncrypted && _encryptionSettings.Enabled)
        {
            var key = await _keyProvider.GetKeyAsync(_encryptionSettings, ct);
            var decryptedJson = DecryptString(encryptedProfile.EncryptedData!, key);
            return JsonSerializer.Deserialize<AetherisProfile>(decryptedJson, JsonOptions.Default)
                ?? throw new InvalidDataException("Failed to deserialize decrypted profile");
        }

        return JsonSerializer.Deserialize<AetherisProfile>(json, JsonOptions.Default)
            ?? throw new InvalidDataException("Failed to deserialize profile");
    }

    public async Task SaveAsync(AetherisProfile profile, CancellationToken ct = default)
    {
        var validation = ProfileValidator.Validate(profile);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Profile validation failed: {string.Join("; ", validation.Errors)}");
        }

        profile.Profile.UpdatedAt = DateTime.UtcNow;
        var file = GetProfilePath(profile.Profile.Id);
        var tempFile = file + ".tmp";

        try
        {
            string json;

            if (_encryptionSettings.Enabled)
            {
                var key = await _keyProvider.GetKeyAsync(_encryptionSettings, ct);
                var profileJson = JsonSerializer.Serialize(profile, JsonOptions.Default);
                var encryptedData = EncryptString(profileJson, key);

                var container = new EncryptedProfileContainer
                {
                    IsEncrypted = true,
                    EncryptedData = encryptedData,
                    Version = 1,
                    EncryptedAt = DateTime.UtcNow
                };
                json = JsonSerializer.Serialize(container, JsonOptions.Default);
            }
            else
            {
                json = JsonSerializer.Serialize(profile, JsonOptions.Default);
            }

            await File.WriteAllTextAsync(tempFile, json, ct);
            File.Move(tempFile, file, overwrite: true);
            _logger.LogInformation("Saved profile {ProfileId} ({Name})", profile.Profile.Id, profile.Profile.Name);
        }
        catch
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            throw;
        }
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken ct = default)
    {
        var file = GetProfilePath(profileId);
        if (File.Exists(file))
        {
            File.Delete(file);
            _logger.LogInformation("Deleted profile {ProfileId}", profileId);
        }
    }

    public async Task<Guid> CreateNewAsync(string name, CancellationToken ct = default)
    {
        var profile = new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            Measurements = new List<Measurement>(),
            Findings = new List<Finding>(),
            Settings = new Settings { Version = 1 }
        };

        await SaveAsync(profile, ct);
        return profile.Profile.Id;
    }

    private string GetProfilePath(Guid profileId) => Path.Combine(_storagePath, $"{profileId}.json");

    private string EncryptString(string plaintext, byte[] key)
    {
        using var aes = new AesGcm(key, 16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Combine nonce + ciphertext + tag
        var result = new byte[nonce.Length + cipherBytes.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + cipherBytes.Length, tag.Length);

        return Convert.ToBase64String(result);
    }

    private string DecryptString(string base64Ciphertext, byte[] key)
    {
        var data = Convert.FromBase64String(base64Ciphertext);
        if (data.Length < 12 + 16)
            throw new InvalidDataException("Invalid encrypted data format");

        using var aes = new AesGcm(key, 16);
        var nonce = data[..12];
        var tag = data[^16..];
        var ciphertext = data[12..^16];
        var plainBytes = new byte[ciphertext.Length];

        aes.Decrypt(nonce, ciphertext, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}

/// <summary>
/// Container for encrypted profile storage.
/// </summary>
public sealed class EncryptedProfileContainer
{
    public bool IsEncrypted { get; set; }
    public string? EncryptedData { get; set; }
    public int Version { get; set; } = 1;
    public DateTime EncryptedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Interface for key providers (DPAPI, password-based, etc.)
/// </summary>
public interface IKeyProvider
{
    Task<byte[]> GetKeyAsync(EncryptionSettings settings, CancellationToken ct = default);
}

/// <summary>
/// DPAPI-based key provider (Windows Data Protection API).
/// </summary>
public sealed class DpapiKeyProvider : IKeyProvider
{
    private static readonly byte[] Entropy = "Aetheris.Studio.ProfileKey"u8.ToArray();

    public Task<byte[]> GetKeyAsync(EncryptionSettings settings, CancellationToken ct = default)
    {
        // Generate a machine/user-specific key using DPAPI
        var masterKey = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(masterKey);
        }

        // Protect with DPAPI (user scope)
        var protectedKey = ProtectedData.Protect(masterKey, Entropy, DataProtectionScope.CurrentUser);

        // Derive final encryption key
        using var sha256 = SHA256.Create();
        return Task.FromResult(sha256.ComputeHash(protectedKey));
    }
}

/// <summary>
/// Password-based key provider using PBKDF2.
/// </summary>
public sealed class PasswordKeyProvider : IKeyProvider
{
    private readonly string _password;

    public PasswordKeyProvider(string password)
    {
        _password = password;
    }

    public Task<byte[]> GetKeyAsync(EncryptionSettings settings, CancellationToken ct = default)
    {
        var salt = settings.Salt ?? new byte[16];
        if (settings.Salt == null)
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            settings.Salt = salt;
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(_password, salt, settings.Argon2Iterations * 10000, HashAlgorithmName.SHA256);
        return Task.FromResult(pbkdf2.GetBytes(32));
    }
}

/// <summary>
/// Key provider factory for creating appropriate key provider.
/// </summary>
public static class KeyProviderFactory
{
    public static IKeyProvider Create(EncryptionSettings settings, string? password = null)
    {
        if (!string.IsNullOrEmpty(password))
            return new PasswordKeyProvider(password);

        if (settings.UseDpapi)
            return new DpapiKeyProvider();

        throw new InvalidOperationException("No key provider available");
    }
}

/// <summary>
/// Service for generating QR codes from profiles (supports chunked QR for large data).
/// </summary>
public interface IQrCodeService
{
    Task<byte[]> GeneratePngAsync(AetherisProfile profile, int pixelsPerModule = 4, CancellationToken ct = default);
    Task<byte[]> GenerateSvgAsync(AetherisProfile profile, CancellationToken ct = default);
    Task<IReadOnlyList<byte[]>> GenerateChunkedPngAsync(AetherisProfile profile, int maxBytesPerChunk = 2500, int pixelsPerModule = 4, CancellationToken ct = default);
}

public sealed class QrCodeService : IQrCodeService
{
    private readonly ILogger<QrCodeService> _logger;

    public QrCodeService(ILogger<QrCodeService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GeneratePngAsync(AetherisProfile profile, int pixelsPerModule = 4, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var transferProfile = ProfileIO.PrepareForTransfer(profile);
            var json = JsonSerializer.Serialize(transferProfile, JsonOptions.Default);

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(json, QRCodeGenerator.ECCLevel.M);
            var qrCode = new PngByteQRCode(qrData);
            var pngBytes = qrCode.GetGraphic(pixelsPerModule);

            _logger.LogDebug("Generated QR code for profile {ProfileId} ({Length} bytes)", profile.Profile.Id, pngBytes.Length);
            return pngBytes;
        }, ct);
    }

    public async Task<byte[]> GenerateSvgAsync(AetherisProfile profile, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var transferProfile = ProfileIO.PrepareForTransfer(profile);
            var json = JsonSerializer.Serialize(transferProfile, JsonOptions.Default);

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(json, QRCodeGenerator.ECCLevel.M);
            var qrCode = new SvgQRCode(qrData);
            var svg = qrCode.GetGraphic(4);

            return Encoding.UTF8.GetBytes(svg);
        }, ct);
    }

    public async Task<IReadOnlyList<byte[]>> GenerateChunkedPngAsync(AetherisProfile profile, int maxBytesPerChunk = 2500, int pixelsPerModule = 4, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var transferProfile = ProfileIO.PrepareForTransfer(profile);
            var json = JsonSerializer.Serialize(transferProfile, JsonOptions.Default);
            var bytes = Encoding.UTF8.GetBytes(json);

            if (bytes.Length <= maxBytesPerChunk)
            {
                return new List<byte[]> { GenerateSinglePng(bytes, pixelsPerModule) };
            }

            // Split into chunks with sequence header
            var chunks = new List<byte[]>();
            var totalChunks = (int)Math.Ceiling((double)bytes.Length / maxBytesPerChunk);

            for (int i = 0; i < totalChunks; i++)
            {
                var start = i * maxBytesPerChunk;
                var length = Math.Min(maxBytesPerChunk, bytes.Length - start);
                var chunkData = bytes[start..(start + length)];

                // Create chunk header: [magic:4][index:2][total:2][crc32:4]
                var header = new byte[12];
                Encoding.ASCII.GetBytes("AETH").CopyTo(header, 0);
                BitConverter.GetBytes((ushort)i).CopyTo(header, 4);
                BitConverter.GetBytes((ushort)totalChunks).CopyTo(header, 6);
                var crc = Crc32(chunkData);
                BitConverter.GetBytes(crc).CopyTo(header, 8);

                var chunkWithHeader = new byte[header.Length + chunkData.Length];
                Buffer.BlockCopy(header, 0, chunkWithHeader, 0, header.Length);
                Buffer.BlockCopy(chunkData, 0, chunkWithHeader, header.Length, chunkData.Length);

                chunks.Add(GenerateSinglePng(chunkWithHeader, pixelsPerModule));
            }

            _logger.LogDebug("Generated {Count} chunked QR codes for profile {ProfileId}", chunks.Count, profile.Profile.Id);
            return chunks;
        }, ct);
    }

    private byte[] GenerateSinglePng(byte[] data, int pixelsPerModule)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    public static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
            }
            return ~crc;
        }
}

/// <summary>
/// Service for exporting/importing profiles via QR transfer format.
/// </summary>
public interface ITransferService
{
    Task<string> ExportToJsonAsync(AetherisProfile profile, bool encrypt = false, string? encryptionKeyB64 = null, CancellationToken ct = default);
    Task<AetherisProfile> ImportFromJsonAsync(string json, string? expectedPublicKeyB64 = null, string? decryptionKeyB64 = null, CancellationToken ct = default);
    Task<AetherisProfile> ImportFromChunkedQrAsync(IReadOnlyList<string> chunkJsons, string? expectedPublicKeyB64 = null, string? decryptionKeyB64 = null, CancellationToken ct = default);
}

public sealed class TransferService : ITransferService
{
    private readonly ILogger<TransferService> _logger;

    public TransferService(ILogger<TransferService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExportToJsonAsync(AetherisProfile profile, bool encrypt = false, string? encryptionKeyB64 = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var transferProfile = ProfileIO.PrepareForTransfer(
                profile,
                encrypt,
                encryptionKeyB64
            );

            var json = JsonSerializer.Serialize(transferProfile, JsonOptions.Default);
            _logger.LogInformation("Exported profile {ProfileId} for transfer ({Length} chars)", profile.Profile.Id, json.Length);
            return json;
        }, ct);
    }

    public async Task<AetherisProfile> ImportFromJsonAsync(string json, string? expectedPublicKeyB64 = null, string? decryptionKeyB64 = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var profile = ProfileIO.ImportFromTransfer(json, expectedPublicKeyB64, decryptionKeyB64);
            _logger.LogInformation("Imported profile {ProfileId} from transfer", profile.Profile.Id);
            return profile;
        }, ct);
    }

    public async Task<AetherisProfile> ImportFromChunkedQrAsync(IReadOnlyList<string> chunkJsons, string? expectedPublicKeyB64 = null, string? decryptionKeyB64 = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (chunkJsons.Count == 0)
                throw new ArgumentException("No QR chunks provided");

            // Reassemble chunks
            var allBytes = new List<byte>();
            ushort? expectedTotal = null;

            for (int i = 0; i < chunkJsons.Count; i++)
            {
                var chunkBytes = Encoding.UTF8.GetBytes(chunkJsons[i]);

                if (chunkBytes.Length < 12)
                    throw new InvalidDataException($"Chunk {i} too small for header");

                var magic = Encoding.ASCII.GetString(chunkBytes, 0, 4);
                if (magic != "AETH")
                    throw new InvalidDataException($"Chunk {i} has invalid magic");

                var index = BitConverter.ToUInt16(chunkBytes, 4);
                var total = BitConverter.ToUInt16(chunkBytes, 6);
                var crc = BitConverter.ToUInt32(chunkBytes, 8);
                var data = chunkBytes[12..];

                if (expectedTotal == null) expectedTotal = total;
                else if (total != expectedTotal) throw new InvalidDataException("Inconsistent chunk total count");

                if (index != i) throw new InvalidDataException($"Chunk index mismatch: expected {i}, got {index}");

                                var computedCrc = QrCodeService.Crc32(data);
                                if (crc != computedCrc) throw new InvalidDataException($"Chunk {i} CRC mismatch");

                                allBytes.AddRange(data);
            }

            var fullJson = Encoding.UTF8.GetString(allBytes.ToArray());
            var profile = ProfileIO.ImportFromTransfer(fullJson, expectedPublicKeyB64, decryptionKeyB64);
            _logger.LogInformation("Imported profile {ProfileId} from {Count} chunked QR codes", profile.Profile.Id, chunkJsons.Count);
            return profile;
        }, ct);
    }
}
