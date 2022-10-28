using System;
using BDUtil;
using BDUtil.Fluent;
using BDUtil.Library;
using BDUtil.Math;
using BDUtil.Pubsub;
using BDUtil.Screen;
using UnityEngine;

namespace ld51
{
    [RequireComponent(typeof(TMPro.TMP_Text))]
    public class ScoreUpdate : MonoBehaviour, Snapshots.IFuzzControls
    {
        public AudioLibrary Library;
        public Topic<float> Score;
        TMPro.TMP_Text text;
        AudioSource audioSource;
        readonly Disposes.All unsubscribe = new();

        public Randoms.UnitRandom Random => UnityRandoms.main.DistributionPow01(Library.Chaos);
        public float Power => Library.Power;
        public float Speed => Library.Speed;
        Camera Snapshots.IFuzzControls.camera => throw new NotSupportedException();
        SpriteRenderer Snapshots.IFuzzControls.renderer => throw new NotSupportedException();
        AudioSource Snapshots.IFuzzControls.audio => audioSource;
        SpriteRenderers.Snapshot Snapshots.IFuzzControls.rendererSnapshot => throw new NotSupportedException();
        AudioSources.Snapshot Snapshots.IFuzzControls.audioSnapshot => new()
        {
            AudioClip = null,
            Volume = 1f,
            Pitch = 1f,
        };

        protected void Awake()
        {
            text = GetComponent<TMPro.TMP_Text>().OrThrow();
            audioSource = GetComponent<AudioSource>().OrThrow();
        }
        protected void OnEnable()
        {
            unsubscribe.Add((Score ?? Defaults.main.Score.Topic).Subscribe(OnScore));
        }
        protected void OnDisable() => unsubscribe.Dispose();
        void OnScore(float score)
        {
            Library.ICategory category = Library.GetICategory("");
            int index = category.GetRandom();
            Library.IEntry entry = category.Entries[index];
            Library.Play(this, entry);
            text.text = $"Score: {score}";
        }
    }
}
