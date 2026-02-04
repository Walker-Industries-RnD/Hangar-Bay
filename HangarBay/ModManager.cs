using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static HangarBay.Generics;

namespace HangarBay
{
    public static class ModManager
    {
        public static readonly string ModsRoot =
            Path.Combine(AppContext.BaseDirectory, "Mods");


        public static IEnumerable<ModDetails> ListMods()
        {
            if (!Directory.Exists(ModsRoot))
                yield break;

            foreach (var dir in Directory.GetDirectories(ModsRoot))
            {
                var detailsPath = Directory.GetFiles(dir, "*.ModDetails").FirstOrDefault();
                if (detailsPath == null) continue;

                var details = LoadModDetails(detailsPath);
                if (details != null)
                    yield return details;
            }
        }


        public static ModDetails GetMod(string modName)
        {
            var modDir = Path.Combine(ModsRoot, modName);
            if (!Directory.Exists(modDir))
                throw new DirectoryNotFoundException($"Mod '{modName}' not found.");

            var detailsPath = Directory.GetFiles(modDir, "*.ModDetails").FirstOrDefault();
            if (detailsPath == null)
                throw new FileNotFoundException("No ModDetails file found for this mod.");

            var details = LoadModDetails(detailsPath);
            if (details == null)
                throw new InvalidDataException("Invalid ModDetails file.");

            return details;
        }


        public static async Task UpdateMod(
            string modName,
            Action<ModDetails> updateAction)
        {
            var modDir = Path.Combine(ModsRoot, modName);
            if (!Directory.Exists(modDir))
                throw new DirectoryNotFoundException($"Mod '{modName}' not found.");

            var detailsPath = Directory.GetFiles(modDir, "*.ModDetails").FirstOrDefault();
            if (detailsPath == null)
                throw new FileNotFoundException("No ModDetails file found to update.");

            var modDetails = LoadModDetails(detailsPath);
            if (modDetails == null)
                throw new InvalidDataException("Invalid ModDetails file.");

            updateAction(modDetails);

            byte[] data = await BinaryConverter.NCObjectToByteArrayAsync(modDetails);
            await File.WriteAllBytesAsync(detailsPath, data);
        }


        public static void DeleteMod(string modName)
        {
            var modDir = Path.Combine(ModsRoot, modName);
            if (!Directory.Exists(modDir))
                throw new DirectoryNotFoundException($"Mod '{modName}' not found.");

            Directory.Delete(modDir, recursive: true);
        }


        private static ModDetails? LoadModDetails(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                return BinaryConverter.NCByteArrayToObjectAsync<ModDetails>(data).GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }
    }
}
