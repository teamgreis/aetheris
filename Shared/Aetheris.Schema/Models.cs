namespace Aetheris.Schema;

/// <summary>
/// Current schema version. Must match the schemaVersion field in JSON exactly.
/// Format: MAJOR.MINOR.PATCH (SemVer)
/// </summary>
public static class SchemaVersion
{
    public const string Current = "1.0.0";
    public const int Major = 1;
    public const int Minor = 0;
    public const int Patch = 0;
}

/// <summary>
/// Supported schema versions for migration.
/// </summary>
public static class SupportedSchemaVersions
{
    public static readonly IReadOnlyList<string> All = new[] { "1.0.0" };
    public static readonly string Latest = "1.0.0";
}

/// <summary>
/// Root profile container matching the JSON schema.
/// </summary>
public sealed class AetherisProfile
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public Profile Profile { get; set; } = new();
    public List<Measurement> Measurements { get; set; } = new();
    public List<Finding> Findings { get; set; } = new();
    public Settings Settings { get; set; } = new();
    public TransferInfo? Transfer { get; set; }
}

/// <summary>
/// Profile identification and metadata.
/// </summary>
public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

/// <summary>
/// Anatomical region for tremor measurement.
/// </summary>
public enum BodyRegion
{
    Hand,
    Arm,
    Finger,
    Leg,
    Foot,
    Head,
    Voice,
    Trunk,
    Other
}

/// <summary>
/// Clinical tremor type classification.
/// </summary>
public enum TremorType
{
    Resting,
    Action,
    Postural,
    Kinetic,
    Intention,
    TaskSpecific,
    Orthostatic,
    UserDefined,
    Unknown
}

/// <summary>
/// Clinical amplitude classification.
/// </summary>
public enum Amplitude
{
    Fine,
    Medium,
    Coarse
}

/// <summary>
/// Device type used for measurement.
/// </summary>
public enum DeviceType
{
    Accelerometer,
    Gyroscope,
    Smartphone,
    Wearable,
    Clinical,
    Manual,
    Other
}

/// <summary>
/// Single tremor measurement entry.
/// </summary>
public sealed class Measurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public BodyRegion BodyRegion { get; set; }
    public TremorType TremorType { get; set; } = TremorType.Unknown;
    public double? FrequencyHz { get; set; }
    public Amplitude? Amplitude { get; set; }
    public int? Severity { get; set; } // 0-4 clinical scale
    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    public string? Conditions { get; set; }
    public int? DurationSeconds { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }
}

/// <summary>
/// Optional metadata about measurement device/method.
/// </summary>
public sealed class DeviceInfo
{
    public DeviceType DeviceType { get; set; }
    public string? DeviceName { get; set; }
    public int? SamplingRateHz { get; set; }
}

/// <summary>
/// Category of user observation/finding.
/// </summary>
public enum FindingCategory
{
    Symptom,
    Trigger,
    Relief,
    ActivityImpact,
    MedicationEffect,
    Custom,
    Unknown
}

/// <summary>
/// User-entered observation or qualitative note.
/// </summary>
public sealed class Finding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FindingCategory Category { get; set; } = FindingCategory.Unknown;
    public string Description { get; set; } = string.Empty;
    public BodyRegion? BodyRegion { get; set; }
    public int? Severity { get; set; } // 0-10 subjective
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Assistive configuration settings for Aetheris Mobile.
/// </summary>
public sealed class Settings
{
    public int Version { get; set; } = 1;
    public TouchAssistSettings? TouchAssist { get; set; }
    public MotionAssistSettings? MotionAssist { get; set; }
    public VoiceAssistSettings? VoiceAssist { get; set; }
    public DisplayAssistSettings? DisplayAssist { get; set; }
    public Dictionary<string, object?> Custom { get; set; } = new();
}

/// <summary>
/// Touch accommodation settings (compatible with iOS AssistiveTouch parameters).
/// </summary>
public sealed class TouchAssistSettings
{
    public bool Enabled { get; set; }
    public int? HoldDurationMs { get; set; } // 50-3000
    public int? IgnoreRepeatedTouchesMs { get; set; } // 0-2000
    public double? TapAssistRadius { get; set; } // 0-50 points
    public bool StabilizationEnabled { get; set; }
    public double? StabilizationSmoothing { get; set; } // 0.0-1.0
}

/// <summary>
/// Motion sensitivity reduction settings.
/// </summary>
public sealed class MotionAssistSettings
{
    public bool Enabled { get; set; }
    public bool ReduceMotion { get; set; } = true;
    public bool AutoRotateDisabled { get; set; }
    public bool ShakeToUndoDisabled { get; set; } = true;
}

/// <summary>
/// Voice control and dictation preferences.
/// </summary>
public sealed class VoiceAssistSettings
{
    public bool Enabled { get; set; }
    public bool VoiceControlEnabled { get; set; }
    public bool DictationEnabled { get; set; }
}

/// <summary>
/// Display accommodation settings.
/// </summary>
public sealed class DisplayAssistSettings
{
    public bool Enabled { get; set; }
    public bool LargerText { get; set; }
    public bool BoldText { get; set; }
    public bool IncreaseContrast { get; set; }
    public bool ReduceTransparency { get; set; }
    public bool ReduceWhitePoint { get; set; }
}

/// <summary>
/// Transfer metadata added on export, validated on import.
/// </summary>
public sealed class TransferInfo
{
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string ExportedBy { get; set; } = "Aetheris.Studio";
    public string ExportVersion { get; set; } = "1.0.0";
    public IntegrityInfo Integrity { get; set; } = new();
    public SignatureInfo? Signature { get; set; }
    public bool Encrypted { get; set; }
    public string? Payload { get; set; } // base64 ciphertext if encrypted
}

/// <summary>
/// Integrity verification (SHA-256 of canonical JSON).
/// </summary>
public sealed class IntegrityInfo
{
    public string Algorithm { get; set; } = "SHA-256";
    public string Hash { get; set; } = string.Empty; // 64 hex chars
}

/// <summary>
/// Optional Ed25519 digital signature for authenticity.
/// </summary>
public sealed class SignatureInfo
{
    public string Algorithm { get; set; } = "Ed25519";
    public string PublicKey { get; set; } = string.Empty; // base64, 43 chars + =
    public string Signature { get; set; } = string.Empty; // base64, 86 chars + =
}