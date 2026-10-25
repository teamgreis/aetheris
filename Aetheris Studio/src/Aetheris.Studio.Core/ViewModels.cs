using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aetheris.Schema;

namespace Aetheris.Studio.Core.ViewModels;

/// <summary>
/// Main application state - coordinates navigation between views.
/// </summary>
public sealed partial class AppViewModel : ObservableObject
{
    private readonly IProfileRepository _repository;
    private readonly IQrCodeService _qrService;
    private readonly ITransferService _transferService;
    private readonly IDialogService _dialogService;
    private readonly IFilePickerService _filePickerService;

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private ProfileSummary? _selectedProfile;

    public ObservableCollection<ProfileSummary> Profiles { get; } = new();

    public AppViewModel(IProfileRepository repository, IQrCodeService qrService, ITransferService transferService, IDialogService dialogService, IFilePickerService filePickerService)
    {
        _repository = repository;
        _qrService = qrService;
        _transferService = transferService;
        _dialogService = dialogService;
        _filePickerService = filePickerService;

        CurrentView = new ProfileListViewModel(this, _repository);
    }

    public async Task LoadProfilesAsync()
    {
        var profiles = await _repository.GetAllAsync();
        Profiles.Clear();
        foreach (var p in profiles) Profiles.Add(p);
    }

    [RelayCommand]
    public async Task NewProfileAsync()
    {
        var name = await _dialogService.PromptForNameAsync("New Profile");
        if (!string.IsNullOrWhiteSpace(name))
        {
            var id = await _repository.CreateNewAsync(name!);
            await LoadProfilesAsync();
            SelectProfile(Profiles.First(p => p.Id == id));
        }
    }

    [RelayCommand]
    public void SelectProfile(ProfileSummary? profile)
    {
        SelectedProfile = profile;
        if (profile != null)
        {
            CurrentView = new ProfileDetailViewModel(this, _repository, _qrService, _transferService, _filePickerService, profile.Id);
        }
        else
        {
            CurrentView = new ProfileListViewModel(this, _repository);
        }
    }

    [RelayCommand]
    public async Task DeleteProfileAsync(ProfileSummary? profile)
    {
        if (profile == null) return;
        if (!await _dialogService.ConfirmAsync($"Delete profile '{profile.Name}'?")) return;

        await _repository.DeleteAsync(profile.Id);
        await LoadProfilesAsync();
    }
}

/// <summary>
/// Profile list/selection view.
/// </summary>
public sealed partial class ProfileListViewModel : ObservableObject
{
    private readonly AppViewModel _app;
    private readonly IProfileRepository _repository;

    public ProfileListViewModel(AppViewModel app, IProfileRepository repository)
    {
        _app = app;
        _repository = repository;
    }

    [RelayCommand]
    private async Task LoadedAsync() => await _app.LoadProfilesAsync();

    [RelayCommand]
    private void OpenProfile(ProfileSummary profile) => _app.SelectProfile(profile);

    [RelayCommand]
    private async Task NewProfileAsync() => await _app.NewProfileAsync();
}

/// <summary>
/// Profile detail view - shows measurements, findings, settings, export.
/// </summary>
public sealed partial class ProfileDetailViewModel : ObservableObject
{
    private readonly AppViewModel _app;
    private readonly IProfileRepository _repository;
    private readonly IQrCodeService _qrService;
    private readonly ITransferService _transferService;
    private readonly IFilePickerService _filePickerService;
    private readonly Guid _profileId;

    [ObservableProperty]
    private AetherisProfile? _profile;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private byte[]? _qrCodeImage;

    public ObservableCollection<MeasurementViewModel> Measurements { get; } = new();
    public ObservableCollection<FindingViewModel> Findings { get; } = new();

    public ProfileDetailViewModel(AppViewModel app, IProfileRepository repository, IQrCodeService qrService, ITransferService transferService, IFilePickerService filePickerService, Guid profileId)
    {
        _app = app;
        _repository = repository;
        _qrService = qrService;
        _transferService = transferService;
        _filePickerService = filePickerService;
        _profileId = profileId;
    }

    [RelayCommand]
    private async Task LoadedAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Profile = await _repository.LoadAsync(_profileId);
            if (Profile != null)
            {
                RefreshCollections();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load profile: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Profile == null) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            ApplyCollectionsToProfile();
            await _repository.SaveAsync(Profile);
            _app.LoadProfilesAsync(); // Refresh list
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task GenerateQrCodeAsync()
    {
        if (Profile == null) return;
        try
        {
            QrCodeImage = await _qrService.GeneratePngAsync(Profile);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"QR generation failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GenerateChunkedQrCodeAsync()
    {
        if (Profile == null) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var chunks = await _qrService.GenerateChunkedPngAsync(Profile, maxBytesPerChunk: 2500);
            if (chunks.Count == 1)
            {
                QrCodeImage = chunks[0];
                await _dialogService.ShowErrorAsync("QR-Code", "Profil passt in einen einzelnen QR-Code. Chunked Export nicht nötig.");
            }
            else
            {
                for (int i = 0; i < chunks.Count; i++)
                {
                    var filePath = await _filePickerService.SaveFileAsync(
                        $"Aetheris_{Profile.Profile.Name}_QR_Part{i + 1}of{chunks.Count}",
                        ".png",
                        new[] { ("PNG Image", new[] { ".png" }) }
                    );
                    if (filePath != null)
                    {
                        await File.WriteAllBytesAsync(filePath, chunks[i]);
                    }
                }
                await _dialogService.ShowErrorAsync("Chunked QR Export", $"{chunks.Count} QR-Codes gespeichert. Scanne sie nacheinander in Aetheris Mobile.");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Chunked QR generation failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveQrCodeAsync()
    {
        if (QrCodeImage == null) return;
        try
        {
            var filePath = await _filePickerService.SaveFileAsync(
                $"Aetheris_{Profile?.Profile.Name ?? "Profile"}_QR",
                ".png",
                new[] { ("PNG Image", new[] { ".png" }) }
            );
            if (filePath != null)
            {
                await File.WriteAllBytesAsync(filePath, QrCodeImage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Save QR failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (Profile == null) return;
        try
