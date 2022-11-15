using System;
using BDUtil;
using BDUtil.Fluent;
using BDUtil.Library;
using BDUtil.Math;
using BDUtil.Pubsub;
using BDUtil.Screen;
using UnityEngine;
using UnityEngine.Events;

namespace ld51
{
    [RequireComponent(typeof(TMPro.TMP_Text))]
    public class ScoreUpdate : MonoBehaviour
    {
        public Topic<float> Score;
        TMPro.TMP_Text text;
        readonly Disposes.All unsubscribe = new();

        protected void Awake()
        { text = GetComponent<TMPro.TMP_Text>().OrThrow(); }
        protected void OnEnable()
        {
            unsubscribe.Add((Score ?? Defaults.main.Score.Topic).Subscribe(OnScoreUpdated));
        }
        protected void OnDisable() => unsubscribe.Dispose();
        void OnScoreUpdated(float score) => text.text = $"Score: {score}";
    }
}
