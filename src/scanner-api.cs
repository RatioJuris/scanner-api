using System;
using System.CommandLine;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // 1. DYNAMIC NO-ARGUMENT CODES AND INTERACTION CHECK BLOCKS
        if (args.Length == 0)
        {
            RenderDeveloperDashboardWelcomeScreen();
            Console.WriteLine("\n[Press any key to close the application console terminal window...]");
            Console.ReadKey(true);
            return 0;
        }

        // 2. EXPLICIT INTERRUPT SWITCH ROUTING FOR VERSION RETRIEVAL
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
            var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
            
            Console.WriteLine($"{product} [Version {version}]");
            Console.WriteLine($"{copyright}");
            return 0;
        }

        // =========================================================================
        // DEFINE COMMAND LINE INTERFACE ROUTING PARAMS & PAYLOAD ARGUMENTS
        // =========================================================================
        var rootCommand = new RootCommand("Proprietary Universal Scanner CLI Engine (scanner-api.exe)");

        var listOption = new Option<bool>("--list", "List all discovered scanning nodes across active TWAIN and WIA driver layers.");
        var statusOption = new Option<string>("--status", "Query real-time hardware status metrics using name, GUID, or IP.");
        var selectOption = new Option<string>("--select", "Select specific device identifier string, GUID, or IP path.");
        var dpiOption = new Option<int>("--dpi", () => 300, "Resolution quality parameter setting (DPI).");
        var colorOption = new Option<string>("--color", () => "color", "Color transformation constraints: [color, gray, bw]");
        var sourceOption = new Option<string>("--source", () => "flatbed", "Paper input feed ingestion targets: [flatbed, feeder]");
        var outputOption = new Option<string>("--output", () => "scan_output.jpg", "Destination storage file path location.");

        rootCommand.AddOption(listOption);
        rootCommand.AddOption(statusOption);
        rootCommand.AddOption(selectOption);
        rootCommand.AddOption(dpiOption);
        rootCommand.AddOption(colorOption);
        rootCommand.AddOption(sourceOption);
        rootCommand.AddOption(outputOption);

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
                return;
            }

            // FEATURE 1: REAL-TIME INDEPENDENT STATUS QUERY VIA FACTORY ABSTRACT LAYER
            if (!string.IsNullOrEmpty(status))
            {
                ProtocolType protocol = ScannerFactory.DetectProtocol(status);
                Console.WriteLine($"[*] Routing status verification request to Factory engine: {protocol}");

                try
                {
                    using IScannerDevice device = ScannerFactory.CreateDevice(protocol);
                    string statusResult = await device.GetStatusAsync(status);
                    Console.WriteLine($"[+] Status Loop Response -> {statusResult}");
                }
                catch (Exception ex) { Console.WriteLine($"[-] Hardware Interface Query Fault: {ex.Message}"); }
                return;
            }

            // FEATURE 2, 3 & 4: COMPREHENSIVE PAYLOAD JOB DISPATCH MACHINE LAYER
            if (!string.IsNullOrEmpty(select))
            {
                ProtocolType protocol = ScannerFactory.DetectProtocol(select);
                Console.WriteLine($"[*] Factory identified routing protocol driver layer context as: {protocol}");

                try
                {
                    using IScannerDevice device = ScannerFactory.CreateDevice(protocol);
                    bool jobSuccess = await device.ExecuteScanAsync(select, dpi, color, source, output);
                    
                    if (jobSuccess)
                    {
                        Console.WriteLine($"[+] Scan Operation Complete. Binary stream written to: {Path.GetFullPath(output)}");
                    }
                    else
                    {
                        Console.WriteLine("[-] Critical Failure: Subsystem driver rejected payload script command parameters.");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[!] Critical Error processing native device streams: {ex.Message}"); }
                return;
            }

        }, listOption, statusOption, selectOption, dpiOption, colorOption, sourceOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static void RenderDeveloperDashboardWelcomeScreen()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================================");
        Console.WriteLine($"    {product?.ToUpper()} (ENGINE ROUTER API)");
        Console.WriteLine($"    Core Module Version Architecture Build Target Pipeline: [ v{version} ]");
        Console.WriteLine($"    {copyright}");
        Console.WriteLine("==================================================================================");
        Console.ResetColor();
        
        Console.WriteLine($"\nDescription:\n  {description}");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n----------------------------------------------------------------------------------");
        Console.WriteLine(" 🛠️  SUPPORTED PARAMETERS & PAYLOAD OPTIONS DESCRIPTION MAP");
        Console.WriteLine("----------------------------------------------------------------------------------");
        Console.ResetColor();

        Console.WriteLine("  --list             : Triggers dynamic scanning device routing tables updates for local drivers.");
        Console.WriteLine("  --status <target>  : Pulls real-time physical telemetry loop details from targeted nodes.");
        Console.WriteLine("  --select <target>  : Chooses a target node by local hardware matching name identity, GUID, or IP.");
        Console.WriteLine("  --dpi <int>        : Resolution constraints configurations pipeline data rules (Default: 300).");
        Console.WriteLine("  --color <string>   : Custom color space layout options mapping rules: [color, gray, bw].");
        Console.WriteLine("  --source <string>  : Target loading component selection criteria configuration: [flatbed, feeder].");
        Console.WriteLine("  --output <path>    : File conversion destination output file path mapping location (Default: scan_output.jpg).");
        Console.WriteLine("  --version / -v     : Outputs compiled engine metadata properties metrics layout tracking matrix scripts.");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n----------------------------------------------------------------------------------");
        Console.WriteLine(" SYSTEM COMMAND STRINGS EXECUTION EXAMPLES SYNTAX");
        Console.WriteLine("----------------------------------------------------------------------------------");
        Console.ResetColor();

        Console.WriteLine("  1. Discover hardware devices across active local pipelines:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("     > scanner-api.exe --list\n");
        Console.ResetColor();

        Console.WriteLine("  2. Poll real-time status matrices of local scanners or driverless IP endpoints:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("     > scanner-api.exe --status \"Canon DR-C225\"");
        Console.WriteLine("     > scanner-api.exe --status \"192.168.1.95\"\n");
        Console.ResetColor();

        Console.WriteLine("  3. Execute high-speed grayscale scanner feeder scan automation payload via raw TWAIN driver handles:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("     > scanner-api.exe --select \"Fujitsu fi-7160\" --dpi 200 --color gray --source feeder --output C:\\Scans\\doc.jpg\n");
        Console.ResetColor();

        Console.WriteLine("  4. Execute standard driverless network scan deployment task package payload mapping over eSCL layer sockets:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("     > scanner-api.exe --select \"192.168.1.120\" --dpi 300 --color color --source flatbed --output C:\\Scans\\sheet.jpg");
        Console.ResetColor();
        
        Console.WriteLine("==================================================================================");
    }
}
