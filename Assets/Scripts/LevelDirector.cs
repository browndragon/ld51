using BDUtil;
using BDUtil.Clone;
using BDUtil.Math;
using BDUtil.Pubsub;
using UnityEngine;

namespace ld51
{
    [RequireComponent(typeof(TilemapCustomizer), typeof(AudioSource))]
    public class LevelDirector : MonoBehaviour
    {
        GameObject SpotlightProto;
        public Val<GameObject> Spotlight;
        public Delay Timer = 10f;
        public Delay SoundTimer = 1f;
        public AudioClip tickSound;
        AudioSource audioSource;
        protected void Awake() => audioSource = GetComponent<AudioSource>();
        protected void Update()
        {
            if (!SoundTimer.HasStart) SoundTimer.Reset();
            if (!Timer.HasStart) Timer.Reset();
            if (!SoundTimer)
            {
                audioSource.PlayOneShot(tickSound, Easings.OutQuad(Timer.Ratio));
                SoundTimer.Reset();
            }
            if (!Timer)
            {
                Debug.Log($"{Timer.Length} is up! Recycling {Spotlight.Value}");
                if (Spotlight.Value != null) Respawn();
            }
        }

        protected void Respawn()
        {
            SoundTimer.Reset();
            Timer.Reset();
            Pool.main.Release(Spotlight.Value);
            foreach (GameObject @object in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                Pool.main.Release(@object);
            }
        }
    }
}