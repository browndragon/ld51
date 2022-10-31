using System;
using System.Collections;
using System.Collections.Generic;
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
        protected struct Randomwalk : IPattern
        {
            public Timer Timer;
            public Extent DXRange;
            public Extent DelayRange;
            public float ReverseOdds;
            public float JumpOdds;
            public float MaxAcc;
            public Coroutine Coroutine;
            public void OnDisable()
            {
                Timer.Stop();
            }
            public void OnEnable(CharacteristicMovement thiz)
            {
                thiz.Controller.WantDX = -1f;
            }
            public void OnUpdate(CharacteristicMovement thiz)
            {
                if (Timer.Tick.IsLive) return;
                Timer.Reset();
                float sign = Mathf.Sign(thiz.Controller.WantDX);
                if (UnityRandoms.main.RandomTrue(ReverseOdds)) sign *= -1f;
                if (Coroutine != null) thiz.Controller.StopCoroutine(Coroutine);
                thiz.StartCoroutine(AdjustSpeedTo(thiz.Controller, sign * UnityRandoms.main.RandomValue(DXRange)));
                thiz.Controller.WantJump = UnityRandoms.main.RandomTrue(JumpOdds);
            }
            IEnumerator AdjustSpeedTo(CharacterController2D thiz, float target)
            {
                float start = thiz.WantDX;
                float delta = target - start;
                float duration = Mathf.Abs(delta / MaxAcc);
                foreach (Tick tick in new Timer(duration))
                {
                    thiz.WantDX = start + delta * tick;
                    yield return null;
                }
                Coroutine = null;
            }
        }
    }
}