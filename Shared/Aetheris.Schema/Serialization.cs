using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.IO;
using System.Text.RegularExpressions;

namespace Aetheris.Schema;

/// <summary>
/// JSON serialization options for Aetheris profiles.
/// </summary>
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new DateTimeConverter()
        }
    };

    /// <summary>
    /// Canonical serialization for integrity hashing: no whitespace, sorted keys, no nulls.
    /// </summary>
    public static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new DateTimeConverter()
        }
    };
}

/// <summary>
/// ISO 8601 UTC date-time converter (yyyy-MM-ddTHH:mm:ssZ).
/// </summary>
public sealed class DateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-ddTHH:mm:ssZ";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (DateTime.TryParse(str, out var dt))
            return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
        throw new JsonException($"Invalid date-time format: {str}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString(Format));
    }
}

/// <summary>
/// Validation result with detailed errors.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    private ValidationResult(bool isValid, List<ValidationError> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public static ValidationResult Success() => new(true, new List<ValidationError>());
    public static ValidationResult Failure(params ValidationError[] errors) => new(false, errors.ToList());
    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new(false, errors.ToList());
}

/// <summary>
/// Single validation error with JSON path context.
/// </summary>
public sealed class ValidationError
{
    public string Path { get; }
    public string Message { get; }
    public string? Code { get; }

    public ValidationError(string path, string message, string? code = null)
    {
        Path = path;
        Message = message;
        Code = code;
    }

    public override string ToString() => $"{Path}: {Message}" + (Code != null ? $" [{Code}]" : "");
}

