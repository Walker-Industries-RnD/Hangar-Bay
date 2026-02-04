using Pariah_Cybersecurity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using WISecureData;
using static HangarBay.Generics;

namespace HangarBay
{
    public static class ModTypeHandler
    {
        public static async Task<bool> CreateModType(
            string modExtension,
            string modVersion,
            ModMetadata metadata,
            ModRules rules,
            SecureData bankName,
            SecureData dataPhrase,
            ModTypeDetails modTypeDetails,
            string? customOutputDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(modExtension)) throw new ArgumentException("Mod extension cannot be empty.", nameof(modExtension));
            if (string.IsNullOrWhiteSpace(modVersion)) throw new ArgumentException("Mod version cannot be empty.", nameof(modVersion));

            string solutionRoot = SolutionLocator.GetSolutionRoot();
            string outDir = customOutputDirectory ?? Path.Combine(solutionRoot, "Engine", "Security", "ModStyles");
            Directory.CreateDirectory(outDir);

            string modFileName = $"{modExtension}.ModStyle";
            string modFilePath = Path.Combine(outDir, modFileName);

            if (File.Exists(modFilePath))
            {
                Console.WriteLine($"Mod '{modFileName}' already exists — skipping creation.");
                return false;
            }

            var constructedMod = new ModConstructor(metadata, rules);

            byte[] modBytes = await BinaryConverter.NCObjectToByteArrayAsync(constructedMod);
            await File.WriteAllBytesAsync(modFilePath, modBytes);

            // Generate PQC keys
            var (publicKeyDict, privateKeyDict) = await EasyPQC.Signatures.CreateKeys();

            // Secret bank
            string bankDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Stride3D Secret Banks");
            bool bankExists = await DataHandler.SecretManager.CheckIfBankExists(bankDir, bankName.ConvertToString());

            if (!bankExists)
            {
                var bankKey = DataHandler.DeviceIdentifier.GetUserBoundMasterSecret(dataPhrase.ConvertToString());
                await DataHandler.SecretManager.CreateBank(bankDir, bankName.ConvertToString(), null, bankKey.ConvertToString());
            }

            byte[] privateKeyBytes = await BinaryConverter.NCObjectToByteArrayAsync(privateKeyDict);
            var privKeyData = new DataHandler.SecretManager.PublicKeyFileInit(
                "Private Key",
                null,
                Convert.ToBase64String(privateKeyBytes).ToSecureData()
            );

            var masterKey = DataHandler.DeviceIdentifier.GetUserBoundMasterSecret(dataPhrase.ConvertToString());
            await DataHandler.SecretManager.AddPublicSecret(bankDir, bankName.ConvertToString(), privKeyData, masterKey.ConvertToString());

            // Save public key
            string securityDir = Path.Combine(AppContext.BaseDirectory, "Security");
            Directory.CreateDirectory(securityDir);

            string engineName = Assembly.GetEntryAssembly()?.GetName().Name ?? "UnknownEngine";
            string publicKeyPath = Path.Combine(securityDir, $"{engineName}.ModSigning.PublicKey.bin");

            byte[] publicKeyBytes = await BinaryConverter.NCObjectToByteArrayAsync(publicKeyDict);
            await File.WriteAllBytesAsync(publicKeyPath, publicKeyBytes);

            // Sign mod file
            byte[] modSignature = await EasyPQC.Signatures.CreateSignature(privateKeyDict, Convert.ToBase64String(modBytes));
            string signsDir = Path.Combine(securityDir, "Signs");
            Directory.CreateDirectory(signsDir);

            string signaturePath = Path.Combine(signsDir, $"{engineName}.ModSigning.v{modVersion}.{modExtension}.sig");
            await File.WriteAllBytesAsync(signaturePath, modSignature);

            Console.WriteLine($"Created mod type: {modFileName} (v{modVersion})");

            // Hash metadata images before saving
            modTypeDetails.Hash.Clear();

            string baseDir = Path.GetDirectoryName(modFilePath)!;

            foreach (var relativePath in modTypeDetails.TypeImagePath)
            {
                string resolvedPath = Path.GetFullPath(
                    Path.Combine(baseDir, relativePath)
                );

                if (!File.Exists(resolvedPath))
                    throw new FileNotFoundException($"Image not found: {resolvedPath}");

                byte[] bytes = await File.ReadAllBytesAsync(resolvedPath);
                modTypeDetails.Hash.Add(xxHash.CalculateHash(bytes));
            }

            byte[] metaBytes = await BinaryConverter.NCObjectToByteArrayAsync(modTypeDetails);
            string metadataPath = Path.Combine(outDir, $"{modExtension}.ModStyleMetaData");
            await File.WriteAllBytesAsync(metadataPath, metaBytes);

            return true;
        }

        public static async Task<bool> UpdateModType(
            string modExtension,
            ModConstructor updatedMod,
            ModTypeDetails modTypeDetails,
            bool mergeIfExists = true,
            string? customModDirectory = null,
            string bankName = "Secrets",
            string dataPhrase = "Random System",
            string? customOutputDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(modExtension)) throw new ArgumentException("Mod extension cannot be empty.", nameof(modExtension));

            string solutionRoot = SolutionLocator.GetSolutionRoot();
            string modDirectory = customModDirectory ?? Path.Combine(solutionRoot, "Engine", "Security", "ModStyles");
            Directory.CreateDirectory(modDirectory);

            string filePath = Path.Combine(modDirectory, $"{modExtension}.ModStyle");
            if (!File.Exists(filePath)) return false;

