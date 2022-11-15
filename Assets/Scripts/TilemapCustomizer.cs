using System;
using System.Collections.Generic;
using BDUtil;
using BDUtil.Clone;
using BDUtil.Fluent;
using BDUtil.Math;
using BDUtil.Screen;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ld51
{
    // As SourceSpawn, but using chunks.
    [RequireComponent(typeof(Grid))]
    public class TilemapCustomizer : MonoBehaviour
    {
        /// A tilemap vertical slice to be populated as we go.
        public Tilemap Proto = default;
        /// Height to leave between tiles
        public int VClear = 2;
        public int Seed = 0;
        public Randoms.IRandom Random;

        public interface ICustomizer
        {
            bool Customize(TilemapCustomizer thiz, Tilemap tilemap);
        }
        public interface IKarma
        {
            bool Customize(TilemapCustomizer thiz, Tilemap tilemap);
        }
        [Serializable]
        public struct CustomizerPhase
        {
            [SerializeReference, Subtype]
            public ICustomizer[] Customizers;
            [SerializeReference, Subtype]
            public IKarma[] Karmas;
            public int Width;
        }
        public CustomizerPhase[] Customizers;

        public int Phase = 0;
        [Header("Tracking fields")]
        /// The total x-position consumed, thus next x position to set.
        /// Customizers use 0-based tilemap slices, so don't need this.
        public int Width = 0;
        /// The set of acceptable y positions for the ground to continue from.
        /// For ICustomizers to use.
        public readonly List<Platform> Platforms = new();
        public int GoodKarma;
        public int BadKarma;

        /// A platform is the running series of walkable space.
        [Serializable]
        public struct Platform : IComparable<Platform>, IEquatable<Platform>
        {
            public int Height;
            public TileBase Tile;
            public bool IsValid => Tile != null;
            public int GetClearance(Platform other) => Height - other.Height;

            public bool ExtrudeTo(Tilemap into, int startX, int endX)
            {
                for (int x = startX; x <= endX; ++x) into.SetTile(new(x, Height, 0), Tile);
                return endX >= startX;
            }
            public void DrawLine(Tilemap tilemap, int rise, int startRun, int endRun)
            {
                Vector2Int start = new(startRun, Height);
                Vector2Int end = new(endRun, Height + rise);
                foreach (Vector2Int point in start.Rasterized(end))
                {
                    tilemap.SetTile(new(point.x, point.y, 0), Tile);
                }
                Height = end.y;
            }
            public int ClampDeltaHeight(int delta, Platform below, Platform above, int clearance)
            {
                Height += delta;
                if (above.IsValid && above.Height - Height < clearance) Height = above.Height - clearance;
                if (below.IsValid && Height - below.Height < clearance) Height = below.Height + clearance;
                return Height;
            }

            public int CompareTo(Platform other) => Height.CompareTo(other.Height);
            public bool Equals(Platform other) => Height == other.Height && Tile == other.Tile;
            public override bool Equals(object obj) => obj is Platform other && Equals(other);
            public override int GetHashCode() => Chain.Hash ^ Height ^ Tile.GetHashCode();
            public override string ToString() => $"{Tile}@{Height}";


            /// scratch space for storing the running X locations of all running platforms within a slice.
            /// Only valid within a single customizer.
            public static readonly List<int> ContinueX = new();
            public static int MaxContinue
            {
                get
                {
                    int max = 0;
                    foreach (int value in ContinueX) max = Mathf.Max(max, value);
                    return max;
                }
            }
            public static void Prepare(TilemapCustomizer thiz)
            {
                ContinueX.Clear();
                for (int i = 0; i < thiz.Platforms.Count; ++i) ContinueX.Add(0);
            }
            public static bool ExtrudeTo(TilemapCustomizer thiz, Tilemap tilemap, int endX)
            {
                bool any = false;
                for (int i = 0; i < thiz.Platforms.Count; ++i)
                {
                    int startX = ContinueX[i];
                    Platform platform = thiz.Platforms[i];
                    any |= platform.ExtrudeTo(tilemap, startX, endX);
                }
                ContinueX.Clear();
                return any;
            }
        }
        protected void Awake() => Random = new Randoms.System(Seed);

        protected void Start()
        {
            for (Phase = 0; Phase < Customizers?.Length; ++Phase)
            {
                int target = Customizers[Phase].Width;
                int startWidth = Width;
                ExtendTo(startWidth + target);
            }
            SceneBounds.ClearBounds();
            SceneBounds.RecalculateBounds(left: 2f, right: 2f, top: 8f);
        }
        public void ExtendTo(int width)
        {
            while (Width < width && Extend()) { }
        }
        public bool Extend()
        {
            if (Customizers == null || Customizers.Length <= 0) throw new NotSupportedException();
            CustomizerPhase phase = Customizers[Phase];
            if (phase.Customizers == null || phase.Customizers.Length <= 0) throw new NotSupportedException();
            Tilemap tilemap = Pool.main.Acquire(Proto);
            tilemap.ClearAllTiles();
            tilemap.CompressBounds();
            if (!ApplyOneCustomizer(tilemap, Random.Index(phase.Customizers)))
            {
                Pool.main.Release(tilemap.gameObject);
                return false;
            }
            tilemap.transform.position = transform.position + tilemap.transform.position + Width * Vector3.right;
            tilemap.transform.SetParent(transform);
            BadKarma += Platforms.Count * tilemap.cellBounds.size.x;
            Width += tilemap.cellBounds.xMax;
            if (phase.Karmas != null) ApplyOneKarma(tilemap, Random.Index(phase.Karmas));
            return true;
        }
        bool ApplyOneCustomizer(Tilemap tilemap, int offset)
        {
            CustomizerPhase phase = Customizers[Phase];
            for (int i = 0; i < phase.Customizers?.Length; ++i)
            {
                ICustomizer customizer = phase.Customizers[(offset + i) % phase.Customizers.Length];
                if (!customizer.Customize(this, tilemap)) continue;
                tilemap.CompressBounds();
                tilemap.RefreshAllTiles();
                tilemap.name = $"{customizer.GetType().Name.Replace("TilemapCustomizer+", "")} ->{Width}";
                return true;
            }
            // We're allowed to fail -- permanently! -- in the final phase.
            if (Phase >= Customizers.Length - 1) return false;
            throw new NotSupportedException($"At phase {Phase}/width {Width}/plat {Platforms.Count} couldn't fit any of {phase.Customizers?.Length} customizers");
        }
        void ApplyOneKarma(Tilemap tilemap, int offset)
        {
            CustomizerPhase phase = Customizers[Phase];
            if (phase.Karmas == null || phase.Karmas.Length <= 0) return;
            for (int i = 0; i < phase.Karmas.Length; ++i)
            {
                IKarma karma = phase.Karmas[(offset + i) % phase.Karmas.Length];
                if (!karma.Customize(this, tilemap)) continue;
                return;
            }
            if (BadKarma <= 0 || GoodKarma <= 0) return;
            else Debug.Log($"Had karma, but none of {phase.Karmas.Summarize()} matched");
        }
        [Serializable]
        public struct None : IKarma
        {
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap) => true;
        }
        [Serializable]
        public struct SpawnSpikes : IKarma
        {
            public Collider2D[] TopSpikes;
            public Collider2D[] BottomSpikes;
            public IntervalInt Width;
            public float TopOdds;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                bool tite = default;
                Collider2D proto = null;
                switch ((TopSpikes?.Length > 0, BottomSpikes?.Length > 0))
                {
                    case (false, false):
                        return false;
                    case (true, true):
                        if (thiz.Random.Odds(TopOdds)) goto TopLabel; else goto BottomLabel;
                    case (true, false):
                    TopLabel:
                        tite = true; proto = thiz.Random.Range(TopSpikes); break;
                    case (false, true):
                    BottomLabel:
                        tite = false; proto = thiz.Random.Range(BottomSpikes); break;
                }

                int dX = thiz.Random.Odds(.5f) ? +1 : -1;
                bool? ceiling = tite ? true : null, floor = tite ? null : true;
                int expended = 0;
                int max = thiz.Random.Range(Width);
                for (
                    Vector3Int target = thiz.RandomCell(tilemap, ceiling, false, floor);
                    tilemap.cellBounds.Contains(target) && thiz.Matches(tilemap, target, ceiling, false, floor) && expended < max;
                    target.x += dX, expended++
                )
                {
                    Collider2D instantiated = Pool.main.Acquire(proto);
                    instantiated.transform.position = tilemap.CellToWorld(target);
                }
                thiz.GoodKarma += expended / 2;
                thiz.BadKarma -= expended;
                Debug.Log($"Finished SpawnSpikes spending {expended}");
                return expended > 0;
            }
        }
        public bool Matches(Tilemap tilemap, Vector3Int cell, bool? wantCeiling, bool? wantSelf, bool? wantFloor)
        {
            tern hasCeiling = tilemap.GetTile(cell + Vector3Int.up) != null;
            tern hasSelf = tilemap.GetTile(cell + Vector3Int.zero) != null;
            tern hasFloor = tilemap.GetTile(cell + Vector3Int.down) != null;
            if (hasCeiling ^ wantCeiling) return false;
            if (hasSelf ^ wantSelf) return false;
            if (hasFloor ^ wantFloor) return false;
            return true;
        }
        public Vector3Int RandomCell(Tilemap tilemap, bool? wantCeiling = null, bool? wantSelf = false, bool? wantFloor = true)
        {
            Vector3Int cell = Random.Range(tilemap.cellBounds);
            int celly = cell.y - tilemap.cellBounds.yMin;

            for (int y = 0; y <= tilemap.cellBounds.size.y; ++y)
            {
                cell.y = tilemap.cellBounds.yMin + ((y + celly) % tilemap.cellBounds.size.y);
                if (Matches(tilemap, cell, wantCeiling, wantSelf, wantFloor)) return cell;
            }
            return new(int.MinValue, int.MinValue, int.MinValue);
        }
        [Serializable]
        public struct SpawnEnemy : IKarma
        {
            public int MaxKarma;
            public Collider2D[] Protos;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (Protos == null) return false;
                int expended = 0;
                while (true)
                {
                    Vector3Int cell = thiz.RandomCell(tilemap);
                    if (cell.x <= int.MinValue) return expended > 0;
                    int offset = thiz.Random.Index(Protos);
                    expended += offset / 2 + 1;
                    if (expended > MaxKarma || expended > thiz.BadKarma) break;
                    Collider2D instantiated = Pool.main.Acquire(Protos[offset]);
                    instantiated.transform.position = tilemap.CellToWorld(cell);
                }
                thiz.GoodKarma += expended / 2;
                thiz.BadKarma -= expended;
                return expended > 0;
            }
        }
        [Serializable]
        public struct Initialize : ICustomizer
        {
            [Tooltip("X/Y to cover with new platforms")]
            public Vector2Int InitArea;
            [Tooltip("Number of new platforms")]
            public IntervalInt Count;
            public Tile[] Tiles;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count > 0) return false;
                Platform.Prepare(thiz);
                // We won't necessarily spawn Count platforms, in case they all collide.
                for (int i = 0, count = thiz.Random.Range(Count); i < count; ++i)
                {
                    Platform @new = new()
                    {
                        Height = thiz.Random.Range(0, InitArea.y),
                        Tile = thiz.Random.Range(Tiles)
                    };
                    int aboveI = thiz.Platforms.BinarySearch(@new);
                    if (aboveI >= 0) continue;
                    aboveI = ~aboveI;
                    int belowI = aboveI - 1;
                    if (
                        belowI.IsInRange(0, thiz.Platforms.Count)
                        && @new.GetClearance(thiz.Platforms[belowI]) < thiz.VClear
                    ) continue;
                    if (
                        aboveI.IsInRange(0, thiz.Platforms.Count)
                        && thiz.Platforms[aboveI].GetClearance(@new) < thiz.VClear
                    ) continue;
                    thiz.Platforms.Insert(aboveI, @new);
                    Platform.ContinueX.Insert(aboveI, thiz.Random.Range(0, InitArea.x));
                }
                return Platform.ExtrudeTo(thiz, tilemap, InitArea.x);
            }
        }
        [Serializable]
        public struct Extrude : ICustomizer
        {
            public IntervalInt Width;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0) return false;
                Platform.Prepare(thiz);
                return Platform.ExtrudeTo(thiz, tilemap, thiz.Random.Range(Width));
            }
        }

        [Serializable]
        public struct Climb : ICustomizer
        {
            [Range(0f, 1f)] public float FallOdds;
            public IntervalInt Rise;
            public IntervalInt Run;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0) return false;
                int rise = (thiz.Random.Odds(FallOdds) ? -1 : +1) * thiz.Random.Range(Rise), run = thiz.Random.Range(Run);
                for (int i = 0; i < thiz.Platforms.Count; ++i)
                {
                    Platform platform = thiz.Platforms[i];
                    platform.DrawLine(tilemap, rise, 0, run);
                    thiz.Platforms[i] = platform;
                }
                return true;
            }
        }

        [Serializable]
        public struct Splay : ICustomizer
        {
            public Interval SetPoint;
            public IntervalInt DeltaHeight;
            public IntervalInt Width;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count < 2) return false;
                Platform.Prepare(thiz);

                Vector2Int low = thiz.Platforms[0].Height * Vector2Int.one, high = thiz.Platforms[^1].Height * Vector2Int.one;
                // Amount of spread to apportion between newLow/newHigh.
                int deltaHeight = Math.DivRem(thiz.Random.Range(DeltaHeight), 2, out int deltaHeightRem);

                int halfI = Math.DivRem(thiz.Platforms.Count, 2, out int modI);
                float medianValue = (thiz.Platforms[halfI].Height + thiz.Platforms[halfI + modI].Height) / 2f;
                switch (medianValue.GetValence(SetPoint.min, SetPoint.max))
                {
                    case true:
                        low.y -= 2 * deltaHeight + deltaHeightRem;
                        break;
                    case null:
                        low.y -= deltaHeight;
                        high.y += deltaHeight + deltaHeightRem;
                        break;
                    case false:
                        high.y += 2 * deltaHeight + deltaHeightRem;
                        break;
                }

                Bresenham.RiseRun splay = new(high.y - low.y, high.x - low.x);
                splay.MoveNext();
                int width = thiz.Random.Range(Width);
                for (int i = 0; i < thiz.Platforms.Count; ++i)
                {
                    Platform platform = thiz.Platforms[i];
                    splay.SkipTo(platform.Height - low.x);
                    // Splay is 0->delta, so rise+lowy is lowy->highy, so the *rise* is the delta beteween that and current.
                    platform.DrawLine(tilemap, splay.Rise + low.y - platform.Height, 0, width);
                    thiz.Platforms[i] = platform;
                }
                return true;
            }
        }

        [Serializable]
        public struct Recolor : ICustomizer
        {
            [Range(0f, 1f)] public float Odds;
            public Tile[] Tiles;
            public IntervalInt Width;
            public IntervalInt Height;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0) return false;
                Platform.Prepare(thiz);
                for (int i = 0; i < thiz.Platforms.Count; ++i)
                {
                    if (!thiz.Random.Odds(Odds)) continue;
                    Platform platform = thiz.Platforms[i];
                    int x = thiz.Random.Range(Width);
                    platform.ExtrudeTo(tilemap, 0, x);
                    Platform.ContinueX[i] = x;

                    platform.Tile = thiz.Random.Range(Tiles);
                    platform.ClampDeltaHeight(thiz.Random.Range(Height), thiz.Platforms.GetValueOrDefault(i - 1), thiz.Platforms.GetValueOrDefault(i + 1), thiz.VClear);
                    thiz.Platforms[i] = platform;
                }
                int max = Platform.MaxContinue;
                if (max <= 0) return false;
                Platform.ExtrudeTo(thiz, tilemap, max);
                return true;
            }
        }
        [Serializable]
        public struct ExtrudeGap : ICustomizer
        {
            public IntervalInt Before;
            public IntervalInt Gap;
            public IntervalInt After;
            public IntervalInt Drop;
            // Not *actually* odds; for instance, .5 means "one half overall" (and: rounded up!).
            [Range(0f, 1f)] public float Odds;
            public Tile[] Tiles;
            [Range(0f, 1f)] public float TileOdds;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0 || Odds <= 0f) return false;
                Platform.Prepare(thiz);
                foreach (int i in thiz.Random.Deal(thiz.Platforms.Count, Odds))
                {
                    Platform platform = thiz.Platforms[i];
                    int before = thiz.Random.Range(Before);
                    platform.ExtrudeTo(tilemap, 0, before);
                    Platform.ContinueX[i] = before + thiz.Random.Range(Gap);
                    platform.ClampDeltaHeight(-thiz.Random.Range(Drop), thiz.Platforms.GetValueOrDefault(i - 1), thiz.Platforms.GetValueOrDefault(i + 1), thiz.VClear);
                    if (thiz.Random.Odds(Odds)) platform.Tile = thiz.Random.Range(Tiles);
                    thiz.Platforms[i] = platform;
                }
                int max = Platform.MaxContinue;
                if (max <= 0) return false;
                max += thiz.Random.Range(After);
                Platform.ExtrudeTo(thiz, tilemap, max);
                return true;
            }
        }
        [Serializable]
        public struct Terminate : ICustomizer
        {
            [Range(0f, 1f)] public float Odds;
            public IntervalInt Width;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 1) return false;
                int width = thiz.Random.Range(Width);
                Platform.Prepare(thiz);
                void UnwrapBackwards(IEnumerator<int> indices)
                {
                    if (!indices.MoveNext()) return;
                    int target = indices.Current;
                    UnwrapBackwards(indices);
                    int remainWidth = thiz.Random.Range(0, width);
                    thiz.Platforms[target].ExtrudeTo(tilemap, 0, remainWidth);
                    thiz.Platforms.RemoveAt(target);
                    Platform.ContinueX.RemoveAt(target);
                };
                UnwrapBackwards(thiz.Random.Deal(thiz.Platforms.Count, Odds).GetEnumerator());
                Platform.ExtrudeTo(thiz, tilemap, width);
                return true;
            }
        }
        [Serializable]
        public struct Fork : ICustomizer
        {
            public IntervalInt RequirePlatforms;
            public IntervalInt Width;
            public IntervalInt HGap;  // Of up to width.
            public IntervalInt Up;  // Height the upper path can bump up
            public IntervalInt Down;  // Height below Up to drop (you must choose min > clearance!).
            public Tile[] Tiles;
            [Range(0f, 1f)] public float TileOdds;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0) return false;
                if (thiz.Platforms.Count > RequirePlatforms.max) return false;
                int splitplatforms = thiz.Random.Range(RequirePlatforms) / 2;

                int width = thiz.Random.Range(Width);
                Platform.Prepare(thiz);
                foreach (int platformI in thiz.Random.Deal(thiz.Platforms.Count, splitplatforms))
                {
                    Platform platform = thiz.Platforms[platformI];
                    int up = platform.Height + thiz.Random.Range(Up);
                    int down = up - thiz.Random.Range(Down);
                    Platform above = thiz.Platforms.GetValueOrDefault(platformI + 1);
                    Platform below = thiz.Platforms.GetValueOrDefault(platformI - 1);
                    if (above.IsValid)
                    {
                        int limit = above.Height - thiz.VClear;
                        if (up > limit)
                        {
                            down -= up - limit;
                            up = limit;
                        }
                    }
                    if (below.IsValid)
                    {
                        down = Mathf.Max(down, below.Height + thiz.VClear);
                    }
                    // We can't split without violating width reqs.
                    if (up - down < thiz.VClear) continue;

                    if (Platform.ContinueX[platformI] <= 0)
                    {
                        // This is our first split, so we'll leave a stub behind and then continue post-gap
                        int hgap = Mathf.Min(thiz.Random.Range(HGap), width - 2);
                        int before = thiz.Random.Range(1, width - hgap);

                        for (int x = 0; x < before; ++x)
                        {
                            tilemap.SetTile(new(x, platform.Height, 0), platform.Tile);
                        }
                        Platform.ContinueX[platformI] = before + hgap;
                    }
                    {  // Then do the actual split.
                        above = thiz.Platforms[platformI];
                        below = above;
                        above.Height = up;
                        if (!Tiles.IsEmpty() && thiz.Random.Odds(TileOdds)) above.Tile = thiz.Random.Range(Tiles);
                        thiz.Platforms[platformI] = above;
                        below.Height = down;
                        if (Tiles.IsEmpty() && thiz.Random.Odds(TileOdds)) below.Tile = thiz.Random.Range(Tiles);
                        thiz.Platforms.Insert(platformI, below);
                        Platform.ContinueX.Insert(platformI, Platform.ContinueX[platformI]);
                    }
                }
                Platform.ExtrudeTo(thiz, tilemap, width);
                return true;
            }
        }
    }
}