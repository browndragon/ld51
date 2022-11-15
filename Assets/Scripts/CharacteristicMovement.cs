using System;
using System.Collections;
using BDUtil;
using BDUtil.Math;
using BDUtil.Pubsub;
using UnityEngine;

namespace ld51
{
    [RequireComponent(typeof(CharacterController2D))]
    public class CharacteristicMovement : MonoBehaviour
    {
        protected interface IPattern
        {
            void OnDisable();
            void OnEnable(CharacteristicMovement thiz);
            void OnUpdate(CharacteristicMovement thiz);
        }
        [SerializeReference, Subtype] IPattern Pattern;
        [HideInInspector] public CharacterController2D Controller;
        protected void Awake() => Controller = GetComponent<CharacterController2D>();
        protected void OnDisable() => Pattern?.OnDisable();
        protected void OnEnable() => Pattern?.OnEnable(this);
        // Update is called once per frame
        protected void Update() => Pattern?.OnUpdate(this);

        [Serializable]
        protected struct Input : IPattern
        {
            public Topic<Vector2> DirPad;
            public Topic<bool> FirePad;
            public Topic<bool> InteractPad;
            Disposes.All unsubscribe;
            public void OnEnable(CharacteristicMovement thiz)
            {
                unsubscribe ??= new();
                if (DirPad) unsubscribe.Add(DirPad.Subscribe(thiz.Controller.SetWantMove));
                if (FirePad) unsubscribe.Add(FirePad.Subscribe(b => thiz.Controller.WantFire = b));
                if (InteractPad) unsubscribe.Add(InteractPad.Subscribe(b => thiz.Controller.WantInteract = b));
            }
            public void OnDisable() => unsubscribe.Dispose();
            public void OnUpdate(CharacteristicMovement thiz) { }
        }

        [Serializable]
        protected struct Randomwalk : IPattern
        {
            public Delay Timer;
            public Delay FireTimer;
            public Interval DXRange;
            public Interval DYRange;
            public Interval FireRange;
            public Interval DelayRange;
            public float ReverseOdds;
            public float JumpOdds;
            public float MaxAcc;
            public Coroutine Coroutine;
            public Bounds Anchor;
            public void OnDisable()
            {
                Timer.Stop();
                FireTimer.Stop();
            }
            public void OnEnable(CharacteristicMovement thiz)
            {
                thiz.Controller.WantMove = new(-1f, 0f);
                FireTimer = new(Randoms.main.Range(FireRange));
                Anchor.center = default;
            }
            public void OnUpdate(CharacteristicMovement thiz)
            {
                if (Anchor.center == default && Anchor.size != default) Anchor.center = thiz.transform.position;
                if (!FireTimer)
                {
                    thiz.Controller.WantFire = true;
                    FireTimer = new(Randoms.main.Range(FireRange));
                }
                if (Timer) return;
                Timer = new(Randoms.main.Range(DelayRange));
                Vector3 want = new(Randoms.main.Range(DXRange), Randoms.main.Range(DYRange));
                want.x *= Mathf.Sign(thiz.Controller.WantMove.x);
                if (Randoms.main.Odds(ReverseOdds)) want.x *= -1;

                if (!Anchor.Contains(thiz.transform.position) && Anchor.size != default)
                {
                    Vector3 pos = thiz.transform.position;
                    Anchor.Bounce(pos, ref want);
                }

                if (Coroutine != null) thiz.Controller.StopCoroutine(Coroutine);
                thiz.StartCoroutine(AdjustSpeedTo(thiz.Controller, want));
            }
            IEnumerator AdjustSpeedTo(CharacterController2D thiz, Vector2 target)
            {
                Vector2 start = thiz.WantMove;
                Vector2 delta = target - start;
                float length = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                float duration = Mathf.Abs(length / MaxAcc);
                foreach (var tick in new Delay(duration))
                {
                    thiz.WantMove = start + delta * tick;
                    yield return null;
                }
                thiz.WantMove = target;
                Coroutine = null;
            }
        }
    }
}