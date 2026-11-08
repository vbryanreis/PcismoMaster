using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ResetExplode;

/// <summary>Menu lateral: conta do GitHub (Device Flow) e atualização automática pelas releases.</summary>
public partial class MainWindow
{
    private CancellationTokenSource? _ghLoginCts;
    private PendingUpdate? _pendingUpdate;
    private DispatcherTimer? _updTimer;
    private bool _updBusy;

    private void InitAccount()
    {
        if (!GitHubAuth.Available)
        {
            GhSub.Text = "Login indisponível nesta versão";
            BtnGhLogin.IsEnabled = false;
        }
        else if (_settings.GitHubToken is { } token)
        {
            ShowGhUser(_settings.GitHubLogin ?? "", null);
            _ = RefreshGhUserAsync(token);
        }
        else ShowGate(true); // sem conta: o app fica bloqueado até entrar

        if (AppUpdater.Supported && _settings.AutoUpdate)
        {
            // Primeira procura 20 s depois de abrir (não disputa com a detecção do hardware), depois a cada 6 h.
            _updTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _updTimer.Tick += async (_, _) => { _updTimer.Interval = TimeSpan.FromHours(6); await CheckUpdateAsync(false); };
            _updTimer.Start();
        }
    }

    // ---------- Conta do GitHub ----------

    private async Task RefreshGhUserAsync(string token)
    {
        try
        {
            var user = await GitHubAuth.GetUserAsync(token, CancellationToken.None);
            // Quem já entrou continua entrando: só "Sair" (ou perder o arquivo de configurações) pede o login de novo.
            if (user == null) { DriverClean.Log("GitHub recusou o token salvo (401); login mantido."); return; }
            _settings.GitHubLogin = user.Login;
            _settings.Save();
            ShowGhUser(user.Name, user.AvatarUrl);
        }
        catch { /* sem internet: fica com o login salvo */ }
    }

