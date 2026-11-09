using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>
/// TWEAKING como passo a passo depois de formatar: o menu fica na ordem de <see cref="TweakSteps"/>, numerado (01, 02…),
/// a aba 1 (Começar aqui) mostra a ordem inteira e cada aba termina com o botão do próximo passo.
/// </summary>
public partial class MainWindow
{
    private static readonly (string Nav, string Page, string Title, string What)[] TweakSteps =
    {
        ("NavOptStart", "PageOptStart", "Começar aqui", "A ordem dos passos e o atalho que aplica o pacote completo de uma vez."),
        ("NavOptUpdate", "PageOptUpdate", "Windows Update", "Veja o que falta instalar e se tem problema conhecido; segure drivers ou o Update se precisar."),
        ("NavDriver", "PageDriver", "Drivers", "Driver de vídeo, chipset e BIOS atualizados antes de qualquer ajuste."),
        ("NavOptDebloat", "PageOptDebloat", "Debloat", "Tira apps, propaganda, telemetria e IA que vêm com o Windows."),
        ("NavOptGeral", "PageOptGeral", "Geral", "Modo de Jogo, gravação de jogos, mouse, teclado e Windows Update."),
        ("NavOptPriv", "PageOptPriv", "Privacidade", "Telemetria, publicidade, permissões dos apps e IA."),
        ("NavOptPower", "PageOptPower", "Energia", "Limitação de energia, timers e dynamic tick."),
        ("NavOptNet", "PageOptNet", "Rede", "Placa de rede, NetBIOS, IPv6 e prioridade dos jogos."),
        ("NavOptAdv", "PageOptAdv", "Avançado", "Memória, SysMain e prioridade do processador."),
        ("NavOptGpu", "PageOptGpu", "GPU e monitor", "Taxa de atualização máxima, agendamento de GPU e PCI Express."),
        ("NavOptSvc", "PageOptSvc", "Serviços", "Revise os serviços desligados e ligue de volta o que você usa."),
        ("NavOptMitig", "PageOptMitig", "Mitigações", "Opcional, só para testar: Spectre e Meltdown."),
        ("NavOptBios", "PageOptBios", "BIOS", "Opcional: ajustes do firmware, feitos à mão na tela da BIOS."),
        ("NavOptLook", "PageOptLook", "Aparência", "Efeitos visuais, tema, menu de contexto e Explorer."),
        ("NavOptClean", "PageOptClean", "Limpeza", "No final: apaga temporários e libera memória."),
        ("NavOptProfiles", "PageOptProfiles", "Perfis", "Salve tudo como perfil para repetir na próxima formatação."),
    };

    private readonly HashSet<string> _stepFooters = new();

    /// <summary>Põe o menu TWEAKING na ordem dos passos e numera cada aba (o nome continua sendo o último texto, usado no título).</summary>
    private void SetupTweakSteps()
    {
        var navs = TweakSteps.Select(s => FindName(s.Nav) as RadioButton).ToList();
        if (navs.Any(n => n == null)) return;
        foreach (var n in navs) GrpTweakItems.Children.Remove(n);
        for (int i = 0; i < navs.Count; i++)
        {
            GrpTweakItems.Children.Insert(i, navs[i]);
            if (navs[i]!.Content is StackPanel sp)
                sp.Children.Insert(1, new TextBlock { Text = (i + 1).ToString("00"), FontFamily = (FontFamily)FindResource("Mono"), FontSize = 10.5,
                    Foreground = (Brush)FindResource("Faint"), Width = 22, VerticalAlignment = VerticalAlignment.Center });
        }
    }

    private void GoToStep(int i)
    {
        if (i < 0 || i >= TweakSteps.Length) return;
        if (FindName(TweakSteps[i].Page) is ScrollViewer sv) sv.ScrollToTop();
        if (FindName(TweakSteps[i].Nav) is RadioButton nav) nav.IsChecked = true;
    }

    /// <summary>No fim de cada aba do passo a passo: PASSO X DE N, voltar e PRÓXIMO. Montado na primeira vez que a aba abre.</summary>
    private void AddStepFooter(string page)
    {
        int i = Array.FindIndex(TweakSteps, s => s.Page == page);
        if (i < 0 || !_stepFooters.Add(page) || FindName(page) is not ScrollViewer { Content: Panel content }) return;
        var row = new DockPanel { LastChildFill = false };
        var label = new TextBlock { Text = $"PASSO {i + 1} DE {TweakSteps.Length}", FontFamily = (FontFamily)FindResource("Mono"), FontSize = 11,
            Foreground = (Brush)FindResource("Faint"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 8) };
        row.Children.Add(label);
        if (i > 0)
        {
            var back = TweakButton("← " + TweakSteps[i - 1].Title, false, (_, _) => GoToStep(i - 1));
            row.Children.Add(back);
        }
        if (i < TweakSteps.Length - 1)
        {
            var next = TweakButton($"Próximo: {i + 2:00} · {TweakSteps[i + 1].Title} →", true, (_, _) => GoToStep(i + 1));
            next.ToolTip = TweakSteps[i + 1].What;
            DockPanel.SetDock(next, Dock.Right);
            row.Children.Add(next);
        }
        else row.Children.Add(OptText("Fim do passo a passo. Reinicie o PC para tudo valer.", "Muted", 12, 8));
        content.Children.Add(new Border { BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 24, 0, 0), Padding = new Thickness(0, 16, 0, 0), Child = row });
    }

    /// <summary>Aba 1: a ordem dos passos (cada um com ABRIR) e o atalho do PC recém-formatado.</summary>
    private void BuildOptStart(Panel host)
    {
        TweakCard(host, "", "Passo a passo depois de formatar",
            "Siga as abas na ordem, de cima para baixo. Em cada uma, leia os avisos, ligue o que fizer sentido para você e clique em PRÓXIMO no final. " +
            "Tudo pode ser desfeito, e antes da primeira mudança o PcismoMaster cria um ponto de restauração. Com pressa? O atalho logo abaixo aplica o pacote completo de uma vez.", out var body);
        for (int i = 1; i < TweakSteps.Length; i++)
        {
            int step = i;
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var num = new TextBlock { Text = (i + 1).ToString("00"), FontFamily = (FontFamily)FindResource("Mono"), FontSize = 12,
                Foreground = (Brush)FindResource("Faint"), VerticalAlignment = VerticalAlignment.Center };
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = TweakSteps[i].Title, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Fg") });
            text.Children.Add(OptText(TweakSteps[i].What, "Muted", 12));
            var open = TweakButton("Abrir", false, (_, _) => GoToStep(step));
            open.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(text, 1);
            Grid.SetColumn(open, 2);
            grid.Children.Add(num); grid.Children.Add(text); grid.Children.Add(open);
            body.Children.Add(grid);
        }
        BuildOptFormat(host);
    }
}
