using System.Collections;
using BDUtil;
using BDUtil.Clone;
using BDUtil.Math;
using UnityEngine;

namespace ld51
{
    [RequireComponent(typeof(Collider2D))]
    public class TouchHarm : MonoBehaviour
    {
        public Delay DieFor = .25f;
        protected void OnDisable()
        {
            DieFor.Stop();
        }
        protected void OnEnable()
        {
            DieFor.Stop();
        }
        protected void OnCollisionEnter2D(Collision2D collision)
        {
            if (DieFor) return;
            if (collision.gameObject.CompareTag(tag)) return;
            if (collision.gameObject.CompareTag("Terrain")) return;
            if (gameObject.GetInstanceID() < collision.gameObject.GetInstanceID()) return;
            for (int i = 0; i < collision.contactCount; ++i)
            {
                ContactPoint2D contact = collision.contacts[i];
                // The normal points away from the contacting body...
                bool isRight = .5f * contact.normal.x <= contact.normal.y && .5f * contact.normal.x <= -contact.normal.y;
                bool isLeft = .5f * contact.normal.x >= contact.normal.y && .5f * contact.normal.x >= -contact.normal.y;
                if (!isLeft && !isRight) continue;
                TouchHarm other = collision.gameObject.GetComponent<TouchHarm>();
                if (other == null) continue;
                if (other.DieFor) continue;
                other.AnimateDeath();
                AnimateDeath();
                return;
            }
        }
        public void AnimateDeath()
        {
            float start = transform.localScale.y;
            IEnumerator Animation()
            {
                DieFor.Reset();
                foreach (var tick in DieFor)
                {
                    transform.localScale = transform.localScale.WithY(Mathf.LerpUnclamped(start, 0f, Easings.OutBounce(tick)));
                    yield return null;
                }
                transform.localScale = transform.localScale.WithY(start);
                Pool.main.Release(gameObject);
            }
            StartCoroutine(Animation());
        }
    }
}
