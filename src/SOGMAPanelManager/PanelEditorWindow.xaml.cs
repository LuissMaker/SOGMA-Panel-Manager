using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SOGMAPanelManager;

public partial class PanelEditorWindow : Window
{
    private readonly BitmapSource _source;
    private readonly List<PanelEditRegion> _regions;
    private PanelEditRegion? _selected;
    private Point _dragStart;
    private PanelEditRegion? _dragOriginal;
    private DragMode _dragMode;
    private bool _updatingControls;

    private enum DragMode { None, Create, Move, Resize }

    public IReadOnlyList<PanelEditRegion> ResultRegions => _regions;
    public string ResultPremiereNote => PremiereNoteBox.Text.Trim();

    public PanelEditorWindow(ImageItem item, BitmapSource source)
    {
        InitializeComponent();
        _source = source;
        _regions = item.EditRegions.Select(r => r.Clone()).ToList();
        PanelNameText.Text = item.FileName;
        PremiereNoteBox.Text = item.PremiereNote;
        PreviewImage.Source = ImageEffects.Apply(_source, _regions);
        Loaded += (_, _) => RedrawOverlay();
        SizeChanged += (_, _) => RedrawOverlay();
        PreviewKeyDown += PanelEditorWindow_PreviewKeyDown;
        UpdateRegionCount();
    }

    private Rect GetImageRect()
    {
        double w = OverlayCanvas.ActualWidth, h = OverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return Rect.Empty;
        double scale = Math.Min(w / _source.PixelWidth, h / _source.PixelHeight);
        double dw = _source.PixelWidth * scale, dh = _source.PixelHeight * scale;
        return new Rect((w - dw) / 2, (h - dh) / 2, dw, dh);
    }

    private Point ToNormalized(Point p)
    {
        Rect r = GetImageRect();
        if (r.IsEmpty) return new Point();
        return new Point(Math.Clamp((p.X - r.X) / r.Width, 0, 1), Math.Clamp((p.Y - r.Y) / r.Height, 0, 1));
    }

    private Rect ToDisplayRect(PanelEditRegion region)
    {
        Rect r = GetImageRect();
        return new Rect(r.X + region.X * r.Width, r.Y + region.Y * r.Height,
                        region.Width * r.Width, region.Height * r.Height);
    }

