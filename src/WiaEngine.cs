using System;
using System.Collections.Generic;
using System.IO;

public sealed class WiaEngine
{
    private const string WiaDeviceTypeScanner = "1";

    private const string WIA_COMPRESSION = "4107";
    private const string WIA_IPA_DATATYPE = "4103";

    private const string WIA_IPS_XRES = "6147";
    private const string WIA_IPS_YRES = "6148";

    private const string WIA_DPS_DOCUMENT_HANDLING_STATUS = "3088";
    private const string WIA_DPS_DOCUMENT_HANDLING_SELECT = "3087";

    private const string WiaFormatJpeg =
        "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";

    private readonly dynamic? _deviceManager;

    public WiaEngine()
    {
        try
        {
            Type? wiaType = Type.GetTypeFromProgID("WIA.DeviceManager");

            if (wiaType == null)
            {
                throw new PlatformNotSupportedException(
                    "WIA.DeviceManager COM registration not found.");
            }

            _deviceManager = Activator.CreateInstance(wiaType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] WIA initialization failed: {ex.Message}");
            _deviceManager = null;
        }
    }

    public bool IsAvailable => _deviceManager != null;

    public Dictionary<string, string> EnumerateScanners()
    {
        var scanners = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (_deviceManager == null)
        {
            return scanners;
        }

        try
        {
            foreach (dynamic info in _deviceManager.DeviceInfos)
            {
                try
                {
                    if (info == null)
                    {
                        continue;
                    }

                    string deviceType = info.Type?.ToString() ?? string.Empty;

                    if (deviceType != WiaDeviceTypeScanner)
                    {
                        continue;
                    }

                    dynamic? properties = info.Properties;

                    if (properties == null)
                    {
                        continue;
                    }

                    string name =
                        properties["Name"]?.Value?.ToString()
                        ?? "Unknown Scanner";

                    string deviceId =
                        info.DeviceID?.ToString()
                        ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(deviceId))
                    {
                        scanners[name] = deviceId;
                    }
                }
                catch
                {
                    // Ignore individual device failures.
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[!] Scanner enumeration failed: {ex.Message}");
        }

        return scanners;
    }

    public string QueryRealtimeStatus(string deviceId)
    {
        if (_deviceManager == null)
        {
            return "OFFLINE: WIA subsystem unavailable";
        }

        try
        {
            dynamic? deviceInfo = FindDeviceInfo(deviceId);

            if (deviceInfo == null)
            {
                return "OFFLINE: Scanner not found";
            }

            dynamic? device = deviceInfo.Connect();

            if (device == null)
            {
                return "OFFLINE: Connection failed";
            }

            int statusFlags = 0;

            try
            {
                dynamic? properties = device.Properties;

                if (properties != null)
                {
                    dynamic? statusProperty =
                        properties[WIA_DPS_DOCUMENT_HANDLING_STATUS];

                    if (statusProperty?.Value != null)
                    {
                        statusFlags = Convert.ToInt32(
                            statusProperty.Value);
                    }
                }
            }
            catch
            {
                return "ONLINE: Connected but status unavailable";
            }

            if ((statusFlags & 1) != 0)
            {
                return "ERROR: PAPER JAM";
            }

            if ((statusFlags & 2) != 0)
            {
                return "ATTENTION: ADF EMPTY";
            }

            if ((statusFlags & 4) != 0)
            {
                return "ERROR: COVER OPEN";
            }

            if ((statusFlags & 32) != 0)
            {
                return "OFFLINE: WARMING UP";
            }

            return "READY: ONLINE";
        }
        catch (Exception ex)
        {
            return $"OFFLINE: {ex.Message}";
        }
    }

    public bool ExecuteScanJob(
        string deviceId,
        int dpi,
        int colorMode,
        int paperSource,
        string targetPath)
    {
        if (_deviceManager == null)
        {
            return false;
        }

        try
        {
            dynamic? deviceInfo = FindDeviceInfo(deviceId);

            if (deviceInfo == null)
            {
                return false;
            }

            dynamic? device = deviceInfo.Connect();

            if (device == null)
            {
                return false;
            }

            dynamic? items = device.Items;

            if (items == null || items.Count < 1)
            {
                return false;
            }

            dynamic? item = items[1];

            if (item == null)
            {
                return false;
            }

            ConfigureScanItem(item, dpi, colorMode);

            ConfigurePaperSource(device, paperSource);

            Type? dialogType =
                Type.GetTypeFromProgID("WIA.CommonDialog");

            if (dialogType == null)
            {
                return false;
            }

            dynamic? dialog = Activator.CreateInstance(dialogType);

            if (dialog == null)
            {
                return false;
            }

            dynamic? transferResult =
                dialog.ShowTransfer(item, WiaFormatJpeg, false);

            if (transferResult == null)
            {
                return false;
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            transferResult.SaveFile(targetPath);

            return File.Exists(targetPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Scan failed: {ex.Message}");
            return false;
        }
    }

    private static void ConfigureScanItem(
        dynamic item,
        int dpi,
        int colorMode)
    {
        try
        {
            dynamic? properties = item.Properties;

            if (properties == null)
            {
                return;
            }

            dynamic? xRes = properties[WIA_IPS_XRES];
            dynamic? yRes = properties[WIA_IPS_YRES];
            dynamic? dataType = properties[WIA_IPA_DATATYPE];

            if (xRes != null)
            {
                xRes.Value = dpi;
            }

            if (yRes != null)
            {
                yRes.Value = dpi;
            }

            if (dataType != null)
            {
                dataType.Value = colorMode;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[-] Unable to configure scan settings: {ex.Message}");
        }
    }

    private static void ConfigurePaperSource(
        dynamic device,
        int paperSource)
    {
        try
        {
            dynamic? properties = device.Properties;

            if (properties == null)
            {
                return;
            }

            dynamic? sourceProperty =
                properties[WIA_DPS_DOCUMENT_HANDLING_SELECT];

            if (sourceProperty != null)
            {
                sourceProperty.Value = paperSource;
            }
        }
        catch
        {
            // Some scanners do not expose this property.
        }
    }

    private dynamic? FindDeviceInfo(string identifier)
    {
        if (_deviceManager == null ||
            string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        try
        {
            foreach (dynamic info in _deviceManager.DeviceInfos)
            {
                try
                {
                    if (info == null)
                    {
                        continue;
                    }

                    string deviceId =
                        info.DeviceID?.ToString()
                        ?? string.Empty;

                    dynamic? properties = info.Properties;

                    string? name = null;

                    if (properties != null)
                    {
                        try
                        {
                            name =
                                properties["Name"]?.Value?.ToString();
                        }
                        catch
                        {
                        }
                    }

                    bool idMatch =
                        string.Equals(
                            deviceId,
                            identifier,
                            StringComparison.OrdinalIgnoreCase);

                    bool nameMatch =
                        !string.IsNullOrEmpty(name) &&
                        name.Contains(
                            identifier,
                            StringComparison.OrdinalIgnoreCase);

                    if (idMatch || nameMatch)
                    {
                        return info;
                    }
                }
                catch
                {
                    // Ignore invalid device entries.
                }
            }
        }
        catch
        {
        }

        return null;
    }
}
