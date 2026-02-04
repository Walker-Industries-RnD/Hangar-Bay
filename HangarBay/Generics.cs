using System;
using System.Collections.Generic;
using System.Text;

namespace HangarBay
{
    public static class Generics
    {


        public record ModTypeDetails
        (
            string Name,                // Display name
            string Description,
            string Extension,           // .testmod (shown, not enforced)
            string? Website,
            string AuthorName,
            string? AuthorWebsite,
            List<string> TypeImagePath, // Icon / banner shown in UI / Relative
            List<ulong> Hash,

            // Sandbox configuration: which types and members external code is allowed to access (I love this system ngl)
            Dictionary<string, List<string>>? AllowedTypesOrMembers = null
        )
        {

            public static readonly Dictionary<string, List<string>> RecommendedGameplayDefaults = new()
            {
                // Safe math / basics (allow all public members)
                ["System.Math"] = new() { "*" },
                ["System.Collections.Generic.List`1"] = new() { "*" },
                ["System.Collections.Generic.Dictionary`2"] = new() { "*" },

                // Stride core types needed for typical scripts
                ["Stride.Core.Mathematics.Vector3"] = new() { "*" },
                ["Stride.Core.Mathematics.Quaternion"] = new() { "*" },
                ["Stride.Engine.TransformComponent"] = new() { "*" },  // Rotation/Position/Scale
                ["Stride.Engine.Entity"] = new() { "Transform", "GetComponent", "Components" }, // Limited access
                ["Stride.Engine.SyncScript"] = new() { "*" },   // Base class inheritance
                ["Stride.Engine.AsyncScript"] = new() { "*" },

                // Example game-specific read-only queries
                ["MyGame.PlayerController"] = new() { "GetHealth", "IsAlive" },
                // Add more as your game grows
            };
        }


        public record ModDetails
        (
            string Name,
            string Description,

            string Type,                // e.g. "Assets", "Gameplay", "Hybrid"

            string AuthorName,
            string? AuthorWebsite,

            List<string> ModImagePath,       // Thumbnail / banner / etc. relative
            List<ulong> Hash,

            DateTime CreatedAt
        );




        public record ModConstructor
        {
            public ModMetadata Metadata { get; set; } 
            public ModRules Ruleset { get; set; } 

            public ModConstructor() { }
            public ModConstructor(ModMetadata metadata, ModRules ruleset)
            {
                this.Metadata = metadata;
                this.Ruleset = ruleset;
            }

        }


        public record ModMetadata
        {
            public string ModName { get; set;  }
            public string ModDescription { get; set;  }
            public string ModWebsite { get; set;  }
            public string AuthorName { get; set;  }
            public string AuthorBio { get; set;  }
            public string AuthorWebsite { get; set;  }
            public string ModImagePath { get; set;  }
            public string AuthorImagePath { get; set;  }

            public string ModVersion { get; set;  }

            public ModMetadata() { }

            public ModMetadata(
                string modName = "Default Mods",
                string modDescription = "Mods You Can Implement",
                string modWebsite = "Unknown",
                string authorName = "Unknown",
                string authorBio = "Unknown",
                string authorWebsite = "Unknown",
                string modImagePath = "Unknown",
                string authorImagePath = "Unknown",
                string modVersion = "1.0.0")
            {
                ModName = modName;
                ModDescription = modDescription;
                ModWebsite = modWebsite;
                AuthorName = authorName;
                AuthorBio = authorBio;
                AuthorWebsite = authorWebsite;
                ModImagePath = modImagePath;
                AuthorImagePath = authorImagePath;
                ModVersion = modVersion;
            }
        }



        public class ModRules
        {
            public HashSet<string> MustInclude { get; set;  } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> MustExclude { get; set;  } = new(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> MustIncludeDLL { get; set;  } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> MustExcludeDLL { get; set;  } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> OnlyAllow { get; set;  } = new(StringComparer.OrdinalIgnoreCase); // Whitelist that overrides AllowedDLLs

            public HashSet<string> DefaultCalls { get; set;  } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> PublicDefaultCalls { get; set;  } = new(StringComparer.OrdinalIgnoreCase);

            public ModRules() { }

            public ModRules(
                IEnumerable<string>? mustInclude = null,
                IEnumerable<string>? mustExclude = null,
                IEnumerable<string>? mustIncludeDLL = null,
                IEnumerable<string>? mustExcludeDLL = null,
                IEnumerable<string>? onlyAllow = null,
                IEnumerable<string>? defaultCalls = null,
                IEnumerable<string>? publicDefaultCalls = null)
            {
                if (mustInclude != null)
                    foreach (var s in mustInclude) if (!string.IsNullOrWhiteSpace(s)) MustInclude.Add(s.Trim());

                if (mustExclude != null)
                    foreach (var s in mustExclude) if (!string.IsNullOrWhiteSpace(s)) MustExclude.Add(s.Trim());

                if (mustIncludeDLL != null)
                    foreach (var s in mustIncludeDLL) if (!string.IsNullOrWhiteSpace(s)) MustIncludeDLL.Add(s.Trim());

                if (mustExcludeDLL != null)
                    foreach (var s in mustExcludeDLL) if (!string.IsNullOrWhiteSpace(s)) MustExcludeDLL.Add(s.Trim());

                if (onlyAllow != null)
                    foreach (var s in onlyAllow) if (!string.IsNullOrWhiteSpace(s)) OnlyAllow.Add(s.Trim());

                if (defaultCalls != null)
                    foreach (var s in defaultCalls) if (!string.IsNullOrWhiteSpace(s)) DefaultCalls.Add(s.Trim());

                if (publicDefaultCalls != null)
                    foreach (var s in publicDefaultCalls) if (!string.IsNullOrWhiteSpace(s)) PublicDefaultCalls.Add(s.Trim());
            }
        }

        public record ModManifest
(
    string Id,
    string Modtype,
    string Name,
    string Version = "1.0.0",
    string Type = null!,           // or default value
    string Description = "Auto-converted folder mod",
    string Created = null!,        // set in constructor below
    string[]? PrefabPaths = null)
        {
            public ModManifest(
                string id,
                string modtype,
                string name,
                string? type = null,
                string? dll = null,
                string[]? prefabPaths = null) : this(
                    id,
                    modtype,
                    name,
                    "1.0.0",
                    type ?? (dll != null ? "Hybrid" : "AssetsOnly"),
                    "Auto-converted folder mod",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    prefabPaths?.Length > 0 ? prefabPaths : null)
            {
            }
        }




        public static class SolutionLocator
        {
            public static string GetSolutionRoot()
            {
                string dir = AppContext.BaseDirectory;
                while (dir != null)
                {
                    if (Directory.EnumerateFiles(dir, "*.sln").Any())
                        return dir;

                    var parent = Directory.GetParent(dir);
                    dir = parent?.FullName;
                }

                // fallback: just use current directory
                Console.WriteLine("Warning: Solution root not found. Using current directory.");
                return Environment.CurrentDirectory;
            }

        }


    }
}
