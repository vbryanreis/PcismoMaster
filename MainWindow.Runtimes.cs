using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>FERRAMENTAS · Drivers: verificação das bibliotecas Visual C++ e DirectX (sem instalar nada).</summary>
public partial class MainWindow
{
    private async void InitRuntimes()
    {
        RuntimeNote.Text = "Verificando…";
        var list = await Task.Run(RuntimeCheck.Read);
        RuntimeRows.Children.Clear();
        foreach (var r in list)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = r.Name, FontWeight = FontWeights.SemiBold });
            if (r.Note.Length > 0) name.Children.Add(new TextBlock { Text = "  " + r.Note, Foreground = (Brush)FindResource("Faint"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            grid.Children.Add(name);
            var x86 = RuntimeBadge("32 bits", r.X86); Grid.SetColumn(x86, 1); grid.Children.Add(x86);
            var x64 = RuntimeBadge("64 bits", r.X64); Grid.SetColumn(x64, 2); grid.Children.Add(x64);
            RuntimeRows.Children.Add(grid);
        }
        int missing = list.Sum(r => (r.X86 ? 0 : 1) + (r.X64 ? 0 : 1));
        RuntimeNote.Text = missing == 0 ? "Tudo instalado." : $"{missing} faltando. Jogos que precisam de uma biblioteca que falta costumam avisar \"MSVCP…dll\" ou \"d3dx9_…dll não encontrado\".";
    }

    private Border RuntimeBadge(string label, bool ok)
    {
        var b = new Border { Style = (Style)FindResource("MaintBadge"), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0),
            Background = new SolidColorBrush(ok ? Color.FromArgb(0x26, 0x4E, 0xD1, 0x7A) : Color.FromArgb(0x26, 0xFF, 0x5C, 0x5C)),
            Child = new TextBlock { Text = $"{label}: {(ok ? "instalado" : "falta")}" } };
        System.Windows.Documents.TextElement.SetForeground(b, (Brush)FindResource(ok ? "ChartGpu" : "TempCrit"));
        return b;
    }

    private void BtnRuntimeCheck_Click(object sender, RoutedEventArgs e) => InitRuntimes();
}
