using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Full_Bot.Config
{
    public enum CoordSource
    {
        World,
        Dungeon
    }

    public sealed class WorldCoord
    {
        public int Id { get; }
        public CoordSource Source { get; }
        public string SourceTag { get; }
        public string Map { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public WorldCoord(int id, CoordSource source, string map, float x, float y, float z)
        {
            Id = id;
            Source = source;
            SourceTag = source == CoordSource.World ? "world" : "dg";
            Map = map;
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "ID={0} | Tipo={1} | Mapa={2} | X={3:F2} Y={4:F2} Z={5:F2}",
                Id, SourceTag, Map, X, Y, Z
            );
        }
    }

    public sealed class CoordsService
    {
        public const string WorldFileName = "coords_world.txt";
        public const string DungeonsFileName = "coords_dungeons.txt";

        private readonly Dictionary<int, List<WorldCoord>> _indexById =
            new Dictionary<int, List<WorldCoord>>();

        public string ConfigFolderPath { get; }
        public int TotalRecords { get; private set; }
        public int DistinctIds { get; private set; }

        public CoordsService(string baseDirectory = null)
        {
            string root = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : baseDirectory;

            ConfigFolderPath = Path.Combine(root, "Config");
            ReloadAll();
        }

        public void ReloadAll()
        {
            _indexById.Clear();
            TotalRecords = 0;

            ParseFileIfExists(Path.Combine(ConfigFolderPath, WorldFileName), CoordSource.World);
            ParseFileIfExists(Path.Combine(ConfigFolderPath, DungeonsFileName), CoordSource.Dungeon);

            DistinctIds = _indexById.Count;
        }

        public WorldCoord[] FindById(int id)
        {
            if (_indexById.TryGetValue(id, out List<WorldCoord> matches) && matches.Count > 0)
            {
                return matches.ToArray();
            }

            return Array.Empty<WorldCoord>();
        }

        private void ParseFileIfExists(string filePath, CoordSource source)
        {
            if (!File.Exists(filePath))
                return;

            char[] delimiters = new char[] { ' ', '\t' };

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0)
                        continue;

                    string[] cols = line.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                    if (cols.Length < 5)
                        continue;

                    if (!int.TryParse(cols[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                        continue;

                    string map = cols[1];

                    if (!float.TryParse(cols[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                        !float.TryParse(cols[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                        !float.TryParse(cols[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        continue;
                    }

                    WorldCoord entry = new WorldCoord(id, source, map, x, y, z);

                    if (!_indexById.TryGetValue(id, out List<WorldCoord> list))
                    {
                        list = new List<WorldCoord>();
                        _indexById[id] = list;
                    }

                    list.Add(entry);
                    TotalRecords++;
                }
            }
        }
    }
}