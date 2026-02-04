using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using static HangarBay.Generics;

namespace HangarBay
{
    public static class ModValidation
    {
        public static async Task ValidateMod(string modFolderPath, string systemDllRoot, bool warnOnly = true)
        {

            var modFilePath = Path.Combine(
    Path.Combine(AppContext.BaseDirectory, "Engine", "Security", "ModStyles"),
    "testmod.ModStyle"
);
            var modBytes = await File.ReadAllBytesAsync(modFilePath);
            var modType = await BinaryConverter.NCByteArrayToObjectAsync<ModConstructor>(modBytes);


            try
            {
                var validExtensions = await ValidateExtensions(modType, modFolderPath);
                if (!validExtensions)
                {
                    throw new Exception("Invalid Extensions Found.");
                }

                 VerifyModDlls(modFolderPath);
                await ValidateDllRules(modType, modFolderPath, warnOnly);
                await CheckRequiredDlls(modType, modFolderPath);
                ReplaceWithSystemDlls(modFolderPath, systemDllRoot);

            }

            catch (Exception ex)
            {
                throw new Exception($"An error has occurred: {ex.Message}", ex);
            }

        }

        private static readonly string TrustedDllFolder = Path.Combine(AppContext.BaseDirectory, "Engine", "Security", "DLLChecks");

