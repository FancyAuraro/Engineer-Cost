using System.Text.Json;
using 施工定额.Helper;

namespace 施工定额.UI
{
    /// <summary>
    /// 记忆 SplitContainer 分隔条位置到 %AppData%\施工定额\splitter_layout.json。
    /// </summary>
    public static class SplitterLayoutStore
    {
        private static readonly object _lock = new();
        private static Dictionary<string, int>? _cache;
        private static readonly HashSet<SplitContainer> _attached = new();
        private static System.Windows.Forms.Timer? _saveTimer;
        private static bool _suppress;

        private static string FilePath => Path.Combine(AppConfig.DataDirectory, "splitter_layout.json");

        public static void Attach(SplitContainer split)
        {
            if (split == null) return;
            if (string.IsNullOrEmpty(split.Name))
                split.Name = "split_" + split.GetHashCode().ToString("X");

            lock (_lock)
            {
                if (_attached.Contains(split)) return;
                _attached.Add(split);
            }

            split.SplitterMoved += OnSplitterMoved;
            split.Disposed += (_, _) =>
            {
                lock (_lock) _attached.Remove(split);
            };
        }

        /// <summary>在控件已有有效尺寸后调用，恢复记忆的分隔位置。</summary>
        /// <returns>是否成功应用了记忆值。</returns>
        public static bool Restore(SplitContainer split, int panel1Min, int panel2Min)
        {
            if (split == null || string.IsNullOrEmpty(split.Name)) return false;
            var map = Load();
            if (!map.TryGetValue(split.Name, out int preferred))
                return false;

            ApplyDistance(split, preferred, panel1Min, panel2Min);
            return true;
        }

        public static void ApplyDistance(SplitContainer split, int preferred, int panel1Min, int panel2Min)
        {
            if (split == null) return;
            int total = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            int splitter = Math.Max(1, split.SplitterWidth);
            int minDist = Math.Max(0, panel1Min);
            int maxDist = Math.Max(minDist, total - panel2Min - splitter);
            if (maxDist < minDist) return;

            _suppress = true;
            try
            {
                split.Panel1MinSize = 0;
                split.Panel2MinSize = 0;
                split.SplitterDistance = Math.Clamp(preferred, minDist, maxDist);
            }
            catch
            {
                // 尺寸未就绪时忽略
            }
            finally
            {
                _suppress = false;
            }
        }

        private static void OnSplitterMoved(object? sender, SplitterEventArgs e)
        {
            if (_suppress) return;
            ScheduleSave();
        }

        private static void ScheduleSave()
        {
            if (_saveTimer == null)
            {
                _saveTimer = new System.Windows.Forms.Timer { Interval = 400 };
                _saveTimer.Tick += (_, _) =>
                {
                    _saveTimer.Stop();
                    SaveAll();
                };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public static void SaveAll()
        {
            Dictionary<string, int> snapshot;
            lock (_lock)
            {
                snapshot = Load();
                foreach (var split in _attached.ToList())
                {
                    if (split.IsDisposed) continue;
                    if (string.IsNullOrEmpty(split.Name)) continue;
                    try
                    {
                        if (split.SplitterDistance > 0)
                            snapshot[split.Name] = split.SplitterDistance;
                    }
                    catch { }
                }
                _cache = snapshot;
            }

            try
            {
                Directory.CreateDirectory(AppConfig.DataDirectory);
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        private static Dictionary<string, int> Load()
        {
            lock (_lock)
            {
                if (_cache != null)
                    return _cache;

                _cache = new Dictionary<string, int>(StringComparer.Ordinal);
                try
                {
                    if (!File.Exists(FilePath))
                        return _cache;
                    var json = File.ReadAllText(FilePath);
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    if (parsed != null)
                        _cache = new Dictionary<string, int>(parsed, StringComparer.Ordinal);
                }
                catch { }
                return _cache;
            }
        }
    }
}
