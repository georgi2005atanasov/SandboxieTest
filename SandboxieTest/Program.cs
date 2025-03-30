using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MultiViberStandard
{
    class Program
    {
        // Paths to standard Sandboxie (not Plus)
        private static string SandboxiePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Sandboxie");

        private static string SandboxiePath32 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Sandboxie");

        // Path to Start.exe (will be determined at runtime)
        private static string StartExePath;

        // Configuration storage
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MultiViberStandard");

        private static readonly string AccountsFile = Path.Combine(ConfigDir, "accounts.txt");
        private static readonly string ViberPathFile = Path.Combine(ConfigDir, "viber_path.txt");

        private static readonly List<ViberAccount> ViberAccounts = new List<ViberAccount>();
        private static string _viberPath = null;

        static void Main(string[] args)
        {
            Console.Title = "Multi-Viber for Standard Sandboxie";

            if (!CheckEnvironment())
                return;

            LoadAccounts();

            bool exit = false;
            while (!exit)
            {
                DisplayMenu();

                var choice = Console.ReadLine()?.Trim();
                switch (choice)
                {
                    case "1":
                        AddAccount();
                        break;
                    case "2":
                        ListAccounts();
                        break;
                    case "3":
                        LaunchAccount();
                        break;
                    case "4":
                        DeleteAccount();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        WriteColor("Invalid option. Please try again.", ConsoleColor.Red);
                        break;
                }
            }
        }

        private static void DisplayMenu()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================");
            Console.WriteLine("=     MULTI-VIBER FOR STANDARD SANDBOXIE          =");
            Console.WriteLine("===================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("1. Add new Viber account");
            Console.WriteLine("2. List Viber accounts");
            Console.WriteLine("3. Launch Viber account");
            Console.WriteLine("4. Delete Viber account");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice: ");
        }

        private static bool CheckEnvironment()
        {
            Console.Clear();
            Console.WriteLine("Checking environment...");

            // Check Sandboxie installation and find paths
            if (!IsSandboxieInstalled())
            {
                WriteColor("Sandboxie is not installed. Please install Sandboxie (free version) first.", ConsoleColor.Red);
                WriteColor("Download from: https://sandboxie.com/", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return false;
            }

            // Check Viber installation
            _viberPath = FindViberPath();
            if (_viberPath == null)
            {
                WriteColor("Viber is not installed or could not be found.", ConsoleColor.Red);
                WriteColor("Please enter the full path to Viber.exe:", ConsoleColor.Yellow);
                _viberPath = Console.ReadLine();

                if (!File.Exists(_viberPath))
                {
                    WriteColor("Invalid path. Viber.exe not found.", ConsoleColor.Red);
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return false;
                }

                // Save the Viber path for future use
                SaveViberPath(_viberPath);
            }

            // Ensure config directory exists
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }

            WriteColor("Environment check complete. Sandboxie and Viber are installed.", ConsoleColor.Green);
            Thread.Sleep(1000);
            return true;
        }

        private static bool IsSandboxieInstalled()
        {
            // Check for 64-bit version
            string start64 = Path.Combine(SandboxiePath, "Start.exe");

            // Check for 32-bit version
            string start32 = Path.Combine(SandboxiePath32, "Start.exe");

            if (File.Exists(start64))
            {
                StartExePath = start64;
                return true;
            }
            else if (File.Exists(start32))
            {
                StartExePath = start32;
                return true;
            }

            return false;
        }

        private static string FindViberPath()
        {
            // First check if we have a saved path
            if (File.Exists(ViberPathFile))
            {
                try
                {
                    string savedPath = File.ReadAllText(ViberPathFile).Trim();
                    if (File.Exists(savedPath))
                        return savedPath;
                }
                catch
                {
                    // Ignore errors, continue searching
                }
            }

            // Check common Viber installation paths
            string[] possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Viber", "Viber.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Viber", "Viber.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Viber", "Viber.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Viber", "Viber.exe")
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static void SaveViberPath(string path)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                File.WriteAllText(ViberPathFile, path);
            }
            catch (Exception ex)
            {
                WriteColor($"Failed to save Viber path: {ex.Message}", ConsoleColor.Red);
            }
        }

        private static void AddAccount()
        {
            Console.Clear();
            Console.WriteLine("=== ADD NEW VIBER ACCOUNT ===");
            Console.WriteLine();

            Console.Write("Enter a name for this Viber account: ");
            string accountName = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(accountName))
            {
                WriteColor("Account name cannot be empty.", ConsoleColor.Red);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // Check if the account already exists
            if (ViberAccounts.Any(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase)))
            {
                WriteColor($"An account with the name '{accountName}' already exists.", ConsoleColor.Red);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // Create a sanitized sandbox name from the account name
            string sandboxName = "Viber" + new string(accountName.Where(c => char.IsLetterOrDigit(c)).ToArray());

            // Create sandbox configuration entry
            Console.WriteLine($"Creating sandbox configuration for {accountName}...");

            try
            {
                // Create the sandbox by directly editing Sandboxie.ini
                string sandboxieIniPath = Path.Combine(Path.GetDirectoryName(StartExePath), "Sandboxie.ini");

                if (!File.Exists(sandboxieIniPath))
                {
                    WriteColor($"Cannot find Sandboxie.ini at {sandboxieIniPath}", ConsoleColor.Red);
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                    return;
                }

                // Read the current config
                string[] iniLines = File.ReadAllLines(sandboxieIniPath);
                List<string> newConfig = new List<string>(iniLines);

                // Check if this sandbox section already exists
                bool sectionExists = false;
                for (int i = 0; i < iniLines.Length; i++)
                {
                    if (iniLines[i].Trim().Equals($"[{sandboxName}]", StringComparison.OrdinalIgnoreCase))
                    {
                        sectionExists = true;
                        break;
                    }
                }

                // Add new sandbox section if it doesn't exist
                if (!sectionExists)
                {
                    newConfig.Add("");
                    newConfig.Add($"[{sandboxName}]");
                    newConfig.Add("Enabled=y");
                    newConfig.Add("AutoDelete=y");
                    newConfig.Add("ConfigLevel=9");
                    newConfig.Add("RecoverFolder=%Desktop%");
                    newConfig.Add("BorderColor=#00ffff");
                    // Audio and network settings
                    newConfig.Add("OpenClsid={2C941FCE-975B-59BE-A960-9A2A262853A5}");
                    newConfig.Add("OpenClsid={D3DCB472-7261-43CE-924B-0704BD730D5A}");
                    newConfig.Add("OpenClsid={A81BA6FE-A5AB-4B2B-82DE-BA558AFDF786}");
                    newConfig.Add("ClosedIpcPath=\\RPC Control\\AudioClientRpc");
                    newConfig.Add("OpenIpcPath=\\BaseNamedObjects\\");
                    newConfig.Add("OpenIpcPath=\\Sessions\\*\\BaseNamedObjects\\");

                    // Save the modified config
                    File.WriteAllLines(sandboxieIniPath, newConfig);

                    // Reload the Sandboxie configuration
                    Process reloadProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = StartExePath,
                            Arguments = "/reload",
                            UseShellExecute = true
                        }
                    };

                    try
                    {
                        reloadProcess.Start();
                        reloadProcess.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        WriteColor($"Warning: Failed to reload configuration: {ex.Message}", ConsoleColor.Yellow);
                    }
                }

                // Add the account to our list
                ViberAccounts.Add(new ViberAccount
                {
                    Name = accountName,
                    SandboxName = sandboxName
                });

                // Save the updated accounts list
                SaveAccounts();

                WriteColor($"Account '{accountName}' added successfully.", ConsoleColor.Green);
                WriteColor($"You can now launch Viber with this account using the sandbox: {sandboxName}", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                WriteColor($"Error creating sandbox: {ex.Message}", ConsoleColor.Red);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void ListAccounts()
        {
            Console.Clear();
            Console.WriteLine("=== VIBER ACCOUNTS ===");
            Console.WriteLine();

            if (ViberAccounts.Count == 0)
            {
                WriteColor("No accounts have been added yet.", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            for (int i = 0; i < ViberAccounts.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ViberAccounts[i].Name} (Sandbox: {ViberAccounts[i].SandboxName})");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to return to the menu...");
            Console.ReadKey();
        }

        private static void LaunchAccount()
        {
            Console.Clear();
            Console.WriteLine("=== LAUNCH VIBER ACCOUNT ===");
            Console.WriteLine();

            if (ViberAccounts.Count == 0)
            {
                WriteColor("No accounts have been added yet.", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // List available accounts
            for (int i = 0; i < ViberAccounts.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ViberAccounts[i].Name}");
            }

            Console.WriteLine();
            Console.Write("Enter the number of the account to launch (or 0 to cancel): ");

            if (!int.TryParse(Console.ReadLine(), out int accountNumber) ||
                accountNumber < 1 ||
                accountNumber > ViberAccounts.Count)
            {
                if (accountNumber != 0)
                    WriteColor("Invalid selection.", ConsoleColor.Red);

                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // Get the selected account
            var account = ViberAccounts[accountNumber - 1];

            // Launch Viber in the sandbox
            WriteColor($"Launching Viber for account: {account.Name}...", ConsoleColor.Cyan);

            try
            {
                string sandboxieIniPath = Path.Combine(Path.GetDirectoryName(StartExePath), "Sandboxie.ini");

                // Verify the sandbox exists in the config
                bool boxExists = false;
                if (File.Exists(sandboxieIniPath))
                {
                    string[] iniLines = File.ReadAllLines(sandboxieIniPath);
                    for (int i = 0; i < iniLines.Length; i++)
                    {
                        if (iniLines[i].Trim().Equals($"[{account.SandboxName}]", StringComparison.OrdinalIgnoreCase))
                        {
                            boxExists = true;
                            break;
                        }
                    }
                }

                if (!boxExists)
                {
                    WriteColor($"Warning: Sandbox '{account.SandboxName}' not found in configuration.", ConsoleColor.Yellow);
                    WriteColor("Attempting to recreate the sandbox configuration...", ConsoleColor.Yellow);

                    // Add the sandbox section to the config file
                    List<string> newConfig = new List<string>();
                    if (File.Exists(sandboxieIniPath))
                    {
                        newConfig.AddRange(File.ReadAllLines(sandboxieIniPath));
                    }

                    newConfig.Add("");
                    newConfig.Add($"[{account.SandboxName}]");
                    newConfig.Add("Enabled=y");
                    newConfig.Add("AutoDelete=y");
                    newConfig.Add("ConfigLevel=9");
                    newConfig.Add("RecoverFolder=%Desktop%");
                    newConfig.Add("BorderColor=#00ffff");
                    newConfig.Add("OpenClsid={2C941FCE-975B-59BE-A960-9A2A262853A5}");
                    newConfig.Add("OpenClsid={D3DCB472-7261-43CE-924B-0704BD730D5A}");
                    newConfig.Add("OpenClsid={A81BA6FE-A5AB-4B2B-82DE-BA558AFDF786}");
                    newConfig.Add("ClosedIpcPath=\\RPC Control\\AudioClientRpc");
                    newConfig.Add("OpenIpcPath=\\BaseNamedObjects\\");
                    newConfig.Add("OpenIpcPath=\\Sessions\\*\\BaseNamedObjects\\");

                    // Save the modified config
                    File.WriteAllLines(sandboxieIniPath, newConfig);

                    // Reload the configuration
                    Process reloadProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = StartExePath,
                            Arguments = "/reload",
                            UseShellExecute = true
                        }
                    };

                    reloadProcess.Start();
                    reloadProcess.WaitForExit();
                }

                // Launch Viber in the sandbox using Start.exe
                Process sandboxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = StartExePath,
                        Arguments = $"/box:{account.SandboxName} \"{_viberPath}\"",
                        UseShellExecute = true
                    }
                };

                sandboxProcess.Start();

                WriteColor($"Viber launched successfully in sandbox: {account.SandboxName}", ConsoleColor.Green);
                WriteColor("Note: First time setup will require phone verification.", ConsoleColor.Yellow);
            }
            catch (Exception ex)
            {
                WriteColor($"Error launching Viber: {ex.Message}", ConsoleColor.Red);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void DeleteAccount()
        {
            Console.Clear();
            Console.WriteLine("=== DELETE VIBER ACCOUNT ===");
            Console.WriteLine();

            if (ViberAccounts.Count == 0)
            {
                WriteColor("No accounts have been added yet.", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // List available accounts
            for (int i = 0; i < ViberAccounts.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ViberAccounts[i].Name}");
            }

            Console.WriteLine();
            Console.Write("Enter the number of the account to delete (or 0 to cancel): ");

            if (!int.TryParse(Console.ReadLine(), out int accountNumber) ||
                accountNumber < 1 ||
                accountNumber > ViberAccounts.Count)
            {
                if (accountNumber != 0)
                    WriteColor("Invalid selection.", ConsoleColor.Red);

                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            // Get the selected account
            var account = ViberAccounts[accountNumber - 1];

            Console.Write($"Are you sure you want to delete account '{account.Name}'? (y/n): ");
            string confirmation = Console.ReadLine()?.Trim().ToLower();

            if (confirmation != "y")
            {
                WriteColor("Account deletion cancelled.", ConsoleColor.Yellow);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            try
            {
                // Remove the sandbox configuration
                string sandboxieIniPath = Path.Combine(Path.GetDirectoryName(StartExePath), "Sandboxie.ini");

                if (File.Exists(sandboxieIniPath))
                {
                    string[] iniLines = File.ReadAllLines(sandboxieIniPath);
                    List<string> newConfig = new List<string>();

                    bool inTargetSection = false;

                    foreach (string line in iniLines)
                    {
                        // Check if we're entering a section header
                        if (line.Trim().StartsWith("[") && line.Trim().EndsWith("]"))
                        {
                            // If we were in the target section, we're now leaving it
                            inTargetSection = line.Trim().Equals($"[{account.SandboxName}]", StringComparison.OrdinalIgnoreCase);

                            // Only add the line if it's not our target section
                            if (!inTargetSection)
                            {
                                newConfig.Add(line);
                            }
                        }
                        else if (!inTargetSection)
                        {
                            // Only keep lines that aren't part of our target section
                            newConfig.Add(line);
                        }
                    }

                    // Save the modified config
                    File.WriteAllLines(sandboxieIniPath, newConfig);

                    // Reload the configuration
                    Process reloadProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = StartExePath,
                            Arguments = "/reload",
                            UseShellExecute = true
                        }
                    };

                    reloadProcess.Start();
                    reloadProcess.WaitForExit();
                }

                // Remove the account from our list
                ViberAccounts.RemoveAt(accountNumber - 1);

                // Save the updated accounts list
                SaveAccounts();

                WriteColor($"Account '{account.Name}' deleted successfully.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                WriteColor($"Error deleting account: {ex.Message}", ConsoleColor.Red);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void LoadAccounts()
        {
            try
            {
                if (File.Exists(AccountsFile))
                {
                    string[] lines = File.ReadAllLines(AccountsFile);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length >= 2)
                        {
                            ViberAccounts.Add(new ViberAccount
                            {
                                Name = parts[0],
                                SandboxName = parts[1]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteColor($"Error loading accounts: {ex.Message}", ConsoleColor.Red);
            }
        }

        private static void SaveAccounts()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                List<string> lines = new List<string>();
                foreach (var account in ViberAccounts)
                {
                    lines.Add($"{account.Name}|{account.SandboxName}");
                }

                File.WriteAllLines(AccountsFile, lines);
            }
            catch (Exception ex)
            {
                WriteColor($"Error saving accounts: {ex.Message}", ConsoleColor.Red);
            }
        }

        private static void WriteColor(string message, ConsoleColor color)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }
    }

    /// <summary>
    /// Simple class to represent a Viber account
    /// </summary>
    class ViberAccount
    {
        public string Name { get; set; }
        public string SandboxName { get; set; }
    }
}