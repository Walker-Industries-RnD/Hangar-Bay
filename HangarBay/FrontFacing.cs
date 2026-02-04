using DouglasDwyer.CasCore;
using Stride.Core.IO;
using Stride.Core.Serialization.Contents;
using Stride.Core.Storage;
using System;
using System.Collections.Generic;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using static HangarBay.Generics;

namespace HangarBay
{
    //The stuff you want to use 
    public static class FrontFacing
    {

        public static class ModContentRegistry
        {
            private static readonly Dictionary<string, ContentManager> _contentManagers
                = new(StringComparer.OrdinalIgnoreCase);

            public static ContentManager GetOrCreate(
                string modId,
                string modDirectory)
            {
                if (_contentManagers.TryGetValue(modId, out var existing))
                    return existing;

                var cm = BuildContentManager(modDirectory, modId);
                _contentManagers[modId] = cm;
                return cm;
            }

            public static void Unload(string modId)
            {
                if (_contentManagers.TryGetValue(modId, out var cm))
                {
                    _contentManagers.Remove(modId);
                }
            }
        }



        public static async Task EnableMod(string modDirectory, string modId, byte[] userKey)
        {

            // Ensure mod folder exists
            if (!Directory.Exists(modDirectory))
                throw new DirectoryNotFoundException($"Mod folder '{modDirectory}' does not exist.");

            // Paths for enabled marker & signature
            var enabledPath = Path.Combine(modDirectory, ".enabled");
            var sigPath = enabledPath + ".sig";

            // Payload to save (YEAH I'M TAKING A SHORTCUT DEAL WITH IT)
            var payload = new EnabledPayload
            {
                Mod = modId,
                EnabledAtUtc = DateTime.UtcNow
            };

            // Serialize payload to byte[]
            var data = await BinaryConverter.NCObjectToByteArrayAsync(payload);

            // Write payload & HMAC signature
            File.WriteAllBytes(enabledPath, data);
            var sig = Sign(data, userKey);
            File.WriteAllBytes(sigPath, sig);

            // Register mod in content registry (auto-build ContentManager)
            ModContentRegistry.GetOrCreate(modId, modDirectory);

            Console.WriteLine($"Mod '{modId}' enabled and registered.");
        }



        public static async Task DisableMod(string modDirectory, string modId)
        {
            var enabledPath = Path.Combine(modDirectory, ".enabled");
            var sigPath = enabledPath + ".sig";

            if (File.Exists(enabledPath))
                File.Delete(enabledPath);

            if (File.Exists(sigPath))
                File.Delete(sigPath);

            ModContentRegistry.Unload(modId);  

            if (_modContexts.TryGetValue(modId, out var context))
            {
                try
                {
                    //When using this, you must 
                    // - Destroy all instantiated entities from this mod's prefabs
                    // - Clear any cached script instances, event subscriptions, etc.
                    // - REMOVE ALL MOD ENTITIES

                    context.Unload();  
                    _modContexts.Remove(modId);

                    Console.WriteLine($"Unloaded script assemblies for mod '{modId}'.");

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to unload mod '{modId}' scripts: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"No script context found for mod '{modId}' — nothing to unload.");
            }

            Console.WriteLine($"Mod '{modId}' fully disabled.");
        }


        public static bool IsModEnabled(string modDirectory, byte[] userKey)
        {
            var enabledPath = Path.Combine(modDirectory, ".enabled");
            var sigPath = enabledPath + ".sig";

            if (!File.Exists(enabledPath) || !File.Exists(sigPath))
                return false;

            var data = File.ReadAllBytes(enabledPath);
            var sig = File.ReadAllBytes(sigPath);

            return Verify(data, sig, userKey);
        }


        private static byte[] Sign(byte[] data, byte[] key)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(data);
        }

        private static bool Verify(byte[] data, byte[] sig, byte[] key)
        {
            using var hmac = new HMACSHA256(key);
            var expected = hmac.ComputeHash(data);
            return CryptographicOperations.FixedTimeEquals(expected, sig);
        }

