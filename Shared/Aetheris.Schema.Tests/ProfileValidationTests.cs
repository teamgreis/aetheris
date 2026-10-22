using System.Text.Json;
using Aetheris.Schema;
using FluentAssertions;
using Xunit;

namespace Aetheris.Schema.Tests;

public class ProfileValidationTests
{
    private static AetherisProfile CreateValidProfile()
    {
        return new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile
            {
                Id = Guid.NewGuid(),
                Name = "Test Profile",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow,
                Notes = "Test notes"
            },
            Measurements = new List<Measurement>
            {
                new Measurement
                {
                    Id = Guid.NewGuid(),
                    BodyRegion = BodyRegion.Hand,
                    TremorType = TremorType.Resting,
                    FrequencyHz = 5.5,
                    Amplitude = Amplitude.Medium,
                    Severity = 2,
                    MeasuredAt = DateTime.UtcNow.AddHours(-2),
                    Conditions = "Morning, off medication",
                    DurationSeconds = 60,
                    DeviceInfo = new DeviceInfo
                    {
                        DeviceType = DeviceType.Smartphone,
                        DeviceName = "iPhone 15 Pro",
                        SamplingRateHz = 100
                    }
                }
            },
            Findings = new List<Finding>
            {
                new Finding
                {
                    Id = Guid.NewGuid(),
                    Category = FindingCategory.Symptom,
                    Description = "Tremor worse when tired",
                    BodyRegion = BodyRegion.Hand,
                    Severity = 6,
                    RecordedAt = DateTime.UtcNow.AddDays(-3),
                    Tags = new List<string> { "fatigue", "morning" }
                }
            },
            Settings = new Settings
            {
                Version = 1,
                TouchAssist = new TouchAssistSettings
                {
                    Enabled = true,
                    HoldDurationMs = 200,
                    IgnoreRepeatedTouchesMs = 100,
                    TapAssistRadius = 10,
                    StabilizationEnabled = true,
                    StabilizationSmoothing = 0.7
                },
                MotionAssist = new MotionAssistSettings
                {
                    Enabled = true,
                    ReduceMotion = true,
                    AutoRotateDisabled = false,
                    ShakeToUndoDisabled = true
                },
                Custom = new Dictionary<string, object?>
                {
                    ["experimentalFeature"] = true,
                    ["threshold"] = 0.5
                }
            }
        };
    }

    [Fact]
    public void ValidProfile_PassesValidation()
    {
        var profile = CreateValidProfile();
        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void MissingProfileName_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.Profile.Name = "";

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("name") && e.Code == "EMPTY_NAME");
    }

    [Fact]
    public void EmptyProfileId_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.Profile.Id = Guid.Empty;

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("id") && e.Code == "EMPTY_ID");
    }

    [Fact]
    public void UpdatedAtBeforeCreatedAt_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.Profile.UpdatedAt = profile.Profile.CreatedAt.AddDays(-1);

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_TIMESTAMP");
    }

    [Fact]
    public void FrequencyOutOfRange_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.Measurements[0].FrequencyHz = 50.0; // > 30 Hz max

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("frequencyHz") && e.Code == "RANGE");
    }

    [Fact]
    public void SeverityOutOfRange_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.Measurements[0].Severity = 5; // > 4 max

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("severity") && e.Code == "RANGE");
    }

    [Fact]
    public void FindingSeverityOutOfRange_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.Findings[0].Severity = 11; // > 10 max

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("severity") && e.Code == "RANGE");
    }

    [Fact]
    public void TooManyTags_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.Findings[0].Tags = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToList();

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "MAX_TAGS");
    }

    [Fact]
    public void TouchAssistHoldDurationOutOfRange_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.Settings.TouchAssist!.HoldDurationMs = 30; // < 50 min

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Path.Contains("holdDurationMs") && e.Code == "RANGE");
    }

    [Fact]
    public void WrongSchemaVersion_FailsValidation()
    {
        var profile = CreateValidProfile();
        profile.SchemaVersion = "99.0.0";

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "VERSION_MISMATCH");
    }

    [Fact]
    public void NullFrequency_IsAllowed()
    {
        var profile = CreateValidProfile();
        profile.Measurements[0].FrequencyHz = null;

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NullSeverity_IsAllowed()
    {
        var profile = CreateValidProfile();
        profile.Measurements[0].Severity = null;

        var result = ProfileValidator.Validate(profile);

        result.IsValid.Should().BeTrue();
    }
}

