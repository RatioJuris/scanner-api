using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class EsclEngine : IDisposable
{
    private readonly HttpClient _httpClient;

    public EsclEngine()
    {
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private string FormatBaseUrl(string hostOrIp)
    {
        string target = hostOrIp.Trim();
        if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            target = $"http://{target}";
        }
        return target.TrimEnd('/');
    }

    public async Task<string> QueryRealtimeStatusAsync(string targetHost)
    {
        try
        {
            string baseUrl = FormatBaseUrl(targetHost);
            HttpResponseMessage response = await _httpClient.GetAsync($"{baseUrl}/eSCL/ScannerStatus");
            if (!response.IsSuccessStatusCode) return $"OFFLINE: HTTP Status Error {(int)response.StatusCode}";

            string xmlStatus = await response.Content.ReadAsStringAsync();
            if (xmlStatus.Contains("<eSCL:State>Idle</eSCL:State>", StringComparison.OrdinalIgnoreCase)) return "READY: HARDWARE IDLE & ONLINE";
            if (xmlStatus.Contains("<eSCL:State>Processing</eSCL:State>", StringComparison.OrdinalIgnoreCase)) return "BUSY: SCANNER ACTIVE / PROCESSING PAGES";
            if (xmlStatus.Contains("<eSCL:State>Stopped</eSCL:State>", StringComparison.OrdinalIgnoreCase)) return "ERROR: HARDWARE COMPONENT STOPPED";
            if (xmlStatus.Contains("ScannerAdfEmpty", StringComparison.OrdinalIgnoreCase)) return "ATTENTION: AUTOMATIC DOCUMENT FEEDER BIN EMPTY";
            if (xmlStatus.Contains("ScannerAdfJam", StringComparison.OrdinalIgnoreCase)) return "ERROR: PHYSICAL PAPER JAM ENCOUNTERED INSIDE UNIT";

            return "ONLINE: INDETERMINATE SYSTEM STATE";
        }
        catch (Exception ex) { return $"OFFLINE: Communication pipeline faulted ({ex.Message})"; }
    }

    public async Task<bool> ExecuteScanJobAsync(string targetHost, int dpi, string colorMode, string paperSource, string targetPath)
    {
        try
        {
            string baseUrl = FormatBaseUrl(targetHost);
            string xmlColor = colorMode.ToLower() switch { "gray" => "Grayscale8", "bw" => "BlackAndWhite1", _ => "RGB24" };
            string xmlSource = paperSource.ToLower() == "feeder" ? "Adf" : "Platen";

            string xmlPayload = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <eSCL:ScanSettings xmlns:eSCL="http://hp.com">
                <eSCL:XResolution>{dpi}</eSCL:XResolution>
                <eSCL:YResolution>{dpi}</eSCL:YResolution>
                <eSCL:ColorMode>{xmlColor}</eSCL:ColorMode>
                <eSCL:InputSource>{xmlSource}</eSCL:InputSource>
                <eSCL:DocumentFormat>image/jpeg</eSCL:DocumentFormat>
            </eSCL:ScanSettings>
            """;

            using var content = new StringContent(xmlPayload, Encoding.UTF8, "text/xml");
            HttpResponseMessage response = await _httpClient.PostAsync($"{baseUrl}/eSCL/ScanJobs", content);

            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                string? jobLocation = response.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(jobLocation)) return false;

                string streamUrl = jobLocation.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? jobLocation : $"{baseUrl}{jobLocation}";
                byte[] rawBinaryStream = await _httpClient.GetByteArrayAsync($"{streamUrl.TrimEnd('/')}/NextPage");

                if (File.Exists(targetPath)) File.Delete(targetPath);
                await File.WriteAllBytesAsync(targetPath, rawBinaryStream);
                return true;
            }
        }
        catch (Exception ex) { Console.WriteLine($"[!] eSCL Job Transaction Failed: {ex.Message}"); }
        return false;
    }

    public void Dispose() => _httpClient.Dispose();
}
