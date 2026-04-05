namespace BISTApp.Services;

/// <summary>
/// Yerel dosyadan (BISTsem.txt) BIST sembollerini okur.
/// </summary>
public class FileSymbolService
{
    private readonly ILogger<FileSymbolService> _logger;
    private const string FileName = "BISTsem.txt";

    public FileSymbolService(ILogger<FileSymbolService> logger)
    {
        _logger = logger;
    }

    public async Task<List<string>> GetAllBistSymbolsAsync(CancellationToken ct = default)
    {
        try
        {
            // Dosyayı hem çalışma dizininde (bin) hem de proje ana dizininde (CurrentDirectory) ara
            var pathsToCheck = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), FileName),
                Path.Combine(AppContext.BaseDirectory, FileName)
            };

            var validPath = pathsToCheck.FirstOrDefault(File.Exists);

            if (validPath == null)
            {
                _logger.LogWarning("Sembol dosyası bulunamadı! Aranan yerler: {Paths}", string.Join(", ", pathsToCheck));
                return new List<string>();
            }

            // Tüm metni oku ve ayır (virgül, boşluk, satır sonu vb. hepsini destekle)
            var text = await File.ReadAllTextAsync(validPath, ct);
            
            var symbols = text.Split(new[] { '\n', '\r', ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpperInvariant())
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            _logger.LogInformation("{FileName} dosyasından {Count} adet sembol okundu. (Kaynak: {Path})", FileName, symbols.Count, validPath);

            return symbols;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sembol dosyası okunurken hata oluştu: {FileName}", FileName);
            return new List<string>();
        }
    }
}
