using System;
using System.Collections.Generic;
using System.Text;
using static HangarBay.Generics;

namespace HangarBay
{
    public static class ModCreator
    {
        public static async Task<bool> ConvertFolderToMod(
            string inputFolder,
            string modName,
            string thisModType,
                        string description, string authorName, string authorWebsite, List<string> modImagePath,

            string? outputRootPath = null)
        {
            if (string.IsNullOrWhiteSpace(inputFolder) || !Directory.Exists(inputFolder))
            {
                Console.WriteLine($"Error: Input folder not found or invalid: {inputFolder}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(modName))
            {
                Console.WriteLine("Error: Mod name cannot be empty.");
                return false;
            }

            modName = modName.Trim();
            string modId = modName.Replace(" ", "_").ToLowerInvariant();

            // Default output location if we're given nothing!
            outputRootPath ??= Path.Combine(Environment.CurrentDirectory, "Mods", modName);
            Directory.CreateDirectory(outputRootPath);

            Console.WriteLine($"Converting folder '{inputFolder}' → mod '{modName}'");
            Console.WriteLine($"Output path: {outputRootPath}");

            // Copy all the assets except the stuff we know is basula
            CopyAllRelevantFiles(inputFolder, Path.Combine(outputRootPath, "Assets"), true);

            // Generate mod.json
            var prefabsDir = Path.Combine(outputRootPath, "Prefabs");
            Directory.CreateDirectory(prefabsDir);

            var prefabFiles = Directory.GetFiles(prefabsDir, "*.prefab", SearchOption.AllDirectories)
                                       .Select(p => Path.GetRelativePath(outputRootPath, p).Replace('\\', '/'))
                                       .ToArray();

            var manifest = new ModManifest(
                id: modId,
                modtype: thisModType,
                name: modName,
                prefabPaths: prefabFiles.Length > 0 ? prefabFiles : null
            );

            string jsonPath = Path.Combine(outputRootPath, "mod.json");
            var json = await BinaryConverter.NCObjectToByteArrayAsync<ModManifest>(manifest);
            await File.WriteAllBytesAsync(jsonPath, json);

            Console.WriteLine($"Created manifest: {jsonPath}");

            var modDetails = new ModDetails(
                Name: modName,
                Description: description,
                Type: prefabFiles.Length > 0 ? "Hybrid" : "Assets",
                AuthorName: authorName,
                AuthorWebsite: authorWebsite,
                ModImagePath: modImagePath, 
                Hash: new List<ulong>(),
                CreatedAt: DateTime.UtcNow
            );

            string detailsPath = Path.Combine(outputRootPath, $"{modId}.moddetails");
            var detailsBytes = await BinaryConverter.NCObjectToByteArrayAsync(modDetails);
            await File.WriteAllBytesAsync(detailsPath, detailsBytes);
            Console.WriteLine($"Created ModDetails file: {detailsPath}");

            Console.WriteLine("\nConversion complete! Mod folder ready at:");
            Console.WriteLine(outputRootPath);
            Console.WriteLine("Place this folder in your game's Mods/ directory.");

            return true;
        }


        private static void CopyAllRelevantFiles(string source, string target, bool recursive)
        {
            Directory.CreateDirectory(target);

            // Copy files (skip known code/build artifacts)
            foreach (var file in Directory.GetFiles(source))
            {
                string fileName = Path.GetFileName(file);
                if (IsCodeOrBuildArtifact(fileName)) continue; // skip .cs, .csproj, .dll (unless in Scripts/), .pdb, etc.

                string dest = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
                Console.WriteLine($"Copied asset candidate: {Path.GetRelativePath(source, file)}");
            }

            // Recurse subfolders
            if (recursive)
            {
                foreach (var dir in Directory.GetDirectories(source))
                {
                    CopyAllRelevantFiles(dir, Path.Combine(target, Path.GetRelativePath(source, dir)), true);
                }
            }
        }

        private static bool IsCodeOrBuildArtifact(string fileName)
        {
            var lower = fileName.ToLowerInvariant();
            return lower.EndsWith(".cs") ||
                   lower.EndsWith(".csproj") ||
                   lower.EndsWith(".sln") ||
                   lower.EndsWith(".user") ||
                   lower.EndsWith(".pdb") ||
                   lower.EndsWith(".deps.json") ||
                   lower.EndsWith(".runtimeconfig.json") ||
                   fileName.StartsWith("."); // hidden files
        }





    }
}
