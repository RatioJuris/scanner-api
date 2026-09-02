using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum ProtocolType
{
    Wia,
    Twain,
    Escl,
    Unknown
}

public class ScannerFactory
{
    public static ProtocolType DetectProtocol(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return ProtocolType.Unknown;

        // 1. Route to eSCL driverless stack if it maps to a network endpoint or IP address
        if (identifier.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            identifier.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || 
            identifier.Contains("."))
        {
            return ProtocolType.Escl;
        }

        // 2. Query low-level Win32 TWAIN DSM registry cache to see if the name matches a vendor driver identity
        try
        {
            using var twain = new TwainEngine();
            var twainDevices = twain.EnumerateScanners();
            if (twainDevices.Exists(x => x.Equals(identifier, StringComparison.OrdinalIgnoreCase)))
            {
                return ProtocolType.Twain;
            }
        }
        catch { /* Fallback to check WIA if TWAIN subsystem environment drops active context */ }

        // 3. Fallback to native Windows WIA for registry strings, local descriptors, or GUID values
        return ProtocolType.Wia;
    }

    public static IScannerDevice CreateDevice(ProtocolType protocol)
    {
        return protocol switch
        {
            ProtocolType.Escl  => new EsclDeviceWrapper(),
            ProtocolType.Twain => new TwainDeviceWrapper(),
            ProtocolType.Wia   => new WiaDeviceWrapper(),
            _ => throw new NotSupportedException("[-] Architecture Exception: Undefined protocol engine target mapping.")
        };
    }
}

// =========================================================================
// CONCRETE WRAPPER MAPPING TRANSLATIONS
// =========================================================================

public class EsclDeviceWrapper : IScannerDevice
{
    private readonly EsclEngine _engine = new EsclEngine();
    public async Task<string> GetStatusAsync(string id) => await _engine.QueryRealtimeStatusAsync(id);
    public async Task<bool> ExecuteScanAsync(string id, int dpi, string color, string source, string path) =>
        await _engine.ExecuteScanJobAsync(id, dpi, color, source, path);
    public void Dispose() => _engine.Dispose();
}

public class WiaDeviceWrapper : IScannerDevice
{
    private readonly WiaEngine _engine = new WiaEngine();
    public Task<string> GetStatusAsync(string id) => Task.FromResult(_engine.QueryRealtimeStatus(id));
    public Task<bool> ExecuteScanAsync(string id, int dpi, string color, string source, string path)
    {
        int colorMode = color.ToLower() switch { "gray" => 2, "bw" => 4, _ => 1 };
        int paperSource = source.ToLower() == "feeder" ? 1 : 2;
        bool success = _engine.ExecuteScanJob(id, dpi, colorMode, paperSource, path);
        return Task.FromResult(success);
    }
    public void Dispose() { }
}

public class TwainDeviceWrapper : IScannerDevice
{
    private readonly TwainEngine _engine = new TwainEngine();
    public Task<string> GetStatusAsync(string id)
    {
        bool open = _engine.SelectAndOpenScanner(id);
        string status = open ? "READY: TWAIN VENDOR DRIVER VERIFIED ONLINE" : "OFFLINE: TWAIN TARGET CONTEXT REJECTED OR BUSY";
        if (open) _engine.CloseActiveScanner();
        return Task.FromResult(status);
    }
    public Task<bool> ExecuteScanAsync(string id, int dpi, string color, string source, string path)
    {
        if (_engine.SelectAndOpenScanner(id))
        {
            ushort pixelType = color.ToLower() switch { "gray" => 1, "bw" => 0, _ => 2 };
            ushort paperSource = source.ToLower() == "feeder" ? (ushort)1 : (ushort)0;
            bool success = _engine.ExecuteScanJob(dpi, pixelType, paperSource);
            return Task.FromResult(success);
        }
        return Task.FromResult(false);
    }
    public void Dispose() => _engine.Dispose();
}