        public sealed record EnabledPayload
        {
            public string Mod { get; init; } = string.Empty;
            public DateTime EnabledAtUtc { get; init; }
        }


        private static ContentManager BuildContentManager(string modDirectory, string modId)
        {
            var contentDbPath = Path.Combine(modDirectory, "data", "db");
            Directory.CreateDirectory(contentDbPath);

            var rootId = "/mod/" + modId.ToLowerInvariant();

            var fsProvider = new FileSystemProvider(rootId, contentDbPath);

            var objectDb = new ObjectDatabase(rootId, "index", "/local/db");
            var dbProvider = new DatabaseFileProvider(objectDb);
            var dbService = new DatabaseFileProviderService(dbProvider);

            return new ContentManager(dbService);
        }

        public enum Strictness {             Strict,
            Moderate,
            Lenient
        }

        private static readonly Dictionary<string, AssemblyLoadContext> _modContexts = new();

        public static void LoadModScripts(string modDirectory, string modId, Strictness strict = Strictness.Strict)
        {
            var scriptsDir = Path.Combine(modDirectory, "Scripts");
            if (!Directory.Exists(scriptsDir)) return;

            var modTypeDetails = LoadModTypeDetailsForMod(modDirectory, modId); 

            //Build the CasPolicy using the dictionary from mod type
            var policyBuilder = new CasPolicyBuilder()
                .WithDefaultSandbox(); // safe BCL basics (collections, math, strings, etc.)

            if (modTypeDetails?.AllowedTypesOrMembers != null && modTypeDetails.AllowedTypesOrMembers.Count > 0)
            {
                policyBuilder.ApplyModTypeConfig(modTypeDetails.AllowedTypesOrMembers);
            }
            else
            {
                Console.WriteLine($"Warning: No AllowedTypesOrMembers defined for mod '{modId}' — using strict default sandbox.");
            }


            var finalPolicy = policyBuilder.Build();

            // Create collectible sandboxed loader for this mod
            var loader = new CasAssemblyLoader(finalPolicy, isCollectible: true);

            // Store it so DisableMod can unload it later
            modLoaders[modId] = loader; 

            foreach (var dllPath in Directory.GetFiles(scriptsDir, "*.dll", SearchOption.AllDirectories))
            {
                try
                {

                    var assembly = loader.LoadFromAssemblyPath(dllPath);
                    Console.WriteLine($"Sandbox-loaded mod script: {assembly.FullName} for '{modId}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to sandbox-load DLL {Path.GetFileName(dllPath)}: {ex.Message}");

                    switch (strict)
                    {
                        case Strictness.Strict:
                            {
                                Console.WriteLine($"Strict strictness: failure to load {Path.GetFileName(dllPath)}.");
                                return;
                            }
                            case Strictness.Moderate:
                            {
                                Console.WriteLine($"Moderate strictness: continuing despite failure to load {Path.GetFileName(dllPath)}.");
                                break;
                            }
                            case Strictness.Lenient
                            :
                            {
                                Console.WriteLine($"Lenient strictness: ignoring failure to load {Path.GetFileName(dllPath)}.");
                                break;
                            }
                    }

                }
            }

            Console.WriteLine($"All scripts for mod '{modId}' loaded under sandbox policy.");
        }

        private static ModTypeDetails? LoadModTypeDetailsForMod(string modDirectory, string modId)
        {
            // Adjust path/filename to match how you save ModTypeDetails
            string detailsPath = Path.Combine(modDirectory, $"{modId}.moddetails");

            if (!File.Exists(detailsPath))
            {
                Console.WriteLine($"No mod type details file found at {detailsPath} — using default strict sandbox.");
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(detailsPath);
                return BinaryConverter.NCByteArrayToObjectAsync<ModTypeDetails>(bytes).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load mod type details for {modDirectory}: {ex.Message}");
                return null;
            }
        }

        private static readonly Dictionary<string, CasAssemblyLoader> modLoaders = new();



    }
}
