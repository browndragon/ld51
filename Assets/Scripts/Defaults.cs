using System.Collections;
using System.Collections.Generic;
using BDUtil;
using BDUtil.Pubsub;
using BDUtil.Serialization;
using UnityEngine;

namespace ld51
{
    public class Defaults : StaticAsset<Defaults>
    {
        public Val<Collider2D> Coin;
        public StoreMap<GameObject, float> CoinPoints = new();
    }
}