            var existingBytes = await File.ReadAllBytesAsync(filePath);
            var existingMod = await BinaryConverter.NCByteArrayToObjectAsync<ModConstructor>(existingBytes);

            if (mergeIfExists)
            {
                existingMod.Metadata = new ModMetadata(
                    updatedMod.Metadata.ModName ?? existingMod.Metadata.ModName,
                    updatedMod.Metadata.ModDescription ?? existingMod.Metadata.ModDescription,
                    updatedMod.Metadata.ModWebsite ?? existingMod.Metadata.ModWebsite,
                    updatedMod.Metadata.AuthorName ?? existingMod.Metadata.AuthorName,
                    updatedMod.Metadata.AuthorBio ?? existingMod.Metadata.AuthorBio,
                    updatedMod.Metadata.AuthorWebsite ?? existingMod.Metadata.AuthorWebsite,
                    updatedMod.Metadata.ModImagePath ?? existingMod.Metadata.ModImagePath,
                    updatedMod.Metadata.AuthorImagePath ?? existingMod.Metadata.AuthorImagePath,
                    updatedMod.Metadata.ModVersion ?? existingMod.Metadata.ModVersion
                );

                existingMod.Ruleset.MustInclude.UnionWith(updatedMod.Ruleset.MustInclude);
                existingMod.Ruleset.MustExclude.UnionWith(updatedMod.Ruleset.MustExclude);
                existingMod.Ruleset.MustIncludeDLL.UnionWith(updatedMod.Ruleset.MustIncludeDLL);
                existingMod.Ruleset.MustExcludeDLL.UnionWith(updatedMod.Ruleset.MustExcludeDLL);
                existingMod.Ruleset.OnlyAllow.UnionWith(updatedMod.Ruleset.OnlyAllow);
                existingMod.Ruleset.DefaultCalls.UnionWith(updatedMod.Ruleset.DefaultCalls);
                existingMod.Ruleset.PublicDefaultCalls.UnionWith(updatedMod.Ruleset.PublicDefaultCalls);
            }
            else
            {
                existingMod = updatedMod;
            }

            byte[] mergedBytes = await BinaryConverter.NCObjectToByteArrayAsync(existingMod);
            await File.WriteAllBytesAsync(filePath, mergedBytes);

            // Re-sign
            string bankDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Stride3D Secret Banks");
            var masterKey = DataHandler.DeviceIdentifier.GetUserBoundMasterSecret(dataPhrase.ToSecureData().ConvertToString());
            var privKeyData = await DataHandler.SecretManager.GetPublicSecret(bankDir, bankName, "Private Key", masterKey.ConvertToString());
            var privateKeyDict = await BinaryConverter.NCByteArrayToObjectAsync<Dictionary<string, byte[]>>(Convert.FromBase64String(privKeyData.ConvertToString()));

            byte[] signature = await EasyPQC.Signatures.CreateSignature(privateKeyDict, Convert.ToBase64String(mergedBytes));
            string signsDir = Path.Combine(AppContext.BaseDirectory, "Security", "Signs");
            Directory.CreateDirectory(signsDir);

            string safeModName = existingMod.Metadata.ModName.Replace(" ", "_");
            string signaturePath = Path.Combine(signsDir, $"{safeModName}.ModSigning.v{existingMod.Metadata.ModVersion}.{modExtension}.sig");
            await File.WriteAllBytesAsync(signaturePath, signature);

            // Update metadata
            modTypeDetails.Hash.Clear();

            string baseDir = Path.GetDirectoryName(modDirectory)!;

            foreach (var relativePath in modTypeDetails.TypeImagePath)
            {
                string resolvedPath = Path.GetFullPath(
                    Path.Combine(baseDir, relativePath)
                );

                if (!File.Exists(resolvedPath))
                    throw new FileNotFoundException($"Image not found: {resolvedPath}");

                byte[] bytes = await File.ReadAllBytesAsync(resolvedPath);
                modTypeDetails.Hash.Add(xxHash.CalculateHash(bytes));
            }

            string outDir = customOutputDirectory ?? modDirectory;
            byte[] metaBytes = await BinaryConverter.NCObjectToByteArrayAsync(modTypeDetails);
            await File.WriteAllBytesAsync(Path.Combine(outDir, $"{modExtension}.ModStyleMetaData"), metaBytes);

            Console.WriteLine($"Updated mod type: {modExtension} → v{existingMod.Metadata.ModVersion}");
            return true;
        }

        public static async Task<bool> DeleteModType(string modExtension, string? versionToDelete = null, string? customModDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(modExtension)) throw new ArgumentException("Mod extension cannot be empty.", nameof(modExtension));

            string solutionRoot = SolutionLocator.GetSolutionRoot();
            string modDirectory = customModDirectory ?? Path.Combine(solutionRoot, "Engine", "Security", "ModStyles");

            if (!Directory.Exists(modDirectory)) return true;

            string modFilePath = Path.Combine(modDirectory, $"{modExtension}.ModStyle");
            if (File.Exists(modFilePath)) File.Delete(modFilePath);

            string signsDir = Path.Combine(AppContext.BaseDirectory, "Security", "Signs");
            if (!Directory.Exists(signsDir)) return true;

            string pattern = versionToDelete != null
                ? $"*.ModSigning*{versionToDelete}.{modExtension}.sig"
                : $"*.ModSigning*.{modExtension}.sig";

            foreach (var sig in Directory.GetFiles(signsDir, pattern))
            {
                try { File.Delete(sig); } catch { /* ignore */ }
            }

            return true;
        }
    }
}