    private void ShowGhUser(string name, string? avatarUrl)
    {
        GhName.Text = name.Length > 0 ? name : "Conectado";
        GhSub.Text = "@" + (_settings.GitHubLogin ?? "") + " · GitHub";
        BtnGhLogin.Content = "Sair";
        GateStatus.Text = "";
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(avatarUrl + (avatarUrl.Contains('?') ? "&" : "?") + "s=56");
                img.DecodePixelWidth = 56;
                img.EndInit();
                GhAvatar.Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
                GhAvatar.Visibility = Visibility.Visible;
            }
            catch { }
        }
    }

    private void SignOutGh(string? sub = null)
    {
        _settings.GitHubToken = null;
        _settings.GitHubLogin = null;
        _settings.Save();
        GhName.Text = "Sem conta";
        GhSub.Text = sub ?? "Entrar com o GitHub";
        BtnGhLogin.Content = "Entrar";
        BtnGateLogin.Content = "Entrar com o GitHub";
        GhAvatar.Visibility = Visibility.Collapsed;
        GateStatus.Text = sub ?? "";
        ShowGate(true);
    }

    /// <summary>Tela de entrada por cima de tudo (só a barra de título fica livre). Não aparece em build sem Client ID.</summary>
    private void ShowGate(bool show) =>
        LoginGate.Visibility = show && GitHubAuth.Available ? Visibility.Visible : Visibility.Collapsed;

    private string _ghVerifyUri = "https://github.com/login/device";

    private void BtnGhCopy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(GateCode.Text); BtnGateCopy.Content = "Copiado ✓"; } catch { }
    }

    private void BtnGhOpen_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(_ghVerifyUri) { UseShellExecute = true }); } catch { }
    }

    private void SetLoginStatus(string text) { GhSub.Text = text; GateStatus.Text = text; }

    private async void BtnGhLogin_Click(object sender, RoutedEventArgs e)
    {
        if (_ghLoginCts != null) { _ghLoginCts.Cancel(); return; }       // "Cancelar" durante o login
        if (_settings.GitHubToken != null)                                 // "Sair"
        {
            if (MessageBox.Show(this, "Sair da conta do GitHub? O PcismoMaster fica bloqueado até você entrar de novo.",
                    "Conta do GitHub", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) SignOutGh();
            return;
        }

        _ghLoginCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        BtnGhLogin.Content = BtnGateLogin.Content = "Cancelar";
        SetLoginStatus("Pedindo o código ao GitHub…");
        try
        {
            var code = await GitHubAuth.StartAsync(_ghLoginCts.Token);
            _ghVerifyUri = code.VerificationUri;
            BtnGateCopy.Content = "Copiar código";
            GhCode.Text = GateCode.Text = code.UserCode;
            GhCodeBox.Visibility = LoginGate.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            GateCodeBox.Visibility = Visibility.Visible;
            SetLoginStatus("Aguardando você autorizar no GitHub…");
            try { Clipboard.SetText(code.UserCode); } catch { }
            try { Process.Start(new ProcessStartInfo(code.VerificationUri) { UseShellExecute = true }); } catch { }

            string token = await GitHubAuth.WaitForTokenAsync(code, _ghLoginCts.Token);
            var user = await GitHubAuth.GetUserAsync(token, _ghLoginCts.Token) ?? throw new InvalidOperationException("o GitHub não aceitou o token");
            _settings.GitHubToken = token;
            _settings.GitHubLogin = user.Login;
            _settings.Save();
            ShowGhUser(user.Name, user.AvatarUrl);
            ShowGate(false);
            Toast("GitHub", $"Conectado como @{user.Login}.", "");
        }
        catch (OperationCanceledException) { SignOutGh(); }
        catch (Exception ex) { SignOutGh(ex is System.Net.Http.HttpRequestException ? "Sem conexão com o GitHub. Confira a internet e tente de novo." : ex.Message); }
        finally
        {
            GhCodeBox.Visibility = GateCodeBox.Visibility = Visibility.Collapsed;
            _ghLoginCts.Dispose();
            _ghLoginCts = null;
        }
    }

    // ---------- Atualização ----------

    private async void BtnCheckUpd_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!AppUpdater.Supported)
        {
            ShowUpd("A atualização automática só funciona no .exe publicado (este é um build de teste).", false);
            return;
        }
        await CheckUpdateAsync(true);
    }

    private async Task CheckUpdateAsync(bool manual)
    {
        if (_updBusy || _pendingUpdate != null) { if (_pendingUpdate != null) UpdBox.Visibility = Visibility.Visible; return; }
        _updBusy = true;
        try
        {
            if (manual) ShowUpd("Procurando atualização…", false);
            var progress = new Progress<double>(p => ShowUpd($"Baixando a nova versão… {p:P0}", false));
            var upd = await AppUpdater.CheckAndDownloadAsync(progress, CancellationToken.None);
            if (upd == null)
            {
                if (manual) ShowUpd($"Você já está na última versão (v{AppUpdater.Current.ToString(3)}).", false);
                else UpdBox.Visibility = Visibility.Collapsed;
                return;
            }
            _pendingUpdate = upd;
            ShowUpd($"A versão {upd.Tag} foi baixada e está pronta.", true);
            Toast("Atualização pronta", $"PcismoMaster {upd.Tag} baixado. Clique em \"Reiniciar e atualizar\" no menu lateral.", "");
        }
        catch (Exception ex)
        {
            if (manual) ShowUpd("Não deu para atualizar: " + ex.Message, false);
            else UpdBox.Visibility = Visibility.Collapsed;
        }
        finally { _updBusy = false; }
    }

    private void ShowUpd(string text, bool canInstall)
    {
        UpdText.Text = text;
        BtnUpdate.Visibility = canInstall ? Visibility.Visible : Visibility.Collapsed;
        UpdBox.Visibility = Visibility.Visible;
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate == null) return;
        try
        {
            AppUpdater.Install(_pendingUpdate);
            Application.Current.Shutdown();
        }
        catch (Exception ex) { ShowUpd("Não deu para trocar o programa: " + ex.Message, true); }
    }
}
