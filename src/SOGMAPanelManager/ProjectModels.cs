namespace SOGMAPanelManager;

public sealed class SogmaProjectData
{
    public int FormatVersion { get; set; } = 4;
    public string ProjectName { get; set; } = "Proyecto SOGMA";
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    public int SelectedIndex { get; set; }
    public bool EnableWheelScale { get; set; } = true;
    public bool InheritScale { get; set; } = true;
    public bool ShowPreviousReference { get; set; } = true;
    public bool SolidReference { get; set; } = true;
    public bool OutlineOnly { get; set; }
    public bool GlowReference { get; set; }
    public double ReferenceOpacity { get; set; } = 28;
    public bool ShowCenterGuides { get; set; } = true;
    public bool ShowSafeArea { get; set; } = true;
    public bool ShowCanvasLimits { get; set; } = true;
    public bool ShowOuterFrame { get; set; }
    public bool ShowInnerFrame { get; set; }
    public List<SogmaProjectImage> Images { get; set; } = new();
}

public sealed class SogmaProjectImage
{
    public string FileName { get; set; } = "";
    public string ArchivePath { get; set; } = "";
    public double Scale { get; set; } = 1.0;
    public bool IsAdjusted { get; set; }
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
    public string PremiereNote { get; set; } = "";
    public List<PanelEditRegion> EditRegions { get; set; } = new();
}

public sealed class PanelEditRegion
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Effect { get; set; } = "Pixelado";
    public int Strength { get; set; } = 14;

    public PanelEditRegion Clone() => new()
    {
        X = X, Y = Y, Width = Width, Height = Height, Effect = Effect, Strength = Strength
    };
}
