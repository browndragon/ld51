using BDUtil.Pubsub;
using BDUtil.Serialization;
using UnityEngine;

namespace ld51
{
    public class Defaults : StaticAsset<Defaults>
    {
        public StoreMap<GameObject, float> CoinPoints = new();
        public GameObject Block;
        public Val<float> Score;
        public Val<Vector2> Controls;
        public Val<GameObject> FrameSwap;
    }
}
