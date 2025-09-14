using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SimpleSpaceMongerCS
{
    // Responsible for computing treemap layout rectangles from a list of items.
    public static class TreemapLayout
    {
        public class TileLayout
        {
            public RectangleF Rect;
            public string? Path;
            public long Size;
            public string Name = string.Empty;
            public int Depth;
        }

        // Build layout (populate list) using recursive binary partitioning
        // startDepth lets callers place children with a deeper depth offset.
        public static List<TileLayout> BuildLayout(Rectangle area, List<(string path, long size, string name)> items, int startDepth = 0)
        {
            var result = new List<TileLayout>();
            if (items == null || items.Count == 0) return result;

            items = items.OrderByDescending(i => i.size).ToList();

            void Recurse(Rectangle a, List<(string path, long size, string name)> its, int depth)
            {
                if (its == null || its.Count == 0) return;
                if (its.Count == 1)
                {
                    var it = its[0];
                    var rf = new RectangleF(a.X, a.Y, a.Width, a.Height);
                    result.Add(new TileLayout { Rect = rf, Path = it.path, Size = it.size, Name = it.name, Depth = depth });

                    // children one-level deep are handled by the caller by calling BuildLayout again with startDepth = depth+1
                    return;
                }

                long totalSize = its.Sum(i => i.size);
                long sum = 0; int splitIndex = 0;
                for (int i = 0; i < its.Count; i++)
                {
                    sum += its[i].size;
                    if (sum >= totalSize / 2)
                    {
                        splitIndex = i;
                        break;
                    }
                }

                var first = its.Take(splitIndex + 1).ToList();
                var second = its.Skip(splitIndex + 1).ToList();

                if (a.Width >= a.Height)
                {
                    float ratio = totalSize > 0 ? (float)first.Sum(x => x.size) / totalSize : 0.5f;
                    int w1 = Math.Max(1, (int)Math.Round(a.Width * ratio));
                    var r1 = new Rectangle(a.X, a.Y, w1, a.Height);
                    var r2 = new Rectangle(a.X + w1, a.Y, a.Width - w1, a.Height);
                    Recurse(r1, first, depth);
                    Recurse(r2, second, depth);
                }
                else
                {
                    float ratio = totalSize > 0 ? (float)first.Sum(x => x.size) / totalSize : 0.5f;
                    int h1 = Math.Max(1, (int)Math.Round(a.Height * ratio));
                    var r1 = new Rectangle(a.X, a.Y, a.Width, h1);
                    var r2 = new Rectangle(a.X, a.Y + h1, a.Width, a.Height - h1);
                    Recurse(r1, first, depth);
                    Recurse(r2, second, depth);
                }
            }

            Recurse(area, items, startDepth);
            return result;
        }
    }
}
