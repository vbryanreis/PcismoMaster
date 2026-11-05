namespace ResetExplode;

/// <summary>Uma linha do inventário de drivers instalados.</summary>
public sealed class DriverRow
{
    public string Device { get; init; } = "";
    public string Class { get; init; } = "";
    public string Vendor { get; init; } = "";
    public string Version { get; init; } = "";
    public string Date { get; init; } = "";
    public bool Signed { get; init; }
    public string SignedText => Signed ? "assinado" : "sem assinatura";
}

/// <summary>Placa-mãe / fabricante e as páginas oficiais de suporte.</summary>
public sealed class BoardInfo
{
    public string Maker { get; set; } = "";
    public string Model { get; set; } = "";
    public string Bios { get; set; } = "";
    public string Brand { get; set; } = "";   // ASUS / MSI / Gigabyte / outro
    public string SupportUrl { get; set; } = "";
}
