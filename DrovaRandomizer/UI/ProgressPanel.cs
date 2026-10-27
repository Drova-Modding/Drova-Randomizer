using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using DrovaRandomizer.Systems.Run;
using Il2Cpp;
using Il2CppDrova.Saveables;
using UnityEngine;

namespace DrovaRandomizer.UI
{
    /// <summary>Checked/total per area and location category for the current run.</summary>
    public class ProgressPanel
    {
        private const float LabelWidth = 190f;
        private const float ColumnWidth = 86f;
        private const float RowHeight = 19f;

        private struct Cell
        {
            public int Checked;
            public int Total;
        }

        private readonly SortedDictionary<string, Dictionary<LocationCategory, Cell>> _byArea = new(StringComparer.Ordinal);
        private readonly List<LocationCategory> _columns = [];
        private RandoSeed? _builtFor;
        private int _builtAtChecked = -1;

        private string _currentArea = "";
        private float _nextAreaPollAt;

        /// <summary>The table's width for the run's categories.</summary>
        public float Width
        {
            get
            {
                EnsureBuilt();
                return LabelWidth + ColumnWidth * (_columns.Count + 1);
            }
        }

        /// <summary>Draws the table at x, y inside the F3 window. Returns where it ends.</summary>
        public float Draw(float x, float y)
        {
            EnsureBuilt();
            GUI.Label(new Rect(x, y, LabelWidth, RowHeight), "Area");
            for (int i = 0; i < _columns.Count; i++)
            {
                GUI.Label(new Rect(x + LabelWidth + ColumnWidth * i, y, ColumnWidth, RowHeight), _columns[i].ToString());
            }
            GUI.Label(new Rect(x + LabelWidth + ColumnWidth * _columns.Count, y, ColumnWidth, RowHeight), "Total");
            y += RowHeight;

            if (Time.realtimeSinceStartup >= _nextAreaPollAt)
            {
                _currentArea = CurrentArea();
                _nextAreaPollAt = Time.realtimeSinceStartup + 1f;
            }

            Dictionary<LocationCategory, Cell> grand = new();
            foreach (KeyValuePair<string, Dictionary<LocationCategory, Cell>> row in _byArea)
            {
                DrawRow(row.Key, row.Value, x, y, row.Key == _currentArea);
                foreach (KeyValuePair<LocationCategory, Cell> pair in row.Value)
                {
                    grand.TryGetValue(pair.Key, out Cell cell);
                    cell.Checked += pair.Value.Checked;
                    cell.Total += pair.Value.Total;
                    grand[pair.Key] = cell;
                }
                y += RowHeight;
            }
            DrawRow("Everything", grand, x, y, false);
            return y + RowHeight;
        }

        private static string CurrentArea()
        {
            try
            {
                SavegameData? data = Savegame.Current?.Data;
                string? key = data == null ? null : SavegameDataExtensions.GetPlayerAreaLocaKey(data);
                return string.IsNullOrEmpty(key) ? "" : LocationTable.AreaKeyToName.GetValueOrDefault(key, "");
            }
            catch
            {
                return "";
            }
        }

        private void DrawRow(string label, Dictionary<LocationCategory, Cell> cells, float x, float y, bool highlight)
        {
            Color previous = GUI.color;
            if (highlight)
            {
                GUI.color = Color.yellow;
            }
            GUI.Label(new Rect(x, y, LabelWidth, RowHeight), highlight ? "> " + label : label);
            GUI.color = previous;

            Cell total = new();
            for (int i = 0; i < _columns.Count; i++)
            {
                Rect rect = new(x + LabelWidth + ColumnWidth * i, y, ColumnWidth, RowHeight);
                if (cells.TryGetValue(_columns[i], out Cell cell))
                {
                    DrawCell(cell, rect);
                    total.Checked += cell.Checked;
                    total.Total += cell.Total;
                }
                else
                {
                    GUI.Label(rect, "-");
                }
            }
            DrawCell(total, new Rect(x + LabelWidth + ColumnWidth * _columns.Count, y, ColumnWidth, RowHeight));
        }

        private static void DrawCell(Cell cell, Rect rect)
        {
            Color previous = GUI.color;
            if (cell.Total > 0 && cell.Checked >= cell.Total)
            {
                GUI.color = Color.green;
            }
            GUI.Label(rect, cell.Checked + "/" + cell.Total);
            GUI.color = previous;
        }

        private void EnsureBuilt()
        {
            if (_builtFor == Randomizer.Seed && _builtAtChecked == Randomizer.CheckedCount)
            {
                return;
            }
            _builtFor = Randomizer.Seed;
            _builtAtChecked = Randomizer.CheckedCount;
            _byArea.Clear();
            _columns.Clear();
            if (_builtFor == null)
            {
                return;
            }

            foreach (string key in _builtFor.Placement.Keys)
            {
                LocationDef location = LocationTable.Get(key)!;
                string area = AreaOf(location);
                if (!_byArea.TryGetValue(area, out Dictionary<LocationCategory, Cell> cells))
                {
                    cells = new Dictionary<LocationCategory, Cell>();
                    _byArea[area] = cells;
                }
                cells.TryGetValue(location.Category, out Cell cell);
                cell.Total++;
                if (Randomizer.IsChecked(location))
                {
                    cell.Checked++;
                }
                cells[location.Category] = cell;
                if (!_columns.Contains(location.Category))
                {
                    _columns.Add(location.Category);
                }
            }
            _columns.Sort();
        }

        private static string AreaOf(LocationDef location)
        {
            switch (location.Category)
            {
                case LocationCategory.Quest:
                    return "Quests";
                case LocationCategory.Trader:
                    return "Traders";
                case LocationCategory.Mugging:
                    return "Muggings";
                case LocationCategory.Kills:
                case LocationCategory.Attributes:
                case LocationCategory.Talents:
                    return "Milestones";
            }
            return string.IsNullOrEmpty(location.Area) || location.Area.StartsWith("Wilds ", StringComparison.Ordinal)
                ? "The Wilds"
                : location.Area;
        }
    }
}
