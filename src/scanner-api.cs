using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // =========================================================================
        // DEFINE COMMAND LINE INTERFACE ROUTING PARAMS & PAYLOAD ARGUMENTS
        // =========================================================================
        var rootCommand = new RootCommand("Proprietary Universal Scanner CLI Engine (scanner-api.exe)");

        var listOption = new Option<bool>(
            "--list", 
            "List all discovered hardware scanning nodes across active TWAIN and WIA driver layers.");
            
        var statusOption = new Option<string>(
            "--status", 
            "Query real-time hardware status metrics using device name, GUID string, or network IP.");
            
        var selectOption = new Option<string>(
            "--select", 
            "Select specific device identifier string, GUID, or IP path targeted for execution.");
            
        var dpiOption = new Option<int>(
            "--dpi", 
            () => 300, 
            "Resolution quality parameter setting (e.g., 150, 300, 600 DPI).");
            
        var colorOption = new Option<string>(
            "--color", 
            () => "color", 
            "Color payload spectrum transformation constraints: [color, gray, bw]");
            
        var sourceOption = new Option<string>(
            "--source", 
            () => "flatbed", 
            "Paper input feed ingestion selector settings: [flatbed, feeder]");
            
        var outputOption = new Option<string>(
            "--output", 
            () => "scan_output.jpg", 
            "Destination system storage file directory path location.");

        rootCommand.AddOption(listOption);
        rootCommand.AddOption(statusOption);
        rootCommand.AddOption(selectOption);
        rootCommand.AddOption(dpiOption);
        rootCommand.AddOption(colorOption);
        rootCommand.AddOption(sourceOption);
        rootCommand.AddOption(outputOption);

        // =========================================================================
        // CORE COMMAND ACTION PARSING & DISPATCHER INTERFACE LOOP
        // =========================================================================
        rootCommand.SetHandler(async (bool list, string status, string select, int dpi, string color, string source, string output) =>
        {
            // FEATURE 1 & 3: MASS SCANNING DEVICE DISCOVERY ROUTINES
            if (list)
            {
                Console.WriteLine("[*] Enumerating active local WIA COM device registration tables...");
                try
                {
                    var wia = new WiaEngine();
                    foreach (var entry in wia.EnumerateScanners())
                    {
                        Console.WriteLine($"  -> [Protocol: WIA] Name: {entry.Key} | Registry ID: {entry.Value}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"  [-] WIA discovery fault: {ex.Message}"); }

                Console.WriteLine("[*] Pinging local Win32 TWAIN DSM registry data sources...");
                try
                {
                    using var twain = new TwainEngine();
                    foreach (var name in twain.EnumerateScanners())
                    {
                        Console.WriteLine($"  -> [Protocol: TWAIN] Driver Mapping Identity Name: {name}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"  [-] TWAIN discovery fault: {ex.Message}"); }
                
                Console.WriteLine("[*] Network discovery note: Network eSCL devices are targeted driverless via their IP host address directly.");
                return;
            }

            // FEATURE 1: REAL-TIME INDEPENDENT STATUS QUERY VIA FACTORY ABSTRACT LAYER
            if (!string.IsNullOrEmpty(status))
            {
                // 1. Determine underlying driver type signature based on the user parameter status query string
                ProtocolType protocol = ScannerFactory.DetectProtocol(status);
                Console.WriteLine($"[*] Routing status verification request to Factory engine: {protocol}");

                try
                {
                    // 2. Instantiate isolated concrete worker object tracking the structural wrapper definition interface
                    using IScannerDevice device = ScannerFactory.CreateDevice(protocol);
                    
                    // 3. Fire status retrieval script token transaction loop
                    string statusResult = await device.GetStatusAsync(status);
                    Console.WriteLine($"[+] Status Loop Response -> {statusResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[-] Hardware Interface Query Fault: {ex.Message}");
                }
                return;
            }

            // FEATURE 2, 3 & 4: COMPREHENSIVE PAYLOAD JOB DISPATCH MACHINE LAYER
            if (!string.IsNullOrEmpty(select))
            {
                // 1. Factory scans data signature rules to pick correct driver profile block context (eSCL, TWAIN, WIA)
                ProtocolType protocol = ScannerFactory.DetectProtocol(select);
                Console.WriteLine($"[*] Factory identified routing protocol driver layer context as: {protocol}");

                try
                {
                    // 2. Instantiate matching execution engine pipeline from the unified Factory collection
                    using IScannerDevice device = ScannerFactory.CreateDevice(protocol);

                    Console.WriteLine($"[*] Dispatching configuration script command payload payload targets to device: {select}");
                    Console.WriteLine($"[*] Parameters: {dpi} DPI | Color Mode: {color} | Feed Mode: {source}");

                    // 3. Pass structured tuning payloads downstream to target hardware execution points
                    bool jobSuccess = await device.ExecuteScanAsync(select, dpi, color, source, output);
                    
                    if (jobSuccess)
                    {
                        Console.WriteLine($"[+] Scan Operation Complete. Binary stream written to: {Path.GetFullPath(output)}");
                    }
                    else
                    {
                        Console.WriteLine("[-] Critical Execution Failure: Underlying protocol subsystem driver rejected the payload script command.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Critical Error processing native middleware device streams: {ex.Message}");
                }
                return;
            }

            Console.WriteLine("[!] Parameters Exception: System execution halted. Supply specific operational payloads or execute '--help'.");

        }, listOption, statusOption, selectOption, dpiOption, colorOption, sourceOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }
}
