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
            public Topic FirePad;
            Disposes.All unsubscribe;
            public void OnEnable(CharacteristicMovement thiz)
            {
                unsubscribe ??= new();
                if (DirPad) unsubscribe.Add(DirPad.Subscribe(thiz.Controller.SetWantMove));
                if (FirePad) unsubscribe.Add(FirePad.Subscribe(() => thiz.Controller.WantFire = true));
            }
            public void OnDisable() => unsubscribe.Dispose();
            public void OnUpdate(CharacteristicMovement thiz) { }
        }

        [Serializable]
        protected struct Randomwalk : IPattern
        {
            public Delay Timer;
            public Delay FireTimer;
            public Extent DXRange;
            public Extent DYRange;
            public Extent FireRange;
            public Extent DelayRange;
            public float ReverseOdds;
            public float JumpOdds;
            public float MaxAcc;
            public Coroutine Coroutine;
            public void OnDisable()
            {
                Timer.Stop();
                FireTimer.Stop();
            }
            public void OnEnable(CharacteristicMovement thiz)
            {
                thiz.Controller.WantMove = new(-1f, 0f);
                FireTimer = new(UnityRandoms.main.RandomValue(FireRange));
            }
            public void OnUpdate(CharacteristicMovement thiz)
            {
                if (!FireTimer)
                {
                    thiz.Controller.WantFire = true;
                    FireTimer = new(UnityRandoms.main.RandomValue(FireRange));
                }
                if (Timer) return;
                Timer = new(UnityRandoms.main.RandomValue(DelayRange));
                Vector2 want = new(UnityRandoms.main.RandomValue(DXRange), UnityRandoms.main.RandomValue(DYRange));
                want.x *= Mathf.Sign(thiz.Controller.WantMove.x);
                if (UnityRandoms.main.RandomTrue(ReverseOdds)) want.x *= -1;

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