    private PanelEditRegion? HitTestRegion(Point p, out bool resize)
    {
        resize = false;
        for (int i = _regions.Count - 1; i >= 0; i--)
        {
            Rect dr = ToDisplayRect(_regions[i]);
            var handle = new Rect(dr.Right - 14, dr.Bottom - 14, 20, 20);
            if (handle.Contains(p)) { resize = true; return _regions[i]; }
            if (dr.Contains(p)) return _regions[i];
        }
        return null;
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Point p = e.GetPosition(OverlayCanvas);
        Rect imageRect = GetImageRect();
        if (!imageRect.Contains(p)) return;

        bool resize;
        PanelEditRegion? hit = HitTestRegion(p, out resize);
        _dragStart = ToNormalized(p);

        if (hit is not null)
        {
            SelectRegion(hit);
            _dragOriginal = hit.Clone();
            _dragMode = resize ? DragMode.Resize : DragMode.Move;
        }
        else
        {
            string effect = (EffectCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Pixelado";
            var region = new PanelEditRegion
            {
                X = _dragStart.X, Y = _dragStart.Y, Width = 0.001, Height = 0.001,
                Effect = effect, Strength = (int)Math.Round(StrengthSlider.Value)
            };
            _regions.Add(region);
            SelectRegion(region);
            _dragOriginal = region.Clone();
            _dragMode = DragMode.Create;
        }

        OverlayCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void Overlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode == DragMode.None || _selected is null || e.LeftButton != MouseButtonState.Pressed) return;
        Point now = ToNormalized(e.GetPosition(OverlayCanvas));

        if (_dragMode == DragMode.Create)
        {
            _selected.X = Math.Min(_dragStart.X, now.X);
            _selected.Y = Math.Min(_dragStart.Y, now.Y);
            _selected.Width = Math.Max(0.002, Math.Abs(now.X - _dragStart.X));
            _selected.Height = Math.Max(0.002, Math.Abs(now.Y - _dragStart.Y));
        }
        else if (_dragMode == DragMode.Move && _dragOriginal is not null)
        {
            double dx = now.X - _dragStart.X, dy = now.Y - _dragStart.Y;
            _selected.X = Math.Clamp(_dragOriginal.X + dx, 0, 1 - _selected.Width);
            _selected.Y = Math.Clamp(_dragOriginal.Y + dy, 0, 1 - _selected.Height);
        }
        else if (_dragMode == DragMode.Resize && _dragOriginal is not null)
        {
            _selected.Width = Math.Clamp(now.X - _selected.X, 0.01, 1 - _selected.X);
            _selected.Height = Math.Clamp(now.Y - _selected.Y, 0.01, 1 - _selected.Y);
        }
        RedrawOverlay();
    }

    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.None) return;
        OverlayCanvas.ReleaseMouseCapture();
        _dragMode = DragMode.None;
        _dragOriginal = null;
        RemoveTinyRegions();
        RefreshPreview();
        e.Handled = true;
    }

    private void RemoveTinyRegions()
    {
        _regions.RemoveAll(r => r.Width < 0.006 || r.Height < 0.006);
        if (_selected is not null && !_regions.Contains(_selected)) _selected = null;
        UpdateRegionCount();
        RedrawOverlay();
    }

    private void SelectRegion(PanelEditRegion region)
    {
        _selected = region;
        _updatingControls = true;
        try
        {
            foreach (ComboBoxItem item in EffectCombo.Items)
                if (string.Equals(item.Content?.ToString(), region.Effect, StringComparison.OrdinalIgnoreCase))
                    EffectCombo.SelectedItem = item;
            StrengthSlider.Value = region.Strength;
        }
        finally { _updatingControls = false; }
        RedrawOverlay();
    }

    private void RedrawOverlay()
    {
        if (OverlayCanvas is null) return;
        OverlayCanvas.Children.Clear();
        foreach (var region in _regions)
        {
            Rect r = ToDisplayRect(region);
            bool sel = ReferenceEquals(region, _selected);
            var rect = new Rectangle
            {
                Width = Math.Max(1, r.Width), Height = Math.Max(1, r.Height),
                Stroke = new SolidColorBrush(sel ? Color.FromRgb(109,143,216) : Color.FromRgb(220,220,220)),
                StrokeThickness = sel ? 2.2 : 1.2,
                Fill = new SolidColorBrush(Color.FromArgb(sel ? (byte)30 : (byte)14, 109,143,216)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(rect, r.X); Canvas.SetTop(rect, r.Y); OverlayCanvas.Children.Add(rect);
            if (sel)
            {
                var handle = new Rectangle { Width = 10, Height = 10, Fill = Brushes.White, Stroke = new SolidColorBrush(Color.FromRgb(109,143,216)), StrokeThickness = 2, IsHitTestVisible = false };
                Canvas.SetLeft(handle, r.Right - 5); Canvas.SetTop(handle, r.Bottom - 5); OverlayCanvas.Children.Add(handle);
            }
        }
    }

    private void RefreshPreview()
    {
        PreviewImage.Source = ImageEffects.Apply(_source, _regions);
        UpdateRegionCount();
        RedrawOverlay();
    }

    private void UpdateRegionCount() => RegionCountText.Text = _regions.Count == 1 ? "1 zona" : $"{_regions.Count} zonas";

    private void EffectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingControls || _selected is null || !IsLoaded) return;
        _selected.Effect = (EffectCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Pixelado";
        RefreshPreview();
    }

    private void StrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (StrengthText is null) return;
        StrengthText.Text = $"{e.NewValue:0}";
        if (_updatingControls || _selected is null || !IsLoaded) return;
        _selected.Strength = (int)Math.Round(e.NewValue);
        RefreshPreview();
    }

    private void DeleteRegion_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _regions.Remove(_selected); _selected = null; RefreshPreview();
    }

    private void ClearRegions_Click(object sender, RoutedEventArgs e)
    {
        _regions.Clear(); _selected = null; RefreshPreview();
    }

    private void PanelEditorWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) { DeleteRegion_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.Escape && _dragMode != DragMode.None)
        {
            _dragMode = DragMode.None; OverlayCanvas.ReleaseMouseCapture(); e.Handled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
