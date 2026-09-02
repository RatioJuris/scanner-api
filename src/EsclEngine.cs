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
        // Setup cross-vendor compatible connection matrices over local subnets
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private string FormatBaseUrl(string hostOrIp)
    {
        string target = hostOrIp.Trim();
        if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            target = $"http://{target}";
        }
        return target.TrimEnd('/');
    }

    // =========================================================================
    // FEATURE 1: REAL-TIME INDEPENDENT HARDWARE STATUS ASSESSMENT
    // =========================================================================
    public async Task<string> QueryRealtimeStatusAsync(string targetHost)
    {
        string baseUrl = FormatBaseUrl(targetHost);
        string endpoint = $"{baseUrl}/eSCL/ScannerStatus";

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                return $"OFFLINE: Endpoint responded with HTTP status {(int)response.StatusCode}";
            }

            string xmlStatus = await response.Content.ReadAsStringAsync();

            // Native XML micro-parsing lookup logic without relying on external parser payloads
            if (xmlStatus.Contains("<eSCL:State>Idle</eSCL:State>", StringComparison.OrdinalIgnoreCase))
                return "READY: HARDWARE IDLE & ONLINE";
            if (xmlStatus.Contains("<eSCL:State>Processing</eSCL:State>", StringComparison.OrdinalIgnoreCase))
                return "BUSY: SCANNER ACTIVE / PROCESSING PAGES";
            if (xmlStatus.Contains("<eSCL:State>Stopped</eSCL:State>", StringComparison.OrdinalIgnoreCase))
                return "ERROR: COMPONENT STOPPED / USER ATTENTION REQUIRED";
            
            if (xmlStatus.Contains("ScannerAdfEmpty", StringComparison.OrdinalIgnoreCase))
                return "ATTENTION: AUTOMATIC DOCUMENT FEEDER BIN EMPTY";
            if (xmlStatus.Contains("ScannerAdfJam", StringComparison.OrdinalIgnoreCase))
                return "ERROR: PHYSICAL PAPER JAM ENCOUNTERED INSIDE UNIT";

            return "ONLINE: STATUS INDETERMINATE";
        }
        catch (Exception ex)
        {
            return $"OFFLINE: Communication pipeline faulted ({ex.Message})";
        }
    }

    // =========================================================================
    // FEATURE 2, 3 & 4: COMPREHENSIVE PAYLOAD JOB DISPATCH ENGINE
    // =========================================================================
    public async Task<bool> ExecuteScanJobAsync(string targetHost, int dpi, string colorMode, string paperSource, string targetPath)
    {
        string baseUrl = FormatBaseUrl(targetHost);
        string endpoint = $"{baseUrl}/eSCL/ScanJobs";

        // Route parameters payload to matching protocol tags
        string xmlColor = colorMode.ToLower() switch
        {
            "gray" => "Grayscale8",
            "bw"   => "BlackAndWhite1",
            _      => "RGB24" 
        };

        string xmlSource = paperSource.ToLower() switch
        {
            "feeder" => "Adf",
            _        => "Platen" 
        };

        // Construct driverless network XML configuration script payload
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

        try
        {
            using var content = new StringContent(xmlPayload, Encoding.UTF8, "text/xml");
            HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content);

            // Printer hardware signals job synchronization acceptance via HTTP 201 Created
            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                string? jobLocation = response.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(jobLocation))
                {
                    return false;
                }

                string targetStreamUrl = jobLocation.StartsWith("http", StringComparison.OrdinalIgnoreCase) 
                    ? jobLocation 
                    : $"{baseUrl}{jobLocation}";

                string dataUrl = $"{targetStreamUrl.TrimEnd('/')}/NextPage";

                // Pull raw network data packets and write straight to physical file cache
                byte[] rawBinaryStream = await _httpClient.GetByteArrayAsync(dataUrl);

                if (File.Exists(targetPath)) File.Delete(targetPath);
                await File.WriteAllBytesAsync(targetPath, rawBinaryStream);

                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Critical Error processing native eSCL payload connection streams: {ex.Message}");
        }

        return false;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
