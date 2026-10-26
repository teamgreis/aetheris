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
        var name = await _dialogService.PromptForNameAsync("Neues Profil");
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
            CurrentView = new ProfileDetailViewModel(this, _repository, _qrService, _transferService, _dialogService, _filePickerService, profile.Id);
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
    private readonly IDialogService _dialogService;
    private readonly IFilePickerService _filePickerService;
    private readonly Guid _profileId;

    [ObservableProperty]
    private AetherisProfile? _profile;

    [ObservableProperty]
    private MeasurementViewModel? _selectedMeasurement;

    [ObservableProperty]
    private FindingViewModel? _selectedFinding;

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

    public ProfileDetailViewModel(AppViewModel app, IProfileRepository repository, IQrCodeService qrService, ITransferService transferService, IDialogService dialogService, IFilePickerService filePickerService, Guid profileId)
    {
        _app = app;
        _repository = repository;
        _qrService = qrService;
        _transferService = transferService;
        _dialogService = dialogService;
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
            ErrorMessage = $"Profil konnte nicht geladen werden: {ex.Message}";
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
            await _app.LoadProfilesAsync(); // Refresh list
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Speichern fehlgeschlagen: {ex.Message}";
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
            ErrorMessage = $"QR-Code-Generierung fehlgeschlagen: {ex.Message}";
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
            ErrorMessage = $"Chunked-QR-Generierung fehlgeschlagen: {ex.Message}";
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
            ErrorMessage = $"QR-Speichern fehlgeschlagen: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (Profile == null) return;
        try
        {
            var json = await _transferService.ExportToJsonAsync(Profile);

            var filePath = await _filePickerService.SaveFileAsync(
                $"Aetheris_{Profile.Profile.Name}_Export",
                ".json",
                new[] { ("JSON", new[] { ".json" }) }
            );
            if (filePath != null)
            {
                await File.WriteAllTextAsync(filePath, json);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Export fehlgeschlagen: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportJsonAsync()
    {
        try
        {
            var filePath = await _filePickerService.OpenFileAsync(
                ".json",
                new[] { ("JSON", new[] { ".json" }) }
            );
            if (filePath == null) return;

            var json = await File.ReadAllTextAsync(filePath);
            var imported = await _transferService.ImportFromJsonAsync(json);
            await _repository.SaveAsync(imported);
            await _app.LoadProfilesAsync();
            _app.SelectProfile(_app.Profiles.First(p => p.Id == imported.Profile.Id));
            await _dialogService.ShowErrorAsync("Import erfolgreich", $"Profil \"{imported.Profile.Name}\" wurde importiert.");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Import fehlgeschlagen", ex.Message);
        }
    }

    [RelayCommand]
    private void ClearError() => ErrorMessage = null;

    [RelayCommand]
    private void Back() => _app.SelectProfile(null);

    [RelayCommand]
    private void AddMeasurement()
    {
        Measurements.Add(new MeasurementViewModel(new Measurement
        {
            Id = Guid.NewGuid(),
            BodyRegion = BodyRegion.Hand,
            TremorType = TremorType.Unknown,
            MeasuredAt = DateTime.UtcNow
        }));
    }

    [RelayCommand]
    private void RemoveMeasurement(MeasurementViewModel? vm)
    {
        if (vm != null) Measurements.Remove(vm);
    }

    [RelayCommand]
    private void AddFinding()
    {
        Findings.Add(new FindingViewModel(new Finding
        {
            Id = Guid.NewGuid(),
            Category = FindingCategory.Unknown,
            Description = "",
            RecordedAt = DateTime.UtcNow
        }));
    }

    [RelayCommand]
    private void RemoveFinding(FindingViewModel? vm)
    {
        if (vm != null) Findings.Remove(vm);
    }

    private void RefreshCollections()
    {
        if (Profile == null) return;
        Measurements.Clear();
        foreach (var m in Profile.Measurements) Measurements.Add(new MeasurementViewModel(m));
        Findings.Clear();
        foreach (var f in Profile.Findings) Findings.Add(new FindingViewModel(f));
    }

    private void ApplyCollectionsToProfile()
    {
        if (Profile == null) return;
        Profile.Measurements = Measurements.Select(vm => vm.ToModel()).ToList();
        Profile.Findings = Findings.Select(vm => vm.ToModel()).ToList();
    }
}

/// <summary>
/// Wrapper for editing a single Measurement.
/// </summary>
public sealed partial class MeasurementViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly Measurement _model;
    private readonly Dictionary<string, List<string>> _errors = new();

    public Guid Id => _model.Id;

    public BodyRegion BodyRegion
    {
        get => _model.BodyRegion;
        set { _model.BodyRegion = value; OnPropertyChanged(); ValidateProperty(nameof(BodyRegion), value); }
    }

    public TremorType TremorType
    {
        get => _model.TremorType;
        set { _model.TremorType = value; OnPropertyChanged(); ValidateProperty(nameof(TremorType), value); }
    }

    public double? FrequencyHz
    {
        get => _model.FrequencyHz;
        set { _model.FrequencyHz = value; OnPropertyChanged(); ValidateProperty(nameof(FrequencyHz), value); }
    }

    public Amplitude? Amplitude
    {
        get => _model.Amplitude;
        set { _model.Amplitude = value; OnPropertyChanged(); ValidateProperty(nameof(Amplitude), value); }
    }

    public int? Severity
    {
        get => _model.Severity;
        set { _model.Severity = value; OnPropertyChanged(); ValidateProperty(nameof(Severity), value); }
    }

    public DateTime MeasuredAt
    {
        get => _model.MeasuredAt;
        set { _model.MeasuredAt = value; OnPropertyChanged(); }
    }

    public string? Conditions
    {
        get => _model.Conditions;
        set { _model.Conditions = value; OnPropertyChanged(); }
    }

    public int? DurationSeconds
    {
        get => _model.DurationSeconds;
        set { _model.DurationSeconds = value; OnPropertyChanged(); ValidateProperty(nameof(DurationSeconds), value); }
    }

    public MeasurementViewModel(Measurement model) => _model = model;

    public Measurement ToModel() => _model;

    public bool HasErrors => _errors.Count > 0;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public System.Collections.IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName != null && _errors.TryGetValue(propertyName, out var errs))
            return (System.Collections.IEnumerable)errs;
        return Array.Empty<string>();
    }

    private void ValidateProperty<T>(string propertyName, T value)
    {
        var errs = new List<string>();

        switch (propertyName)
        {
            case nameof(FrequencyHz):
                if (value is double f && (f < 0.5 || f > 30)) errs.Add("Frequency must be 0.5-30 Hz");
                break;
            case nameof(Severity):
                if (value is int s && (s < 0 || s > 4)) errs.Add("Severity must be 0-4");
                break;
            case nameof(DurationSeconds):
                if (value is int d && (d < 1 || d > 3600)) errs.Add("Duration must be 1-3600 seconds");
                break;
        }

        if (errs.Count > 0)
        {
            _errors[propertyName] = errs;
        }
        else
        {
            _errors.Remove(propertyName);
        }
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Wrapper for editing a single Finding.
/// </summary>
public sealed partial class FindingViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly Finding _model;
    private readonly Dictionary<string, List<string>> _errors = new();

    public Guid Id => _model.Id;

    public FindingCategory Category
    {
        get => _model.Category;
        set { _model.Category = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _model.Description;
        set { _model.Description = value; OnPropertyChanged(); ValidateProperty(nameof(Description), value); }
    }

    public BodyRegion? BodyRegion
    {
        get => _model.BodyRegion;
        set { _model.BodyRegion = value; OnPropertyChanged(); }
    }

    public int? Severity
    {
        get => _model.Severity;
        set { _model.Severity = value; OnPropertyChanged(); ValidateProperty(nameof(Severity), value); }
    }

    public DateTime RecordedAt
    {
        get => _model.RecordedAt;
        set { _model.RecordedAt = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> Tags { get; } = new();

    public FindingViewModel(Finding model)
    {
        _model = model;
        foreach (var tag in model.Tags) Tags.Add(tag);
        Tags.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Tags));
    }

    public Finding ToModel()
    {
        _model.Tags = Tags.ToList();
        return _model;
    }

    public bool HasErrors => _errors.Count > 0;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public System.Collections.IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName != null && _errors.TryGetValue(propertyName, out var errs))
            return (System.Collections.IEnumerable)errs;
        return Array.Empty<string>();
    }

    private void ValidateProperty(string propertyName, object? value)
    {
        var errs = new List<string>();

        switch (propertyName)
        {
            case nameof(Description):
                if (value is string s && string.IsNullOrWhiteSpace(s)) errs.Add("Description is required");
                break;
            case nameof(Severity):
                if (value is int sev && (sev < 0 || sev > 10)) errs.Add("Severity must be 0-10");
                break;
        }

        if (errs.Count > 0) _errors[propertyName] = errs;
        else _errors.Remove(propertyName);
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
}
