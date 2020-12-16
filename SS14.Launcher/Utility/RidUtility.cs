using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SS14.Launcher.Utility
{
    public static class RidUtility
    {
        public static string GetRid()
        {
            var rid = RuntimeInformation.RuntimeIdentifier;
            if (rid != "unknown")
                return rid;

            // Not sure what could cause it to return unknown but if it does we'll make a decent educated guess.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X86 => "linux-x86",
                    Architecture.X64 => "linux-x64",
                    Architecture.Arm => "linux-arm",
                    Architecture.Arm64 => "linux-arm64",
                    _ => "unknown"
                };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X86 => "win-x86",
                    Architecture.X64 => "win-x64",
                    Architecture.Arm => "win-arm",
                    Architecture.Arm64 => "win-arm64",
                    _ => "unknown"
                };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "osx-x64",
                    Architecture.Arm64 => "osx-arm64",
                    _ => "unknown"
                };
            }

            return "unknown";
        }

        public static string? FindBestRid(ICollection<string> runtimes, string currentRid)
        {
            var catalog = LoadRidCatalog();

            // Breadth-first search.
            var q = new Queue<string>();
            if (!catalog.Runtimes.TryGetValue(currentRid, out var root))
                // RID doesn't exist in catalog???
                return null;

            root.Discovered = true;
            q.Enqueue(currentRid);

            while (q.TryDequeue(out var v))
            {
                if (runtimes.Contains(v))
                {
                    return v;
                }

                foreach (var w in catalog.Runtimes[v].Imports)
                {
                    var r = catalog.Runtimes[w];
                    if (!r.Discovered)
                    {
                        q.Enqueue(w);
                        r.Discovered = true;
                    }
                }
            }

            return null;
        }

        private static RidCatalog LoadRidCatalog()
        {
            using var stream = typeof(RidCatalog).Assembly.GetManifestResourceStream("Utility.runtime.json")!;
            var ms = new MemoryStream();
            stream.CopyTo(ms);

            return JsonSerializer.Deserialize<RidCatalog>(ms.GetBuffer().AsSpan(0, (int) ms.Length))!;
        }

#pragma warning disable 649
        private sealed class RidCatalog
        {
            [JsonInclude] [JsonPropertyName("runtimes")]
            public Dictionary<string, Runtime> Runtimes = default!;

            public class Runtime
            {
                public bool Discovered;

                [JsonInclude] [JsonPropertyName("#import")]
                public string[] Imports = default!;
            }
        }
#pragma warning restore 649
    }
}
