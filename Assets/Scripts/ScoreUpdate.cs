using BDUtil;
using BDUtil.Clone;
using BDUtil.Math;
using UnityEngine;

namespace ld51
{
    [RequireComponent(typeof(TMPro.TMP_Text))]
    public class ScoreUpdate : MonoBehaviour
    {
        float score;
        TMPro.TMP_Text text;
        readonly Disposes.All unsubscribe = new();
        protected void Awake()
        {
            text = GetComponent<TMPro.TMP_Text>().OrThrow();
        }
        protected void OnEnable()
        {
            unsubscribe.Add(Defaults.main.Coin.Topic.Subscribe(OnCoin));
        }
        protected void OnDisable() => unsubscribe.Dispose();
        void OnCoin(Collider2D coin)
        {
            if (!coin.isTrigger) return;
            GameObject root = coin.GetComponent<Cloned>()?.Root;
            if (root != null && Defaults.main.CoinPoints.Collection.TryGetValue(root, out float value))
            {
                score += value;
                text.OrThrow().text = $"Score: {score}";
                Vector3 start = coin.transform.localScale, end = new(2f, 0f, 1f);
                Coroutines.StartCoroutine(new Timer(1f).Foreach(t => coin.transform.localScale = Vector3.Lerp(start, end, Easings.Impl.OutBounce(t)), () => Destroy(coin.gameObject)));
            }
        }
    }
}