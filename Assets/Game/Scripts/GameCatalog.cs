using System.Collections.Generic;
using UnityEngine;

namespace VictorianAristocrat
{
    [CreateAssetMenu(menuName = "Victorian Aristocrat/Game Catalog")]
    public class GameCatalog : ScriptableObject
    {
        public List<GoodDef> goods = new List<GoodDef>();
        public List<CountryDef> countries = new List<CountryDef>();
        public List<SettlementDef> settlements = new List<SettlementDef>();
        public List<RouteDef> routes = new List<RouteDef>();

        public GoodDef Good(string id) { return goods.Find(x => x.id == id); }
        public SettlementDef Settlement(int id) { return settlements.Find(x => x.id == id); }
        public IEnumerable<RouteDef> RoutesFrom(int id)
        {
            foreach (var route in routes)
                if (route.from == id || route.to == id) yield return route;
        }
        public int OtherEnd(RouteDef route, int id) { return route.from == id ? route.to : route.from; }
    }
}
