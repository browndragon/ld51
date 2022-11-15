using System;
using System.Collections.Generic;
using BDUtil;
using BDUtil.Clone;
using BDUtil.Math;
using UnityEngine;

namespace ld51
{
    // Streaming alternating-velocity total random generation line
    [RequireComponent(typeof(Collider2D))]
    public class SourceSpawn : MonoBehaviour
    {
        public float totalCoinOdds;

        public Rigidbody2D PlatformProto;

        public float MinDistance = 3f;

        [Tooltip("output velocity")]
        public Interval dX = new(7f, 8f);

        [Tooltip("When we do adjust dY, by how much?")]
        public Interval ddY = new(-.125f, +.125f);

        [Tooltip("Odds of spawning heads (when asked)")]
        public AnimationCurves.Scaled SpawnHeads = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // Odds by distance *without* a coin we place a coin above our head.
        public AnimationCurve CoinOdds = new(new Keyframe[] { new(0f, .5f), new(2f, .5f), new(5f, 1f), new(10f, 0f), new(11f, 0f) });
        // Odds by distance that while drawing we change heading.
        public AnimationCurve SwerveOdds = AnimationCurve.Constant(0f, 1f, .1f);
        // Odds by distance that, given we are drawing, we continue drawing.
        public AnimationCurve StopOdds = new(new Keyframe[] { new(0f, 0f), new(1f, 0f), new(3f, .025f), new(5f, .05f), new(7f, .1f), new(9f, .25f), new(11f, .8f), new(13f, .5f), new(15f, 1f) });
        // Odds by distance that, given we are not drawing, we restart drawing
        public AnimationCurve RestartOdds = AnimationCurve.Constant(0f, 1f, .5f);
        // Odds by distance that, given we are not drawing, we kill this line off.
        public AnimationCurve AbortOdds = AnimationCurve.Constant(0f, 1f, .5f);

        // Odds by total active distance that we fork or merge a line.
        public AnimationCurve ForkOdds = new(new Keyframe[] { new(0f, 0f), new(6f, 0f), new(12f, .25f), new(18f, 0f), new(24f, 0f) });
        [Serializable]
        public class SpawnHead
        {
            public Vector2 SpawnPoint;
            public Vector2 dSpawnPoint;
            public Delay Delay;
            public bool isDrawing = true;
            public float turnsInState;  // amount of space we've gone in the current draw/nodraw state
            public float turnsInStateNoCoin;  // amount of space we've gone without placing a coin.
        }
        readonly List<SpawnHead> BlockHeads = new();
        float partialHead = 0;
        new Collider2D collider;

        IEnumerable<Coin> CoinProtos
        {
            get
            {
                foreach (GameObject coinProto in Defaults.main.CoinPoints.Collection.Keys)
                    yield return coinProto.GetComponent<Coin>();
            }
        }

