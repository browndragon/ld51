using BDUtil;
using BDUtil.Fluent;
using BDUtil.Math;
using BDUtil.Pubsub;
using UnityEngine;

namespace ld51
{
    [RequireComponent(typeof(Collider2D))]
    public class Coin : MonoBehaviour
    {
        public float Points = 1f;
        public float Odds => 1f / Points;
        new Collider2D collider;
        ValueTopic<float> Score;
        protected void Awake()
        {
            Score = Defaults.main.Score.Topic.OrThrow();
            collider = GetComponent<Collider2D>();
        }
        protected void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                Score.Value += Points;
                StartCoroutine(new Delay(.5f)
                    .Let(out var start, transform.localScale)
                    .Let(out var mid, new Vector3(2f * start.x, 0f, start.z))
                    .Foreach(
                        t => transform.localScale = Vector3.Lerp(start, mid, Easings.OutBounce(t)),
                        () => StartCoroutine(new Delay(.25f)
                            .Let(out var end, new Vector3(0f, 3f * start.y, start.z))
                            .Foreach(
                                t => transform.localScale = Vector3.Lerp(mid, end, Easings.OutBounce(t)),
                                () => Destroy(collider.gameObject)
                            )
                        )
                    )
                );
            }
        }
    }
}