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
        public interface ICustomizer
        {
            bool Customize(TilemapCustomizer thiz, Tilemap tilemap);
        }
        /// The set of customizers to apply
        [SerializeReference, Subtype] public ICustomizer[] Customizers;

        [Header("Tracking fields")]
        /// The total x-position consumed, thus next x position to set.
        /// Customizers use 0-based tilemap slices, so don't need this.
        public int Width = 0;
        /// The set of acceptable y positions for the ground to continue from.
        /// For ICustomizers to use.
        public readonly List<Platform> Platforms = new();

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

        protected void Start()
        {
            int target = Width;
            Width = 0;
            ExtendTo(target);
            SceneBounds.ClearBounds();
        }
        public void ExtendTo(int width)
        {
            while (Width < width) Extend();
        }
        public void Extend()
        {
            if (Customizers == null || Customizers.Length <= 0)
            {
                throw new NotSupportedException();
            }
            Tilemap tilemap = Pool.main.Acquire(Proto);
            tilemap.ClearAllTiles();
            tilemap.CompressBounds();
            int offset = UnityRandoms.main.Range(0, Customizers.Length);
            for (int i = 0; i < Customizers.Length; ++i)
            {
                ICustomizer customizer = Customizers[(offset + i) % Customizers.Length];
                if (!customizer.Customize(this, tilemap)) continue;
                tilemap.CompressBounds();
                tilemap.RefreshAllTiles();
                tilemap.transform.position = tilemap.transform.position.WithX(Width);
                tilemap.transform.SetParent(transform);
                Width += tilemap.cellBounds.xMax;
                tilemap.name = $"{customizer.GetType().Name.Replace("TilemapCustomizer+", "")} ->{Width}";
                return;
            }
            throw new NotSupportedException();
        }
        [Serializable]
        public struct Initialize : ICustomizer
        {
            public Vector2Int InitArea;
            public ExtentInt.MinMax Count;
            public Tile[] Tiles;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count > 0) return false;
                Platform.Prepare(thiz);
                // We won't necessarily spawn Count platforms, in case they all collide.
                for (int i = 0, count = UnityRandoms.main.RandomValue(Count); i < count; ++i)
                {
                    Platform @new = new()
                    {
                        Height = UnityRandoms.main.Range(0, InitArea.y),
                        Tile = UnityRandoms.main.RandomValue(Tiles)
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
                    Platform.ContinueX.Insert(aboveI, UnityRandoms.main.Range(0, InitArea.x));
                }
                return Platform.ExtrudeTo(thiz, tilemap, InitArea.x);
            }
        }
        [Serializable]
        public struct Extrude : ICustomizer
        {
            public ExtentInt.MinMax Width;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0) return false;
                Platform.Prepare(thiz);
                return Platform.ExtrudeTo(thiz, tilemap, UnityRandoms.main.RandomValue(Width));
            }
        }

        [Serializable]
        public struct Climb : ICustomizer
        {
            [Range(0f, 1f)] public float FallOdds;
            public ExtentInt.MinMax Rise;
            public ExtentInt.MinMax Run;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0) return false;
                int rise = (UnityRandoms.main.RandomTrue(FallOdds) ? -1 : +1) * UnityRandoms.main.RandomValue(Rise), run = UnityRandoms.main.RandomValue(Run);
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
            public Extent.MinMax SetPoint;
            public ExtentInt.MinMax DeltaHeight;
            public ExtentInt.MinMax Width;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count < 2) return false;
                Platform.Prepare(thiz);

                Vector2Int low = thiz.Platforms[0].Height * Vector2Int.one, high = thiz.Platforms[^1].Height * Vector2Int.one;
                // Amount of spread to apportion between newLow/newHigh.
                int deltaHeight = Math.DivRem(UnityRandoms.main.RandomValue(DeltaHeight), 2, out int deltaHeightRem);

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
                int width = UnityRandoms.main.RandomValue(Width);
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
            public ExtentInt.MinMax Width;
            public ExtentInt.MinMax Height;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0) return false;
                Platform.Prepare(thiz);
                for (int i = 0; i < thiz.Platforms.Count; ++i)
                {
                    if (!UnityRandoms.main.RandomTrue(Odds)) continue;
                    Platform platform = thiz.Platforms[i];
                    int x = UnityRandoms.main.RandomValue(Width);
                    platform.ExtrudeTo(tilemap, 0, x);
                    Platform.ContinueX[i] = x;

                    platform.Tile = UnityRandoms.main.RandomValue(Tiles);
                    platform.ClampDeltaHeight(UnityRandoms.main.RandomValue(Height), thiz.Platforms.GetValueOrDefault(i - 1), thiz.Platforms.GetValueOrDefault(i + 1), thiz.VClear);
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
            public ExtentInt.MinMax Before;
            public ExtentInt.MinMax Gap;
            public ExtentInt.MinMax After;
            public ExtentInt.MinMax Drop;
            // Not *actually* odds; for instance, .5 means "one half overall" (and: rounded up!).
            [Range(0f, 1f)] public float Odds;
            public Tile[] Tiles;
            [Range(0f, 1f)] public float TileOdds;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0 || Odds <= 0f) return false;
                Platform.Prepare(thiz);
                foreach (int i in UnityRandoms.main.RandomIndices(thiz.Platforms.Count, Odds))
                {
                    Platform platform = thiz.Platforms[i];
                    int before = UnityRandoms.main.RandomValue(Before);
                    platform.ExtrudeTo(tilemap, 0, before);
                    Platform.ContinueX[i] = before + UnityRandoms.main.RandomValue(Gap);
                    platform.ClampDeltaHeight(-UnityRandoms.main.RandomValue(Drop), thiz.Platforms.GetValueOrDefault(i - 1), thiz.Platforms.GetValueOrDefault(i + 1), thiz.VClear);
                    if (UnityRandoms.main.RandomTrue(Odds)) platform.Tile = UnityRandoms.main.RandomValue(Tiles);
                    thiz.Platforms[i] = platform;
                }
                int max = Platform.MaxContinue;
                if (max <= 0) return false;
                max += UnityRandoms.main.RandomValue(After);
                Platform.ExtrudeTo(thiz, tilemap, max);
                return true;
            }
        }
        [Serializable]
        public struct Terminate : ICustomizer
        {
            [Range(0f, 1f)] public float Odds;
            public ExtentInt.MinMax Width;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 1) return false;
                int width = UnityRandoms.main.RandomValue(Width);
                Platform.Prepare(thiz);
                void UnwrapBackwards(IEnumerator<int> indices)
                {
                    if (!indices.MoveNext()) return;
                    int target = indices.Current;
                    UnwrapBackwards(indices);
                    int remainWidth = UnityRandoms.main.Range(0, width);
                    thiz.Platforms[target].ExtrudeTo(tilemap, 0, remainWidth);
                    thiz.Platforms.RemoveAt(target);
                    Platform.ContinueX.RemoveAt(target);
                };
                UnwrapBackwards(UnityRandoms.main.RandomIndices(thiz.Platforms.Count, Odds).GetEnumerator());
                Platform.ExtrudeTo(thiz, tilemap, width);
                return true;
            }
        }
        [Serializable]
        public struct Fork : ICustomizer
        {
            public ExtentInt.MinMax RequirePlatforms;
            public ExtentInt.MinMax Width;
            public ExtentInt.MinMax HGap;  // Of up to width.
            public ExtentInt.MinMax Up;  // Height the upper path can bump up
            public ExtentInt.MinMax Down;  // Height below Up to drop (you must choose min > clearance!).
            public Tile[] Tiles;
            [Range(0f, 1f)] public float TileOdds;
            public bool Customize(TilemapCustomizer thiz, Tilemap tilemap)
            {
                if (thiz.Platforms.Count <= 0) return false;
                if (thiz.Platforms.Count > RequirePlatforms.max) return false;
                int splitplatforms = UnityRandoms.main.RandomValue(RequirePlatforms) / 2;

                int width = UnityRandoms.main.RandomValue(Width);
                Platform.Prepare(thiz);
                foreach (int platformI in UnityRandoms.main.RandomIndices(thiz.Platforms.Count, splitplatforms))
                {
                    Platform platform = thiz.Platforms[platformI];
                    int up = platform.Height + UnityRandoms.main.RandomValue(Up);
                    int down = up - UnityRandoms.main.RandomValue(Down);
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
                        int hgap = Mathf.Min(UnityRandoms.main.RandomValue(HGap), width - 2);
                        int before = UnityRandoms.main.Range(1, width - hgap);

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
                        if (UnityRandoms.main.RandomTrue(TileOdds)) above.Tile = UnityRandoms.main.RandomValue(Tiles);
                        thiz.Platforms[platformI] = above;
                        below.Height = down;
                        if (UnityRandoms.main.RandomTrue(TileOdds)) below.Tile = UnityRandoms.main.RandomValue(Tiles);
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