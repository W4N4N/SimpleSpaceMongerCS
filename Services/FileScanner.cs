using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimpleSpaceMongerCS
{
    public static class FileScanner
    {
        // Moved from MainForm.ScanPath; identical behavior
        public static Dictionary<string, long> ScanPath(string root, IProgress<int>? progress)
        {
            var allDirs = new List<string>();
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                allDirs.Add(dir);
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                        stack.Push(sub);
                }
                catch { }
            }

            int totalDirs = allDirs.Count;
            var fileSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < totalDirs; i++)
            {
                var dir = allDirs[i];
                long sum = 0;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir))
                    {
                        try { sum += new FileInfo(file).Length; } catch { }
                    }
                }
                catch { }
                fileSizes[dir] = sum;
                int percent = totalDirs > 0 ? (int)((i + 1) * 100.0 / totalDirs) : 100;
                progress?.Report(percent);
            }

            // Aggregate sizes bottom-up: process directories ordered by descending path length
            var aggregated = new Dictionary<string, long>(fileSizes, StringComparer.OrdinalIgnoreCase);
            var ordered = allDirs.OrderByDescending(d => d.Length).ToList();
            foreach (var dir in ordered)
            {
                try
                {
                    var parent = Path.GetDirectoryName(dir);
                    if (string.IsNullOrEmpty(parent)) continue;
                    if (!aggregated.ContainsKey(parent)) aggregated[parent] = 0;
                    aggregated[parent] += aggregated[dir];
                }
                catch { }
            }

            // Ensure root exists
            if (!aggregated.ContainsKey(root)) aggregated[root] = fileSizes.ContainsKey(root) ? fileSizes[root] : 0;

            return aggregated;
        }
    }
}
