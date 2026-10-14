using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SOGMAPanelManager;

public partial class MainWindow
{
    private void MultiProjects_Click(object sender, RoutedEventArgs e)
    {
        var selected = new List<string>();
        var w = new Window { Title = "Proyectos .sogma · Lotes y combinación", Owner = this,
            Width = 680, Height = 510, MinWidth = 540, MinHeight = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(29,31,35)) };
        var root = new DockPanel { Margin = new Thickness(16) };
        w.Content = root;
        var top = new TextBlock { Text = "Organiza archivos .sogma: orden superior primero", Foreground = Brushes.White, Margin = new Thickness(0,0,0,10) };
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var bottom = new StackPanel { Margin = new Thickness(0,12,0,0) };
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var status = new TextBlock { Text = "Selecciona dos o más archivos para combinar, o uno o más para exportar.", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
        var commands = new WrapPanel { Margin = new Thickness(0,0,0,9) };
        bottom.Children.Add(commands); bottom.Children.Add(status);
        var list = new ListBox { Background = new SolidColorBrush(Color.FromRgb(39,42,47)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        root.Children.Add(list);
        void Refresh()
        {
            list.Items.Clear();
            for (int i=0;i<selected.Count;i++) list.Items.Add($"{i+1:D2} · {Path.GetFileName(selected[i])}");
        }
        Button Btn(string title, Action act)
        {
            var b = new Button { Content=title, Margin=new Thickness(0,0,8,8), Padding=new Thickness(12,7,12,7) };
            b.Click += (_,_) => act(); commands.Children.Add(b); return b;
        }
        Btn("+ Agregar .sogma", () => {
            var d = new OpenFileDialog { Filter="Proyectos SOGMA|*.sogma", Multiselect=true };
            if(d.ShowDialog(w)==true) { foreach(var p in d.FileNames) if(!selected.Contains(p,StringComparer.OrdinalIgnoreCase)) selected.Add(p); Refresh(); }
        });
        Btn("Quitar", () => { if(list.SelectedIndex>=0){selected.RemoveAt(list.SelectedIndex); Refresh();} });
        Btn("↑", () => { int i=list.SelectedIndex; if(i>0){(selected[i-1],selected[i])=(selected[i],selected[i-1]);Refresh();list.SelectedIndex=i-1;} });
        Btn("↓", () => { int i=list.SelectedIndex; if(i>=0 && i<selected.Count-1){(selected[i+1],selected[i])=(selected[i],selected[i+1]);Refresh();list.SelectedIndex=i+1;} });
        Btn("Exportar lote PNG", () => {
            if(selected.Count==0) return;
            var d=new OpenFolderDialog { Title="Destino de capítulos exportados" };
            if(d.ShowDialog(w)!=true) return;
            var inputs=selected.ToArray(); int done=0; var errors=new List<string>();
            foreach(var src in inputs) {
                try { status.Text=$"Exportando {++done}/{inputs.Length}: {Path.GetFileName(src)}"; w.Dispatcher.Invoke(() => {},System.Windows.Threading.DispatcherPriority.Background);
                    ExportSogmaArchive(src,Path.Combine(d.FolderName,Path.GetFileNameWithoutExtension(src))); }
                catch(Exception ex){errors.Add(Path.GetFileName(src)+": "+ex.Message);}
            }
            status.Text=$"Lote finalizado: {done-errors.Count}/{done} correctos. "+string.Join(" | ",errors);
            MessageBox.Show(w,status.Text,"SOGMA",MessageBoxButton.OK,errors.Count==0?MessageBoxImage.Information:MessageBoxImage.Warning);
        });
        Btn("Combinar en .sogma", () => {
            if(selected.Count<2){MessageBox.Show(w,"Selecciona al menos 2 proyectos.");return;}
            var d=new SaveFileDialog { Filter="Proyecto SOGMA|*.sogma",DefaultExt=".sogma",FileName="Proyecto_combinado.sogma" };
            if(d.ShowDialog(w)!=true)return;
            if(selected.Any(x=>Path.GetFullPath(x).Equals(Path.GetFullPath(d.FileName),StringComparison.OrdinalIgnoreCase))) { MessageBox.Show(w,"No puedes sobrescribir los proyectos de origen.");return; }
            try { CombineSogmaArchives(selected,d.FileName);status.Text="Proyecto combinado guardado: "+d.FileName;MessageBox.Show(w,status.Text); }
            catch(Exception ex){status.Text="Error: "+ex.Message;MessageBox.Show(w,status.Text,"SOGMA",MessageBoxButton.OK,MessageBoxImage.Error);}
        });
        w.ShowDialog();
    }

    private static (ZipArchive Archive, FileStream Stream, SogmaProjectData Project) ReadSogma(string path)
    {
        var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        try {
            var zip=new ZipArchive(stream,ZipArchiveMode.Read);
            try {
                using var data=zip.GetEntry("project.json")?.Open()??throw new InvalidDataException("Falta project.json");
                var model=JsonSerializer.Deserialize<SogmaProjectData>(data,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Manifiesto incorrecto");
                if(model.FormatVersion>4) throw new InvalidDataException("Formato no compatible");
                return (zip,stream,model);
            }catch {zip.Dispose();throw;}
        }catch {stream.Dispose();throw;}
    }

    private static void CombineSogmaArchives(IReadOnlyList<string> sources,string destination)
    {
        string tmp=destination+".building";
        if(File.Exists(tmp))File.Delete(tmp);
        var combined=new SogmaProjectData { FormatVersion=4,ProjectName=Path.GetFileNameWithoutExtension(destination),SavedAtUtc=DateTime.UtcNow };
        try {
            using(var outZip=ZipFile.Open(tmp,ZipArchiveMode.Create)) {
                int n=0;
                foreach(string source in sources) {
                    var (zip,stream,project)=ReadSogma(source);
                    using(stream) using(zip) {
                        if(n==0) {
                            combined.EnableWheelScale=project.EnableWheelScale; combined.InheritScale=project.InheritScale;
                            combined.ShowPreviousReference=project.ShowPreviousReference; combined.SolidReference=project.SolidReference;
                            combined.GlowReference=project.GlowReference; combined.ReferenceOpacity=project.ReferenceOpacity;
                            combined.ShowCenterGuides=project.ShowCenterGuides; combined.ShowSafeArea=project.ShowSafeArea;
                            combined.ShowCanvasLimits=project.ShowCanvasLimits; combined.ShowOuterFrame=project.ShowOuterFrame; combined.ShowInnerFrame=project.ShowInnerFrame;
                        }
                        var chapter=Path.GetFileNameWithoutExtension(source);
                        foreach(var item in project.Images) {
                            var entry=zip.GetEntry(item.ArchivePath)??throw new InvalidDataException("Falta "+item.ArchivePath);
                            string path=$"images/{++n:D6}{Path.GetExtension(item.FileName).ToLowerInvariant()}";
                            var dest=outZip.CreateEntry(path,CompressionLevel.NoCompression);
                            using(var input=entry.Open()) using(var output=dest.Open())input.CopyTo(output);
                            combined.Images.Add(new SogmaProjectImage {
                                FileName=$"{chapter} - {item.FileName}",ArchivePath=path,ChapterName=chapter,
                                Scale=item.Scale,IsAdjusted=item.IsAdjusted,LastModifiedUtc=item.LastModifiedUtc,
                                PremiereNote=item.PremiereNote,EditRegions=item.EditRegions?.Select(x=>x.Clone()).ToList()??new()
                            });
                        }
                    }
                }
                var json=outZip.CreateEntry("project.json",CompressionLevel.Optimal);
                using var file=json.Open();JsonSerializer.Serialize(file,combined,new JsonSerializerOptions{WriteIndented=true});
            }
            File.Move(tmp,destination,true);
        }catch {if(File.Exists(tmp))File.Delete(tmp);throw;}
    }

    private static void ExportSogmaArchive(string source,string destination)
    {
        var (zip,stream,project)=ReadSogma(source);
        using(stream) using(zip) {
            Directory.CreateDirectory(destination);
            for(int i=0;i<project.Images.Count;i++) {
                var p=project.Images[i];
                var entry=zip.GetEntry(p.ArchivePath)??throw new InvalidDataException("Falta "+p.ArchivePath);
                var temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+Path.GetExtension(p.FileName));
                try {
                    using(var from=entry.Open())using(var to=File.Create(temp))from.CopyTo(to);
                    var item=new ImageItem { FilePath=temp,FileName=p.FileName,Thumbnail= new WriteableBitmap(1,1,96,96,PixelFormats.Bgra32,null),Scale=p.Scale,IsAdjusted=p.IsAdjusted };
                    if(p.EditRegions!=null)item.EditRegions.AddRange(p.EditRegions.Select(r=>r.Clone()));
                    string filename=$"{i+1:D5}_{CleanSogmaName(Path.GetFileNameWithoutExtension(p.FileName))}.png";
                    ExportPanelToPng(item,Path.Combine(destination,filename));
                } finally { if(File.Exists(temp)) File.Delete(temp); }
            }
        }
    }
    private static string CleanSogmaName(string name) => string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
}
