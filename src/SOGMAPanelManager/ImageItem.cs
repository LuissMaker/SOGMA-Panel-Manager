using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace SOGMAPanelManager;

public sealed class ImageItem : INotifyPropertyChanged
{
    private double _scale = 1.0;
    private bool _isAdjusted;
    private DateTime _lastModifiedUtc = DateTime.UtcNow;
    private BitmapSource? _thumbnail;
    private string _premiereNote = "";

    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required BitmapSource Thumbnail
    {
        get => _thumbnail!;
        set { _thumbnail = value; OnPropertyChanged(); }
    }

    public List<PanelEditRegion> EditRegions { get; } = new();

    public string PremiereNote
    {
        get => _premiereNote;
        set
        {
            value ??= "";
            if (_premiereNote == value) return;
            _premiereNote = value;
            _lastModifiedUtc = DateTime.UtcNow;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPremiereNote));
            OnPropertyChanged(nameof(PremiereNoteBadge));
            OnPropertyChanged(nameof(LastModifiedUtc));
        }
    }

    public bool HasPremiereNote => !string.IsNullOrWhiteSpace(PremiereNote);
    public string PremiereNoteBadge => HasPremiereNote ? "NOTE" : "";
    public bool HasEdits => EditRegions.Count > 0;
    public string EditBadge => HasEdits ? $"✎ {EditRegions.Count}" : "";

    public double Scale
    {
        get => _scale;
        set
        {
            double clamped = Math.Clamp(value, 0.10, 4.00);
            if (Math.Abs(_scale - clamped) < 0.0001) return;
            _scale = clamped;
            _lastModifiedUtc = DateTime.UtcNow;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastModifiedUtc));
        }
    }

    public bool IsAdjusted
    {
        get => _isAdjusted;
        set
        {
            if (_isAdjusted == value) return;
            _isAdjusted = value;
            _lastModifiedUtc = DateTime.UtcNow;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastModifiedUtc));
        }
    }

    public DateTime LastModifiedUtc
    {
        get => _lastModifiedUtc;
        set
        {
            DateTime normalized = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            if (_lastModifiedUtc == normalized) return;
            _lastModifiedUtc = normalized;
            OnPropertyChanged();
        }
    }

    public void NotifyEditsChanged()
    {
        _lastModifiedUtc = DateTime.UtcNow;
        OnPropertyChanged(nameof(HasEdits));
        OnPropertyChanged(nameof(EditBadge));
        OnPropertyChanged(nameof(LastModifiedUtc));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
