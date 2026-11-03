using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ResetExplode.Modular;

namespace ResetExplode;

/// <summary>Mostra textos escritos em CAIXA ALTA como frase (ver <see cref="FriendlyText"/>).</summary>
public sealed class FriendlyCaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string s ? FriendlyText.Apply(s) : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Aplica o modelo amigável só quando o conteúdo é texto; ícones e painéis seguem o padrão do WPF.</summary>
public sealed class FriendlyTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Template { get; set; }
    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item is string ? Template : null;
}