        protected void Awake()
        {
            collider = GetComponent<Collider2D>();
            totalCoinOdds = 0f;
            foreach (Coin coin in CoinProtos)
            {
                totalCoinOdds += coin.Odds;
            }
        }
        protected void Start()
        {
            foreach (Postfab clone in FindObjectsOfType<Postfab>())
            {
                bool wasCoin = false;
                foreach (Coin coin in CoinProtos)
                {
                    if (clone.Asset != coin) continue;
                    Rigidbody2D rbCoin = clone.GetComponent<Rigidbody2D>();
                    rbCoin.velocity = new(-1f, 0f);
                    wasCoin = true;
                    break;
                }
                if (wasCoin) continue;
                if (clone.Asset != PlatformProto) continue;
                Rigidbody2D rbPlat = clone.GetComponent<Rigidbody2D>();
                rbPlat.velocity = new(-1f, 0f);
            }
        }
        Rect bounds
        {
            get
            {
                Bounds bounds = collider.bounds;
                return new(bounds.min, bounds.size);
            }
        }
        protected void Update()
        {
            if (BlockHeads.Count <= 0) partialHead += Randoms.main.Range(SpawnHeads);
            // Update each head by spawning one or more bricks.
            float total = 0f;
            foreach (SpawnHead spawnHead in BlockHeads)
            {
                if (spawnHead.isDrawing) continue;
                total += spawnHead.turnsInState;
            }
            if (UnityEngine.Random.Range(0f, 1f) <= ForkOdds.Evaluate(total)) partialHead += Mathf.Max(0f, Randoms.main.Range(SpawnHeads) - 1f);
            int AllLowerPositionsOld = BlockHeads.Count;
            for (; partialHead > 0f; partialHead -= 1f)
            {
                Vector3 spawnPoint = Randoms.main.Range(bounds);
                float xDisplacement = bounds.center.x - spawnPoint.x;
                float speed = Randoms.main.Range(dX);
                BlockHeads.Add(new()
                {
                    SpawnPoint = spawnPoint,
                    dSpawnPoint = new(speed, Randoms.main.Range(ddY)),
                    Delay = 1 / speed,
                    turnsInState = xDisplacement,
                    turnsInStateNoCoin = xDisplacement,
                });
            }
            for (int i = BlockHeads.Count - 1; i >= 0; --i)
            {
                SpawnHead head = BlockHeads[i];
                if (head.Delay) continue;
                head.Delay.Reset();
                head.turnsInState += 1f;
                if (UnityEngine.Random.Range(0f, 1f) <= SwerveOdds.Evaluate(head.turnsInState))
                {
                    head.dSpawnPoint.y += Randoms.main.Range(ddY);
                }
                head.SpawnPoint.y += head.dSpawnPoint.y;
                if (!bounds.Contains(head.SpawnPoint))
                {
                    BlockHeads.RemoveAt(i);
                    continue;
                }

                if (UnityEngine.Random.Range(0f, 1f) <= CoinOdds.Evaluate(head.turnsInStateNoCoin))
                {
                    float coinOdds = UnityEngine.Random.Range(0f, totalCoinOdds);
                    foreach (Coin coin in CoinProtos)
                    {
                        if ((coinOdds -= coin.Odds) > 0f) continue;
                        Coin coined = Pool.main.Acquire(coin);
                        coined.transform.position = head.SpawnPoint + Vector2.up;
                        Rigidbody2D coinRb = coined.GetComponent<Rigidbody2D>();
                        coinRb.velocity = new(-head.dSpawnPoint.x, 0f);
                        break;
                    }
                }
                else head.turnsInStateNoCoin += 1;

                if (!head.isDrawing && i < AllLowerPositionsOld)
                {
                    if (UnityEngine.Random.value <= AbortOdds.Evaluate(head.turnsInState))
                    {
                        BlockHeads.RemoveAt(i);
                        continue;
                    }
                    if (UnityEngine.Random.value <= RestartOdds.Evaluate(head.turnsInState))
                    {
                        head.isDrawing = true;
                        head.turnsInState = 1f;
                    }
                    else continue;
                }
                bool tooClose = false;
                for (int j = i + 1; j < BlockHeads.Count; ++j)
                {
                    if (!BlockHeads[j].isDrawing) continue;
                    if (Mathf.Abs(head.SpawnPoint.y - BlockHeads[j].SpawnPoint.y) < MinDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                {
                    head.isDrawing = false;
                    head.turnsInState = 0f;
                    continue;
                }
                Rigidbody2D platform = Pool.main.Acquire(PlatformProto);
                platform.transform.position = head.SpawnPoint;
                platform.velocity = new(-head.dSpawnPoint.x, 0f);
                if (UnityEngine.Random.Range(0f, 1f) <= StopOdds.Evaluate(head.turnsInState))
                {
                    head.isDrawing = false;
                    head.turnsInState = 0f;
                }
            }
        }
    }
}