public class SerializationTests
{
    private static AetherisProfile CreateValidProfile()
    {
        return new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Serialization Test",
                CreatedAt = DateTime.Parse("2026-01-01T12:00:00Z"),
                UpdatedAt = DateTime.Parse("2026-01-02T12:00:00Z")
            },
            Measurements = new List<Measurement>
            {
                new Measurement
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    BodyRegion = BodyRegion.Finger,
                    TremorType = TremorType.Action,
                    FrequencyHz = 8.0,
                    Severity = 1,
                    MeasuredAt = DateTime.Parse("2026-01-02T10:00:00Z")
                }
            },
            Findings = new List<Finding>(),
            Settings = new Settings { Version = 1 }
        };
    }

    [Fact]
    public void RoundtripSerialization_PreservesData()
    {
        var original = CreateValidProfile();
        var json = JsonSerializer.Serialize(original, JsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<AetherisProfile>(json, JsonOptions.Default);

        deserialized.Should().NotBeNull();
        deserialized!.SchemaVersion.Should().Be(original.SchemaVersion);
        deserialized.Profile.Id.Should().Be(original.Profile.Id);
        deserialized.Profile.Name.Should().Be(original.Profile.Name);
        deserialized.Measurements.Should().HaveCount(1);
        deserialized.Measurements[0].BodyRegion.Should().Be(BodyRegion.Finger);
        deserialized.Measurements[0].TremorType.Should().Be(TremorType.Action);
        deserialized.Measurements[0].FrequencyHz.Should().Be(8.0);
    }

    [Fact]
    public void CanonicalJson_ProducesDeterministicOutput()
    {
        var profile = CreateValidProfile();
        var json1 = JsonSerializer.Serialize(profile, JsonOptions.Canonical);
        var json2 = JsonSerializer.Serialize(profile, JsonOptions.Canonical);

        json1.Should().Be(json2);
    }

    [Fact]
    public void CanonicalJson_ExcludesNulls()
    {
        var profile = CreateValidProfile();
        profile.Measurements[0].FrequencyHz = null;
        profile.Measurements[0].Conditions = null;

        var json = JsonSerializer.Serialize(profile, JsonOptions.Canonical);

        json.Should().NotContain("frequencyHz");
        json.Should().NotContain("conditions");
    }

    [Fact]
    public void DateTime_SerializesAsUtcIso8601()
    {
        var profile = CreateValidProfile();
        var json = JsonSerializer.Serialize(profile, JsonOptions.Canonical);

        json.Should().Contain("2026-01-01T12:00:00Z");
        json.Should().Contain("2026-01-02T12:00:00Z");
    }
}

public class IntegrityTests
{
    [Fact]
    public void ComputeSha256_ProducesExpectedLength()
    {
        var hash = ProfileValidator.ComputeSha256("test");
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[a-f0-9]{64}$");
    }

    [Fact]
    public void ComputeSha256_IsDeterministic()
    {
        var hash1 = ProfileValidator.ComputeSha256("test");
        var hash2 = ProfileValidator.ComputeSha256("test");
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeSha256_DifferentInputsProduceDifferentHashes()
    {
        var hash1 = ProfileValidator.ComputeSha256("test1");
        var hash2 = ProfileValidator.ComputeSha256("test2");
        hash1.Should().NotBe(hash2);
    }
}

public class MigrationTests
{
    [Fact]
    public void CurrentVersion_MigratesToSelf()
    {
        var profile = new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile { Id = Guid.NewGuid(), Name = "Test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            Measurements = new(),
            Findings = new(),
            Settings = new Settings { Version = 1 }
        };

        var migrated = ProfileMigrator.MigrateToCurrent(profile);

        migrated.SchemaVersion.Should().Be(SchemaVersion.Current);
        migrated.Profile.Name.Should().Be("Test");
    }

    [Fact]
    public void UnsupportedVersion_Throws()
    {
        var profile = new AetherisProfile
        {
            SchemaVersion = "0.9.0",
            Profile = new Profile { Id = Guid.NewGuid(), Name = "Test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            Measurements = new(),
            Findings = new(),
            Settings = new Settings { Version = 1 }
        };

        var act = () => ProfileMigrator.MigrateToCurrent(profile);
        act.Should().Throw<NotSupportedException>();
    }
}

public class ProfileIOTests
{
    private readonly string _tempDir;

    public ProfileIOTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AetherisTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SaveAndLoad_RoundtripWorks()
    {
        var profile = new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile
            {
                Id = Guid.NewGuid(),
                Name = "IO Test",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            Measurements = new(),
            Findings = new(),
            Settings = new Settings { Version = 1 }
        };

        var file = Path.Combine(_tempDir, "profile.json");
        ProfileIO.SaveToFile(profile, file);
        var loaded = ProfileIO.LoadFromFile(file);

        loaded.Profile.Name.Should().Be("IO Test");
        loaded.SchemaVersion.Should().Be(SchemaVersion.Current);
    }

    [Fact]
    public void SaveInvalidProfile_Throws()
    {
        var profile = new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile { Id = Guid.NewGuid(), Name = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            Measurements = new(),
            Findings = new(),
            Settings = new Settings { Version = 1 }
        };

        var file = Path.Combine(_tempDir, "invalid.json");
        var act = () => ProfileIO.SaveToFile(profile, file);
        act.Should().Throw<InvalidOperationException>();
    }
}