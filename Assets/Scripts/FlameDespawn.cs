using System.Collections;
using BDUtil.Clone;
using BDUtil.Library;
using BDUtil.Math;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(Player))]
public class FlameDespawn : MonoBehaviour
{
    public float DeathSpeed = 25f;
    Player player;
    protected void Awake() => player = GetComponent<Player>();
    protected void OnEnable()
    {
        SpriteRenderer me = GetComponent<SpriteRenderer>();
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer == me) continue;
            StartCoroutine(Shimmer(renderer));
        }
    }
    public Interval FadeTime = new(2f, 1f);
    public Interval FadeDistance = new(0f, 1f);
    public HSVA FadeColor = new(.1f, .1f, 0f, 0f);
    IEnumerator Shimmer(SpriteRenderer renderer)
    {
        HSVA origColor = renderer.color;
        HSVA prevColor = origColor;
        Vector3 origPosition = renderer.transform.localPosition;
        Vector3 prevPosition = origPosition;
        while (true)
        {
            HSVA targetColor = Randoms.main.Fuzz(origColor, FadeColor);
            targetColor.a = origColor.a;
            Vector3 targetPosition = origPosition + Randoms.main.Range(FadeDistance) * Vector3.left;
            yield return new Delay(Randoms.main.Range(FadeTime)).Foreach(
                t =>
                {
                    renderer.color = Color.Lerp(prevColor, targetColor, t);
                    renderer.transform.localPosition = Vector3.Lerp(prevPosition, targetPosition, t);
                }
            );
        }
    }

    protected void OnTriggerEnter2D(Collider2D doomed)
    {
        if (doomed.attachedRigidbody != null) doomed.attachedRigidbody.velocity += DeathSpeed * Vector2.left;
    }
    protected void OnTriggerExit2D(Collider2D doomed)
    {
        if (doomed != null)
        {
            if (doomed.attachedRigidbody != null) doomed.attachedRigidbody.velocity = Vector2.zero;
            if (doomed.gameObject != null) Pool.main.Release(doomed.gameObject);
        }
        player.PlayCurrentCategory();
    }
}
