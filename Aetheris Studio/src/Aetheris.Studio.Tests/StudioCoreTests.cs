using System;
using System.IO;
using System.Threading.Tasks;
using Aetheris.Schema;
using Aetheris.Studio.Core;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aetheris.Studio.Tests;

public class FileProfileRepositoryTests
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<FileProfileRepository>> _loggerMock;

    public FileProfileRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AetherisTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _loggerMock = new Mock<ILogger<FileProfileRepository>>();
    }

    [Fact]
    public async Task CreateNewAsync_CreatesProfileWithDefaults()
    {
        var repo = new FileProfileRepository(_tempDir, _loggerMock.Object);
        var id = await repo.CreateNewAsync("Test Profile");

        id.Should().NotBe(Guid.Empty);

        var profile = await repo.LoadAsync(id);
        profile.Should().NotBeNull();
        profile!.Profile.Name.Should().Be("Test Profile");
        profile.Profile.Id.Should().Be(id);
        profile.Measurements.Should().BeEmpty();
        profile.Findings.Should().BeEmpty();
        profile.Settings.Version.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSavedProfiles()
    {
        var repo = new FileProfileRepository(_tempDir, _loggerMock.Object);
        var id1 = await repo.CreateNewAsync("Profile 1");
        var id2 = await repo.CreateNewAsync("Profile 2");

        var profiles = await repo.GetAllAsync();

        profiles.Should().HaveCount(2);
        profiles.Should().Contain(p => p.Id == id1 && p.Name == "Profile 1");
        profiles.Should().Contain(p => p.Id == id2 && p.Name == "Profile 2");
    }

    [Fact]
    public async Task SaveAsync_UpdatesTimestamp()
    {
        var repo = new FileProfileRepository(_tempDir, _loggerMock.Object);
        var id = await repo.CreateNewAsync("Original");

        var profile = await repo.LoadAsync(id);
        var originalUpdated = profile!.Profile.UpdatedAt;

        await Task.Delay(10);
        profile.Profile.Name = "Updated";
        await repo.SaveAsync(profile);

        var reloaded = await repo.LoadAsync(id);
        reloaded!.Profile.Name.Should().Be("Updated");
        reloaded.Profile.UpdatedAt.Should().BeAfter(originalUpdated);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProfile()
    {
        var repo = new FileProfileRepository(_tempDir, _loggerMock.Object);
        var id = await repo.CreateNewAsync("To Delete");

        await repo.DeleteAsync(id);

        var profile = await repo.LoadAsync(id);
        profile.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_NonExistent_ReturnsNull()
    {
        var repo = new FileProfileRepository(_tempDir, _loggerMock.Object);

        var profile = await repo.LoadAsync(Guid.NewGuid());

        profile.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_InvalidProfile_Throws()
    {
        var repo = new FileProfileRepository(_tempDir, _loggerMock.Object);
        var profile = new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile { Id = Guid.NewGuid(), Name = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            Measurements = new(),
            Findings = new(),
            Settings = new Settings { Version = 1 }
        };

        var act = () => repo.SaveAsync(profile);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

public class QrCodeServiceTests
{
    private readonly Mock<ILogger<QrCodeService>> _loggerMock;

    public QrCodeServiceTests()
    {
        _loggerMock = new Mock<ILogger<QrCodeService>>();
    }

    [Fact]
    public async Task GeneratePngAsync_ValidProfile_ReturnsPngBytes()
    {
        var service = new QrCodeService(_loggerMock.Object);
        var profile = CreateTestProfile();

        var png = await service.GeneratePngAsync(profile);

        png.Should().NotBeNullOrEmpty();
        png.Length.Should().BeGreaterThan(100);
        png[0].Should().Be(0x89);
        png[1].Should().Be(0x50);
        png[2].Should().Be(0x4E);
        png[3].Should().Be(0x47);
    }

    [Fact]
    public async Task GenerateSvgAsync_ValidProfile_ReturnsSvgBytes()
    {
        var service = new QrCodeService(_loggerMock.Object);
        var profile = CreateTestProfile();

        var svg = await service.GenerateSvgAsync(profile);

        svg.Should().NotBeNullOrEmpty();
        var svgString = System.Text.Encoding.UTF8.GetString(svg);
        svgString.Should().Contain("<svg");
    }

    [Fact]
    public async Task GenerateChunkedPngAsync_SmallProfile_ReturnsSingleChunk()
    {
        var service = new QrCodeService(_loggerMock.Object);
        var profile = CreateTestProfile();

        var chunks = await service.GenerateChunkedPngAsync(profile, maxBytesPerChunk: 10000);

        chunks.Should().HaveCount(1);
        chunks[0].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateChunkedPngAsync_LargeProfile_ReturnsMultipleChunks()
    {
        var service = new QrCodeService(_loggerMock.Object);
        var profile = CreateTestProfile();
        for (int i = 0; i < 100; i++)
        {
            profile.Measurements.Add(new Measurement
            {
                Id = Guid.NewGuid(),
                BodyRegion = BodyRegion.Hand,
                TremorType = TremorType.Resting,
                FrequencyHz = 5.0 + i * 0.1,
                Severity = i % 5,
                MeasuredAt = DateTime.UtcNow,
                Conditions = $"Test measurement {i} with some long text to increase size"
            });
        }

        var chunks = await service.GenerateChunkedPngAsync(profile, maxBytesPerChunk: 500);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().AllSatisfy(c => c.Should().NotBeNullOrEmpty());
    }

    private static AetherisProfile CreateTestProfile()
    {
        return new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile
            {
                Id = Guid.NewGuid(),
                Name = "QR Test",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            Measurements = new List<Measurement>
            {
                new Measurement
                {
                    Id = Guid.NewGuid(),
                    BodyRegion = BodyRegion.Hand,
                    TremorType = TremorType.Resting,
                    FrequencyHz = 5.5,
                    Severity = 2,
                    MeasuredAt = DateTime.UtcNow
                }
            },
            Findings = new List<Finding>(),
            Settings = new Settings { Version = 1 }
        };
    }
}

public class TransferServiceTests
{
    private readonly Mock<ILogger<TransferService>> _loggerMock;

    public TransferServiceTests()
    {
        _loggerMock = new Mock<ILogger<TransferService>>();
    }

    [Fact]
    public async Task ExportToJsonAsync_ValidProfile_ReturnsJson()
    {
        var service = new TransferService(_loggerMock.Object);
        var profile = CreateTestProfile();

        var json = await service.ExportToJsonAsync(profile);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("schemaVersion");
        json.Should().Contain("profile");
        json.Should().Contain("QR Test");
    }

    [Fact]
    public async Task ImportFromJsonAsync_ValidJson_ReturnsProfile()
    {
        var service = new TransferService(_loggerMock.Object);
        var profile = CreateTestProfile();
        var json = await service.ExportToJsonAsync(profile);

        var imported = await service.ImportFromJsonAsync(json);

        imported.Should().NotBeNull();
        imported.Profile.Name.Should().Be("Transfer Test");
    }

    private static AetherisProfile CreateTestProfile()
    {
        return new AetherisProfile
        {
            SchemaVersion = SchemaVersion.Current,
            Profile = new Profile
            {
                Id = Guid.NewGuid(),
                Name = "Transfer Test",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            Measurements = new(),
            Findings = new(),
            Settings = new Settings { Version = 1 }
        };
    }
}

public class EncryptionTests
{
    [Fact]
    public void DpapiKeyProvider_ReturnsKey()
    {
        var provider = new DpapiKeyProvider();
        var settings = new EncryptionSettings { UseDpapi = true };

        var key = provider.GetKeyAsync(settings).Result;

        key.Should().NotBeNull();
        key.Length.Should().Be(32);
    }

    [Fact]
    public void PasswordKeyProvider_ReturnsKey()
    {
        var provider = new PasswordKeyProvider("test-password-123");
        var settings = new EncryptionSettings();

        var key = provider.GetKeyAsync(settings).Result;

        key.Should().NotBeNull();
        key.Length.Should().Be(32);
    }

    [Fact]
    public void FileProfileRepository_EncryptAndDecrypt_Roundtrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AetherisTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var settings = new EncryptionSettings { Enabled = true, UseDpapi = true };
            var repo = new FileProfileRepository(tempDir, Mock.Of<ILogger<FileProfileRepository>>(), settings);

            var profile = new AetherisProfile
            {
                SchemaVersion = SchemaVersion.Current,
                Profile = new Profile
                {
                    Id = Guid.NewGuid(),
                    Name = "Encrypted Profile",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                Measurements = new(),
                Findings = new(),
                Settings = new Settings { Version = 1 }
            };

            repo.SaveAsync(profile).Wait();
            var loaded = repo.LoadAsync(profile.Profile.Id).Result;

            loaded.Should().NotBeNull();
            loaded!.Profile.Name.Should().Be("Encrypted Profile");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
