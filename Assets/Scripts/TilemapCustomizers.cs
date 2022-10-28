// [Serializable]
// public struct RandTile
// {
//     public float Odds0;
//     public float Odds => Odds0 + 1f;
//     public TileBase Tile;
//     public static RandTile GetRandom(ref float totalOdds, params RandTile[] randTiles)
//     {
//         if (totalOdds <= 0f)
//         {
//             totalOdds = 0f;
//             foreach (
//                 RandTile tile in randTiles ?? Array.Empty<RandTile>()
//             ) totalOdds += tile.Odds;
//         }
//         float origRandom = UnityRandoms.main.Range(0f, totalOdds);
//         float random = origRandom;
//         foreach (RandTile randTile in randTiles ?? Array.Empty<RandTile>())
//         {
//             if ((random -= randTile.Odds) > 0f) return randTile;
//             break;
//         }
//         return default;
//     }
// }
// [Serializable]
// internal struct TrueRandom : ICustomizer
// {
//     public RandTile[] RandTiles;

//     public void Customize(TilemapCustomizer tilemap)
//     {
//         float totalOdds = 0f;
//         foreach (
//             Vector2Int point in Vectors.MinMaxRect(Vector2Int.zero, tilemap.ChunkSize).allPositionsWithin
//         )
//         {
//             RandTile tile = RandTile.GetRandom(ref totalOdds, RandTiles);
//             if (tile.Tile != null) tilemap.tilemap.SetTile(new(point.x, point.y, 0), tile.Tile);
//         }
//     }
// }
// internal struct Tuple : IComparable<Tuple>, IEquatable<Tuple>
// {
//     public int height; public int widthRemaining; public TileBase tile;
//     public static implicit operator Tuple((int height, int widthRemaining, TileBase tile) tuple) => new()
//     {
//         height = tuple.height,
//         widthRemaining = tuple.widthRemaining,
//         tile = tuple.tile,
//     };
//     public void Deconstruct(out int height, out int widthRemaining, out TileBase tile)
//     {
//         height = this.height;
//         widthRemaining = this.widthRemaining;
//         tile = this.tile;
//     }
//     public int CompareTo(Tuple other)
//     => height.CompareTo(other.height);
//     public bool Equals(Tuple other)
//     => height == other.height && widthRemaining == other.widthRemaining && tile == other.tile;
//     public override bool Equals(object obj) => obj is Tuple other && Equals(other);
//     public override int GetHashCode() => Chain.Hash ^ height ^ widthRemaining ^ tile.GetHashCode();
//     public override string ToString() => $"{widthRemaining}x{tile}@{height}";
// }
// [Serializable]
// internal struct Platforms : ICustomizer
// {
//     static readonly List<Tuple> leftSet = new();
//     public RandTile[] RandTiles;
//     [Tooltip("platform sizes")]
//     public ExtentInt WidthRange;
//     public ExtentInt PreferredPlatforms;
//     [Range(0, 1)] public float SwerveOdds;
//     [Range(0, 1)] public float ChangeOdds;
//     public int ReserveBetween;
//     public void Customize(TilemapCustomizer tilemap)
//     {
//         float totalOdds = 0f;
//         leftSet.Clear();
//         if (tilemap.Left != null)
//         {
//             BoundsInt old = tilemap.Left.tilemap.cellBounds;
//             for (int y = old.yMin; y <= old.yMax; ++y)
//             {
//                 TileBase oldTile = tilemap.Left.tilemap.GetTile(new(old.xMax, y, 0));
//                 int yf = y;
//                 int xf = old.xMax;
//                 while (xf >= old.xMin)
//                 {
//                     int offset = -2;
//                     while (++offset <= +1)
//                     {
//                         TileBase hadTile = tilemap.Left.tilemap.GetTile(new(xf, yf + offset, 0));
//                         if (oldTile == hadTile)
//                         {
//                             yf += offset;
//                             break;
//                         }
//                     }
//                     if (offset > +1) break;
//                     xf--;
//                 }
//                 if (oldTile != null) leftSet.Add((y, UnityRandoms.main.Range(0, WidthRange.max - (old.xMax - xf)), oldTile));
//             }
//         }

//         for (
//             int x = 0; x < tilemap.ChunkSize.x; ++x
//         )
//         {
//             int preferredPlatforms = UnityRandoms.main.RandomValue(PreferredPlatforms);
//             while (leftSet.Count < preferredPlatforms)
//             {
//                 int y = UnityRandoms.main.Range(0, tilemap.ChunkSize.y);
//                 RandTile tile = RandTile.GetRandom(ref totalOdds, RandTiles);
//                 Tuple newTuple = (y, UnityRandoms.main.RandomValue(WidthRange), tile.Tile);
//                 leftSet.BinaryInsert(newTuple);
//             }
//             for (int j = leftSet.Count - 1; j >= 0; --j)
//             {
//                 (int height, int widthRemaining, TileBase tile) = leftSet[j];
//                 if (--widthRemaining < 0)
//                 {
//                     leftSet.RemoveAt(j);
//                     continue;
//                 }
//                 if (UnityRandoms.main.RandomTrue(SwerveOdds))
//                 {
//                     height += UnityRandoms.main.RandomTrue() ? +1 : -1;
//                 }
//                 if (UnityRandoms.main.RandomTrue(ChangeOdds))
//                 {
//                     tile = RandTile.GetRandom(ref totalOdds, RandTiles).Tile;
//                 }
//                 if (tile == null)
//                 {
//                     leftSet.RemoveAt(j);
//                     continue;
//                 }
//                 // Check collisions, leave map, etc.
//                 if (!height.IsInRange(0, tilemap.ChunkSize.y))
//                 {
//                     leftSet.RemoveAt(j);
//                     continue;
//                 }
//                 if (j < leftSet.Count - 1 && leftSet[j + 1].tile != null && leftSet[j + 1].height - height <= ReserveBetween)
//                 {
//                     // Prefer the lower survive always.
//                     leftSet.RemoveAt(j + 1);
//                     continue;
//                 }
//                 if (j > 0 && leftSet[j - 1].tile != null && height - leftSet[j - 1].height <= ReserveBetween)
//                 {
//                     leftSet.RemoveAt(j);
//                     continue;
//                 }
//                 tilemap.tilemap.SetTile(new(x, height, 0), tile);
//                 leftSet[j] = (height, widthRemaining, tile);
//             }
//         }
//     }
// }