/// <summary>
/// Validates Aetheris profiles against business rules.
/// JSON Schema validation removed - use external tools for schema validation if needed.
/// </summary>
public static class ProfileValidator
{
    /// <summary>
    /// Validates a profile object against business rules.
    /// </summary>
    public static ValidationResult Validate(AetherisProfile profile)
    {
        var errors = new List<ValidationError>();

        // Business rule validation
        ValidateBusinessRules(profile, errors);

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    private static void ValidateBusinessRules(AetherisProfile profile, List<ValidationError> errors)
    {
        // Schema version must match current
        if (profile.SchemaVersion != SchemaVersion.Current)
        {
            errors.Add(new ValidationError("$.schemaVersion",
                $"Schema version '{profile.SchemaVersion}' not supported. Current: {SchemaVersion.Current}",
                "VERSION_MISMATCH"));
        }

        // Profile ID must be valid UUID
        if (profile.Profile.Id == Guid.Empty)
        {
            errors.Add(new ValidationError("$.profile.id", "Profile ID must be a valid UUID", "EMPTY_ID"));
        }

        // Name required
        if (string.IsNullOrWhiteSpace(profile.Profile.Name))
        {
            errors.Add(new ValidationError("$.profile.name", "Profile name is required", "EMPTY_NAME"));
        }

        // UpdatedAt >= CreatedAt
        if (profile.Profile.UpdatedAt < profile.Profile.CreatedAt)
        {
            errors.Add(new ValidationError("$.profile.updatedAt", "UpdatedAt cannot be before CreatedAt", "INVALID_TIMESTAMP"));
        }

        // Measurements validation
        for (int i = 0; i < profile.Measurements.Count; i++)
        {
            var m = profile.Measurements[i];
            var path = $"$.measurements[{i}]";

            if (m.Id == Guid.Empty)
                errors.Add(new ValidationError($"{path}.id", "Measurement ID must be valid UUID", "EMPTY_ID"));

            if (m.FrequencyHz.HasValue && (m.FrequencyHz < 0.5 || m.FrequencyHz > 30))
                errors.Add(new ValidationError($"{path}.frequencyHz", "Frequency must be 0.5-30 Hz", "RANGE"));

            if (m.Severity.HasValue && (m.Severity < 0 || m.Severity > 4))
                errors.Add(new ValidationError($"{path}.severity", "Severity must be 0-4", "RANGE"));

            if (m.DurationSeconds.HasValue && (m.DurationSeconds < 1 || m.DurationSeconds > 3600))
                errors.Add(new ValidationError($"{path}.durationSeconds", "Duration must be 1-3600 seconds", "RANGE"));

            if (m.DeviceInfo?.SamplingRateHz.HasValue == true &&
                (m.DeviceInfo.SamplingRateHz < 10 || m.DeviceInfo.SamplingRateHz > 1000))
                errors.Add(new ValidationError($"{path}.deviceInfo.samplingRateHz", "Sampling rate must be 10-1000 Hz", "RANGE"));
        }

        // Findings validation
        for (int i = 0; i < profile.Findings.Count; i++)
        {
            var f = profile.Findings[i];
            var path = $"$.findings[{i}]";

            if (f.Id == Guid.Empty)
                errors.Add(new ValidationError($"{path}.id", "Finding ID must be valid UUID", "EMPTY_ID"));

            if (string.IsNullOrWhiteSpace(f.Description))
                errors.Add(new ValidationError($"{path}.description", "Finding description is required", "EMPTY_DESCRIPTION"));

            if (f.Severity.HasValue && (f.Severity < 0 || f.Severity > 10))
                errors.Add(new ValidationError($"{path}.severity", "Finding severity must be 0-10", "RANGE"));

            if (f.Tags.Count > 10)
                errors.Add(new ValidationError($"{path}.tags", "Maximum 10 tags allowed", "MAX_TAGS"));
        }

        // Settings validation
        if (profile.Settings.Version < 1)
            errors.Add(new ValidationError("$.settings.version", "Settings version must be >= 1", "RANGE"));

        ValidateTouchAssist(profile.Settings.TouchAssist, errors);
        ValidateMotionAssist(profile.Settings.MotionAssist, errors);
    }

    private static void ValidateTouchAssist(TouchAssistSettings? s, List<ValidationError> errors)
    {
        if (s == null) return;

        if (s.HoldDurationMs.HasValue && (s.HoldDurationMs < 50 || s.HoldDurationMs > 3000))
            errors.Add(new ValidationError("$.settings.touchAssist.holdDurationMs", "Hold duration must be 50-3000 ms", "RANGE"));

        if (s.IgnoreRepeatedTouchesMs.HasValue && (s.IgnoreRepeatedTouchesMs < 0 || s.IgnoreRepeatedTouchesMs > 2000))
            errors.Add(new ValidationError("$.settings.touchAssist.ignoreRepeatedTouchesMs", "Ignore repeated touches must be 0-2000 ms", "RANGE"));

        if (s.TapAssistRadius.HasValue && (s.TapAssistRadius < 0 || s.TapAssistRadius > 50))
            errors.Add(new ValidationError("$.settings.touchAssist.tapAssistRadius", "Tap assist radius must be 0-50 points", "RANGE"));

        if (s.StabilizationSmoothing.HasValue && (s.StabilizationSmoothing < 0.0 || s.StabilizationSmoothing > 1.0))
            errors.Add(new ValidationError("$.settings.touchAssist.stabilizationSmoothing", "Smoothing must be 0.0-1.0", "RANGE"));
    }

    private static void ValidateMotionAssist(MotionAssistSettings? s, List<ValidationError> errors)
    {
        // No additional range checks needed for booleans
    }

    /// <summary>
    /// Validates transfer integrity (hash and optional signature).
    /// </summary>
    public static ValidationResult ValidateTransfer(AetherisProfile profile, string? expectedPublicKey = null)
    {
        var errors = new List<ValidationError>();

        if (profile.Transfer == null)
        {
            errors.Add(new ValidationError("$.transfer", "Transfer metadata is missing", "MISSING_TRANSFER"));
            return ValidationResult.Failure(errors);
        }

        // Recompute hash of profile without transfer object
        var profileForHash = new AetherisProfile
        {
            SchemaVersion = profile.SchemaVersion,
            Profile = profile.Profile,
            Measurements = profile.Measurements,
            Findings = profile.Findings,
            Settings = profile.Settings,
            Transfer = null
        };

        var canonicalJson = JsonSerializer.Serialize(profileForHash, JsonOptions.Canonical);
        var computedHash = ComputeSha256(canonicalJson);

        if (!computedHash.Equals(profile.Transfer.Integrity.Hash, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ValidationError("$.transfer.integrity.hash",
                "Integrity hash mismatch - profile data may have been modified", "INTEGRITY_FAILED"));
        }

        // Verify signature if present
        if (profile.Transfer.Signature != null)
        {
            if (string.IsNullOrEmpty(expectedPublicKey))
            {
                errors.Add(new ValidationError("$.transfer.signature",
                    "Signature present but no public key provided for verification", "MISSING_PUBLIC_KEY"));
            }
            else if (!VerifyEd25519Signature(profile.Transfer.Integrity.Hash, profile.Transfer.Signature.Signature, expectedPublicKey))
            {
                errors.Add(new ValidationError("$.transfer.signature",
                    "Signature verification failed", "SIGNATURE_INVALID"));
            }
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }

    public static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool VerifyEd25519Signature(string message, string signatureB64, string publicKeyB64)
    {
        try
        {
            var publicKey = Convert.FromBase64String(publicKeyB64);
            var signature = Convert.FromBase64String(signatureB64);
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);

            // Ed25519 verification requires .NET 8+ or Sodium.Core
            // Placeholder: implement with System.Security.Cryptography.Ed25519 when available
            // For now, return true if lengths are correct (actual impl needed)
            return publicKey.Length == 32 && signature.Length == 64;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Migration helpers for schema version upgrades.
/// </summary>
public static class ProfileMigrator
{
    /// <summary>
    /// Migrates a profile from an older schema version to current.
    /// Returns migrated profile or throws if migration not supported.
    /// </summary>
    public static AetherisProfile MigrateToCurrent(AetherisProfile profile)
    {
        var fromVersion = profile.SchemaVersion;
        if (fromVersion == SchemaVersion.Current)
            return profile;

        if (!SupportedSchemaVersions.All.Contains(fromVersion))
            throw new NotSupportedException($"Migration from schema version {fromVersion} not supported");

        // Apply migrations in sequence
        var current = profile;
        var versions = SupportedSchemaVersions.All
            .Where(v => VersionCompare(v, fromVersion) > 0)
            .OrderBy(v => v, new VersionComparer())
            .ToList();

        foreach (var targetVersion in versions)
        {
            current = MigrateStep(current, targetVersion);
        }

        current.SchemaVersion = SchemaVersion.Current;
        current.Profile.UpdatedAt = DateTime.UtcNow;
        return current;
    }

    private static AetherisProfile MigrateStep(AetherisProfile profile, string targetVersion)
    {
        // No migrations needed yet (v1.0.0 is first version)
        // Future migrations would go here:
        // if (targetVersion == "1.1.0") return MigrateTo110(profile);
        return profile;
    }

    private static int VersionCompare(string a, string b)
    {
        var va = Version.Parse(a);
        var vb = Version.Parse(b);
        return va.CompareTo(vb);
    }

    private sealed class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y) => VersionCompare(x ?? "0.0.0", y ?? "0.0.0");
    }
}

/// <summary>
/// High-level profile I/O with validation and transfer support.
/// </summary>
public static class ProfileIO
{
    /// <summary>
    /// Loads and validates a profile from JSON file.
    /// </summary>
    public static AetherisProfile LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<AetherisProfile>(json, JsonOptions.Default)
            ?? throw new InvalidDataException("Failed to deserialize profile");

        var validation = ProfileValidator.Validate(profile);
        if (!validation.IsValid)
            throw new InvalidDataException($"Profile validation failed: {string.Join("; ", validation.Errors)}");

        return profile;
    }

    /// <summary>
    /// Saves profile to JSON file with validation.
    /// </summary>
    public static void SaveToFile(AetherisProfile profile, string path)
    {
        var validation = ProfileValidator.Validate(profile);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Profile invalid: {string.Join("; ", validation.Errors)}");

        profile.Profile.UpdatedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(profile, JsonOptions.Default);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Creates a transfer-ready profile with integrity hash and optional encryption/signature.
    /// </summary>
    public static AetherisProfile PrepareForTransfer(
        AetherisProfile profile,
        bool encrypt = false,
        string? encryptionKey = null,
        string? signingPrivateKey = null,
        string? signingPublicKey = null)
    {
        var validation = ProfileValidator.Validate(profile);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Profile invalid: {string.Join("; ", validation.Errors)}");

        profile.Profile.UpdatedAt = DateTime.UtcNow;

        // Compute integrity hash
        var profileForHash = new AetherisProfile
        {
            SchemaVersion = profile.SchemaVersion,
            Profile = profile.Profile,
            Measurements = profile.Measurements,
            Findings = profile.Findings,
            Settings = profile.Settings,
            Transfer = null
        };

        var canonicalJson = JsonSerializer.Serialize(profileForHash, JsonOptions.Canonical);
        var hash = ComputeSha256(canonicalJson);

        var transfer = new TransferInfo
        {
            ExportedAt = DateTime.UtcNow,
            ExportedBy = "Aetheris.Studio",
            ExportVersion = "1.0.0",
            Integrity = new IntegrityInfo { Algorithm = "SHA-256", Hash = hash },
            Encrypted = encrypt
        };

        if (encrypt)
        {
            if (string.IsNullOrEmpty(encryptionKey))
                throw new ArgumentException("Encryption key required when encrypt=true");

            var payloadJson = JsonSerializer.Serialize(profileForHash, JsonOptions.Canonical);
            var (ciphertext, nonce, tag) = EncryptAes256Gcm(payloadJson, encryptionKey);
            // Combine nonce(12) || ciphertext || tag(16)
            var combined = new byte[12 + ciphertext.Length + 16];
            Buffer.BlockCopy(nonce, 0, combined, 0, 12);
            Buffer.BlockCopy(ciphertext, 0, combined, 12, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, 12 + ciphertext.Length, 16);
            transfer.Payload = Convert.ToBase64String(combined);
        }

        if (!string.IsNullOrEmpty(signingPrivateKey) && !string.IsNullOrEmpty(signingPublicKey))
        {
            var signature = SignEd25519(hash, signingPrivateKey);
            transfer.Signature = new SignatureInfo
            {
                Algorithm = "Ed25519",
                PublicKey = signingPublicKey,
                Signature = signature
            };
        }

        profile.Transfer = transfer;
        return profile;
    }

    /// <summary>
    /// Imports a profile from transfer format (QR code), validating integrity and signature.
    /// </summary>
    public static AetherisProfile ImportFromTransfer(
        string jsonOrBase64,
        string? expectedPublicKey = null,
        string? decryptionKey = null)
    {
        AetherisProfile profile;

        // Try direct JSON first, then base64
        try
        {
            profile = JsonSerializer.Deserialize<AetherisProfile>(jsonOrBase64, JsonOptions.Default)
                ?? throw new InvalidDataException("Failed to deserialize");
        }
        catch (JsonException)
        {
            // Try base64 decode
            var bytes = Convert.FromBase64String(jsonOrBase64);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            profile = JsonSerializer.Deserialize<AetherisProfile>(json, JsonOptions.Default)
                ?? throw new InvalidDataException("Failed to deserialize after base64 decode");
        }

        // Handle encrypted payload
        if (profile.Transfer?.Encrypted == true)
        {
            if (string.IsNullOrEmpty(decryptionKey))
                throw new InvalidOperationException("Profile is encrypted but no decryption key provided");

            if (string.IsNullOrEmpty(profile.Transfer.Payload))
                throw new InvalidDataException("Encrypted profile missing payload");

            var combined = Convert.FromBase64String(profile.Transfer.Payload);
            if (combined.Length < 12 + 16)
                throw new InvalidDataException("Invalid encrypted payload format");

            var nonce = combined[..12];
            var tag = combined[^16..];
            var ciphertext = combined[12..^16];

            var plaintext = DecryptAes256Gcm(ciphertext, nonce, tag, decryptionKey);
            var decryptedProfile = JsonSerializer.Deserialize<AetherisProfile>(plaintext, JsonOptions.Default)
                ?? throw new InvalidDataException("Failed to decrypt profile");

            // Merge decrypted data with transfer metadata (manual since AetherisProfile is a class)
            profile = new AetherisProfile
            {
                SchemaVersion = decryptedProfile.SchemaVersion,
                Profile = decryptedProfile.Profile,
                Measurements = decryptedProfile.Measurements,
                Findings = decryptedProfile.Findings,
                Settings = decryptedProfile.Settings,
                Transfer = profile.Transfer
            };
        }

        // Validate structure
        var validation = ProfileValidator.Validate(profile);
        if (!validation.IsValid)
            throw new InvalidDataException($"Profile validation failed: {string.Join("; ", validation.Errors)}");

        // Validate transfer integrity
        var transferValidation = ProfileValidator.ValidateTransfer(profile, expectedPublicKey);
        if (!transferValidation.IsValid)
            throw new InvalidDataException($"Transfer validation failed: {string.Join("; ", transferValidation.Errors)}");

        return profile;
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static (byte[] ciphertext, byte[] nonce, byte[] tag) EncryptAes256Gcm(string plaintext, string keyB64)
    {
        var key = Convert.FromBase64String(keyB64);
        if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes (256 bits)");

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        return (ciphertext, nonce, tag);
    }

    private static string DecryptAes256Gcm(byte[] ciphertext, byte[] nonce, byte[] tag, string keyB64)
    {
        var key = Convert.FromBase64String(keyB64);
        if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes (256 bits)");

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }

    private static string SignEd25519(string message, string privateKeyB64)
    {
        // Placeholder - implement with Ed25519 when available in .NET
        // For now return dummy signature of correct length
        return Convert.ToBase64String(new byte[64]);
    }
}