        public static bool VerifyModDlls(string modFolder)
        {
            string scriptsDir = Path.Combine(modFolder, "Scripts");
            if (!Directory.Exists(scriptsDir))
                return true; // nothing to check

            bool allValid = true;

            foreach (var dllPath in Directory.GetFiles(scriptsDir, "*.dll", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(dllPath);
                string trustedPath = Path.Combine(TrustedDllFolder, fileName);

                if (!File.Exists(trustedPath))
                {
                    Console.WriteLine($"[WARN] Trusted DLL missing for comparison: {fileName}");
                    allValid = false;
                    continue;
                }

                // 1. Strong-name check
                try
                {
                    var asm = AssemblyName.GetAssemblyName(dllPath);
                    var trustedAsm = AssemblyName.GetAssemblyName(trustedPath);

                    if (!asm.GetPublicKey().SequenceEqual(trustedAsm.GetPublicKey()))
                    {
                        Console.WriteLine($"[FAIL] Strong-name mismatch: {fileName}");
                        allValid = false;
                        continue;
                    }
                }
                catch
                {
                    Console.WriteLine($"[FAIL] Could not read strong-name for {fileName}");
                    allValid = false;
                    continue;
                }

                // 2. SHA256 hash
                byte[] dllHash, trustedHash;
                using (var sha = SHA256.Create())
                {
                    dllHash = sha.ComputeHash(File.ReadAllBytes(dllPath));
                    trustedHash = sha.ComputeHash(File.ReadAllBytes(trustedPath));
                }

                if (!dllHash.SequenceEqual(trustedHash))
                {
                    Console.WriteLine($"[WARN] SHA256 mismatch: {fileName}");
                }

            }

            return allValid;
        }



        private static async Task<bool> ValidateExtensions(ModConstructor modType, string modFolderPath)
        {
            Console.WriteLine("Validating mod files against ruleset...");

            var rules = modType.Ruleset;
            var allFiles = Directory.GetFiles(modFolderPath, "*.*", SearchOption.AllDirectories)
                                    .Select(Path.GetExtension)
                                    .Select(ext => ext.ToLowerInvariant())
                                    .ToHashSet();

            // Check required extensions (MustInclude)
            foreach (var required in rules.MustInclude)
            {
                if (!allFiles.Contains(required.ToLowerInvariant()))
                {
                    Console.WriteLine($"Error: Required file extension '{required}' missing in mod '{modType.Metadata.ModName}'.");
                    return false;
                }
            }

            // Check disallowed extensions (MustExclude)
            foreach (var disallowed in rules.MustExclude)
            {
                if (allFiles.Contains(disallowed.ToLowerInvariant()))
                {
                    Console.WriteLine($"Error: Disallowed file extension '{disallowed}' found in mod '{modType.Metadata.ModName}'. Mod build aborted.");
                    return false;
                }
            }

            Console.WriteLine("Mod files validated successfully — all required present, no disallowed found.");
            return true;
        }

        private static async Task ValidateDllRules(ModConstructor modType, string modFolderPath, bool warnOnly = true)
        {
            var rules = modType.Ruleset;
            var scriptsDir = Path.Combine(modFolderPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return;

            var dllFiles = Directory.GetFiles(scriptsDir, "*.dll", SearchOption.AllDirectories);

            // Check if OnlyAllow list exists and has entries
            bool hasOnlyAllowList = rules.OnlyAllow != null && rules.OnlyAllow.Count > 0;

            bool dllsRequired = modType.Ruleset.MustIncludeDLL?.Count > 0;

            if (!Directory.Exists(scriptsDir))
            {
                if (dllsRequired)
                    throw new InvalidOperationException("Scripts folder missing, required DLLs cannot be validated.");
                else
                    return; // safe to skip for asset-only mods
            }


            foreach (var dll in dllFiles)
            {
                string dllName = Path.GetFileName(dll).ToLowerInvariant();

                if (rules.MustExcludeDLL.Contains(dllName))
                {
                    string msg = $"Warning: Disallowed DLL '{dllName}' found in mod '{modType.Metadata.ModName}' (excluded by ruleset).";
                    Console.WriteLine(msg);
                    if (!warnOnly) throw new InvalidOperationException(msg);
                }

                // Apply OnlyAllow list if it exists (whitelist overrides everything)
                if (hasOnlyAllowList && !rules.OnlyAllow.Contains(dllName))
                {
                    string msg = $"Warning: DLL '{dllName}' not in OnlyAllow list for mod '{modType.Metadata.ModName}'. DLL will be restricted.";
                    Console.WriteLine(msg);
                    if (!warnOnly) throw new InvalidOperationException(msg);
                }
            }

            await CheckRequiredDlls(modType, modFolderPath, warnOnly);
        }
        private static async Task CheckRequiredDlls(ModConstructor modType, string modFolderPath, bool warnOnly = true)
        {
            var rules = modType.Ruleset;
            var scriptsDir = Path.Combine(modFolderPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return;

            var existingDlls = Directory.EnumerateFiles(scriptsDir, "*.dll", SearchOption.AllDirectories)
                                .Select(Path.GetFileName)
                                .Select(n => n.ToLowerInvariant())
                                .ToHashSet();

            foreach (var required in rules.MustIncludeDLL)
            {
                if (!existingDlls.Contains(required.ToLowerInvariant()))
                {
                    string msg = $"Warning: Required DLL '{required}' missing in mod '{modType.Metadata.ModName}'.";
                    Console.WriteLine(msg);
                    if (!warnOnly) throw new InvalidOperationException(msg);
                }
            }
        }

        private static void ReplaceWithSystemDlls(string modFolderPath, string systemDllRoot)
        {
            var scriptsDir = Path.Combine(modFolderPath, "Scripts");
            if (!Directory.Exists(scriptsDir)) return;

            var modDlls = Directory.GetFiles(scriptsDir, "*.dll");
            foreach (var modDll in modDlls)
            {
                string dllName = Path.GetFileName(modDll);
                string systemDll = Path.Combine(systemDllRoot, dllName);

                if (File.Exists(systemDll))
                {
                    var modVersion = FileVersionInfo.GetVersionInfo(modDll).FileVersion;
                    var systemVersion = FileVersionInfo.GetVersionInfo(systemDll).FileVersion;

                    var modVersionParse = Version.Parse(modVersion);
                    var systemVersionParse = Version.Parse(systemVersion);


                    if (systemVersionParse >= modVersionParse)
                    {
                        File.Delete(modDll);
                        File.Copy(systemDll, modDll, true);
                        Console.WriteLine($"Replaced mod DLL '{dllName}' with system version for compatibility.");
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Mod DLL '{dllName}' version {modVersion} differs from system {systemVersion} — keeping mod version.");
                    }
                }
            }
        }

        public static object? CallModInterface(
            ModConstructor modType,
            Assembly modAssembly,
            string interfaceName,
            string methodName,
            params object[] args)
        {
            if (modAssembly == null)
            {
                Console.WriteLine("Error: Mod assembly is null.");
                return null;
            }

            var rules = modType.Ruleset;

            // Security: Check OnlyAllow list first if it exists
            bool hasOnlyAllowList = rules.OnlyAllow != null && rules.OnlyAllow.Count > 0;
            if (hasOnlyAllowList)
            {
                string assemblyName = modAssembly.GetName().Name?.ToLowerInvariant() ?? "";

                // Check if the assembly itself is in the OnlyAllow list
                if (!rules.OnlyAllow.Contains(assemblyName) &&
                    !rules.OnlyAllow.Contains($"{assemblyName}.dll"))
                {
                    Console.WriteLine($"Security warning: Assembly '{assemblyName}' not in OnlyAllow list for mod '{modType.Metadata.ModName}'.");
                    return null;
                }
            }

            // Security: Only allow whitelisted method names
            if (!rules.DefaultCalls.Contains(methodName) &&
                !rules.PublicDefaultCalls.Contains(methodName))
            {
                Console.WriteLine($"Security warning: Method '{methodName}' not allowed by mod rules for '{modType.Metadata.ModName}'.");
                return null;
            }

            try
            {
                // Find types that implement the interface (case-insensitive name match)
                var candidateTypes = modAssembly.GetTypes()
                    .Where(t => t.GetInterfaces().Any(i =>
                        string.Equals(i.Name, interfaceName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(i.FullName, interfaceName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (candidateTypes.Count == 0)
                {
                    Console.WriteLine($"No type found implementing interface '{interfaceName}' in mod assembly.");
                    return null;
                }

                if (candidateTypes.Count > 1)
                {
                    Console.WriteLine($"Warning: Multiple types implement '{interfaceName}'. Using first: {candidateTypes[0].FullName}");
                }

                var targetType = candidateTypes[0];

                // Try to find the method (instance or static)
                var method = targetType.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);

                if (method == null)
                {
                    Console.WriteLine($"Method '{methodName}' not found on type '{targetType.FullName}'.");
                    return null;
                }

                // Handle static vs instance
                object? instance = null;
                if (!method.IsStatic)
                {
                    try
                    {
                        instance = Activator.CreateInstance(targetType);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create instance of '{targetType.FullName}': {ex.Message}");
                        return null;
                    }
                }

                // Invoke with proper error handling
                try
                {
                    return method.Invoke(instance, args);
                }
                catch (TargetInvocationException tie)
                {
                    Console.WriteLine($"Mod method '{methodName}' threw an exception: {tie.InnerException?.Message ?? tie.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to invoke method '{methodName}': {ex.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error while calling mod interface: {ex.Message}");
                return null;
            }
        }
    }

}
