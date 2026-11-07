using System;
using System.Collections.Generic;
using UnityEngine;

namespace VictorianAristocrat
{
    public static class Economy
    {
        public static int Get(List<StockEntry> entries, string good)
        {
            var entry = entries.Find(x => x.good == good);
            return entry == null ? 0 : entry.amount;
        }

        public static void Add(List<StockEntry> entries, string good, int quantity)
        {
            var entry = entries.Find(x => x.good == good);
            if (entry == null) { entry = new StockEntry { good = good }; entries.Add(entry); }
            entry.amount = Math.Max(0, entry.amount + quantity);
        }

        public static int Capacity(GameState state) { return 30 + Mathf.Max(1, state.attachedWagons) * 70; }
        public static int CargoWeight(GameCatalog catalog, GameState state)
        {
            int weight = 0;
            foreach (var item in state.cargo)
            {
                var good = catalog.Good(item.good);
                if (good != null) weight += item.amount * good.weight;
            }
            return weight;
        }

        public static int Price(GameCatalog catalog, SettlementState market, GoodDef good)
        {
            int stock = Get(market.stock, good.id);
            int target = 35;
            float scarcity = Mathf.Clamp(1f + (target - stock) / 70f, 0.55f, 1.9f);
            float crisis = 1f;
            if (market.crisis == CrisisKind.War && good.kind == GoodKind.Arms)
                crisis += market.severity * 0.16f;
            if (market.crisis == CrisisKind.Famine && good.kind == GoodKind.Food)
                crisis += market.severity * 0.14f;
            return Mathf.Max(1, Mathf.RoundToInt(good.basePrice * scarcity * crisis));
        }

        public static bool Trade(GameCatalog catalog, GameState state, string goodId, int quantity, bool buy, int salePrice, out string message)
        {
            message = "";
            var good = catalog.Good(goodId);
            var market = state.settlements.Find(x => x.id == state.settlement);
            if (good == null || market == null || quantity <= 0) { message = "Geçersiz işlem."; return false; }
            int reference = Price(catalog, market, good);
            int unit = buy ? reference : (salePrice <= 0 ? Mathf.RoundToInt(reference * .85f) : salePrice);
            int total = unit * quantity;
            if (buy)
            {
                if (Get(market.stock, goodId) < quantity) { message = "Pazarda yeterli stok yok."; return false; }
                if (state.money < total) { message = "Yeterli para yok."; return false; }
                if (CargoWeight(catalog, state) + quantity * good.weight > Capacity(state)) { message = "Konvoy kapasitesi dolu."; return false; }
                Add(market.stock, goodId, -quantity);
                Add(state.cargo, goodId, quantity);
                state.money -= total;
            }
            else
            {
                if (Get(state.cargo, goodId) < quantity) { message = "Yeterli yük yok."; return false; }
                if (unit > reference) { message = "Pazar bu fiyatı kabul etmiyor."; return false; }
                Add(state.cargo, goodId, -quantity);
                Add(market.stock, goodId, quantity);
                state.money += total;
                bool needed = (market.crisis == CrisisKind.War && good.kind == GoodKind.Arms) ||
                              (market.crisis == CrisisKind.Famine && good.kind == GoodKind.Food);
                if (needed)
                {
                    market.severity = Mathf.Max(0, market.severity - Mathf.Max(1, quantity / 8));
                    if (market.crisis == CrisisKind.War) market.warProgress += quantity;
                    if (market.severity == 0) market.crisis = CrisisKind.None;
                }
                if (needed && unit < reference)
                    market.relationship = Mathf.Clamp(market.relationship + Mathf.Max(1, quantity * (reference - unit) / Math.Max(1, reference * 2)), -100, 100);
            }
            message = buy ? "Mal satın alındı." : "Mal satıldı.";
            return true;
        }

        public static void AdvanceDay(GameCatalog catalog, GameState state)
        {
            state.day++;
            foreach (var market in state.settlements)
            {
                var def = catalog.Settlement(market.id);
                if (def == null) continue;
                Add(market.stock, def.specialty, def.city ? 5 : 8);
                if (def.city)
                {
                    Produce(market, "grain", "food", 3, 2);
                    Produce(market, "cotton", "cloth", 3, 2);
                    Produce(market, "iron", "metal", 3, 2);
                    if (Get(market.stock, "metal") >= 2 && Get(market.stock, "sulfur") >= 1 && Get(market.stock, "wood") >= 1)
                    {
                        Add(market.stock, "metal", -2); Add(market.stock, "sulfur", -1);
                        Add(market.stock, "wood", -1); Add(market.stock, "arms", 1);
                    }
                }
                foreach (var good in catalog.goods)
                {
                    int demand = good.kind == GoodKind.Food ? 2 : (good.kind == GoodKind.Raw ? 0 : 1);
                    if (market.crisis == CrisisKind.Famine && good.kind == GoodKind.Food) demand += 4;
                    if (market.crisis == CrisisKind.War && good.kind == GoodKind.Arms) demand += 3;
                    Add(market.stock, good.id, -Math.Min(demand, Get(market.stock, good.id)));
                }
                if (market.crisis == CrisisKind.Famine)
                {
                    if (Get(market.stock, "food") >= 12) market.severity = Math.Max(0, market.severity - 1);
                    else market.severity = Math.Min(5, market.severity + 1);
                }
                if (market.crisis == CrisisKind.War && market.warProgress >= 12)
                {
                    market.severity = Math.Max(0, market.severity - 1);
                    market.warProgress = 0;
                }
                if (market.severity == 0) market.crisis = CrisisKind.None;
            }
            foreach (var license in state.licenses) license.pending += license.level * 4;
        }

        static void Produce(SettlementState market, string input, string output, int needed, int result)
        {
            if (Get(market.stock, input) < needed) return;
            Add(market.stock, input, -needed);
            Add(market.stock, output, result);
        }
    }
}
