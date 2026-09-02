using System;
using System.Threading.Tasks;

public interface IScannerDevice : IDisposable
{
    Task<string> GetStatusAsync(string identifier);
    Task<bool> ExecuteScanAsync(string identifier, int dpi, string color, string source, string outputPath);
}
