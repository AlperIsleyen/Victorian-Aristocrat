using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VictorianAristocrat
{
    public class GameController : MonoBehaviour
    {
        public class SaveEntry
        {
            public string path;
            public bool automatic;
            public DateTime writtenUtc;
            public GameState preview;
            public bool valid;
        }
        public static GameController Instance { get; private set; }
        public GameCatalog catalog;
        public GameState state;
        public event Action Changed;
        public string LastMessage { get; private set; }
        public string SavePath { get { return Path.Combine(Application.persistentDataPath, "victorian_aristocrat.json"); } }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (catalog == null) catalog = Resources.Load<GameCatalog>("GameCatalog");
            if (catalog == null) { Debug.LogError("GameCatalog missing from Resources."); enabled = false; return; }
            // The main menu chooses which game to start. Keep direct scene play useful in the Editor.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu" && !LoadLatest()) NewGame();
        }

        public void NewGame()
        {
            state = new GameState();
            foreach (var def in catalog.settlements)
            {
                var market = new SettlementState { id = def.id, crisis = def.initialCrisis, severity = def.crisisSeverity };
                foreach (var good in catalog.goods)
                {
                    int stock = good.id == def.specialty ? 65 : (def.city ? 22 : 8);
                    if (def.initialCrisis == CrisisKind.Famine && (good.id == "food" || good.id == "grain")) stock = 2;
                    if (def.initialCrisis == CrisisKind.War && good.id == "arms") stock = 1;
                    market.stock.Add(new StockEntry { good = good.id, amount = stock });
                }
                state.settlements.Add(market);
            }
            Economy.Add(state.cargo, "grain", 12);
            Economy.Add(state.cargo, "wood", 8);
            Say("Şirketiniz kuruldu. Pazara uğrayıp ticarete başlayın.");
        }

        public bool Load()
        {
            return LoadFrom(SavePath);
        }

        public bool LoadLatest()
        {
            foreach (var entry in Saves()) if (entry.valid && LoadFrom(entry.path)) return true;
            return false;
        }

        public List<SaveEntry> Saves()
        {
            var result = new List<SaveEntry>();
            if (File.Exists(SavePath)) result.Add(Inspect(SavePath, true));
            string directory = Path.Combine(Application.persistentDataPath, "Saves");
            if (Directory.Exists(directory)) foreach (string path in Directory.GetFiles(directory, "*.json")) result.Add(Inspect(path, false));
            result.Sort((a, b) => b.writtenUtc.CompareTo(a.writtenUtc));
            return result;
        }

        SaveEntry Inspect(string path, bool automatic)
        {
            var entry = new SaveEntry { path = path, automatic = automatic, writtenUtc = File.GetLastWriteTimeUtc(path) };
            entry.valid = TryRead(path, out entry.preview);
            return entry;
        }

        bool TryRead(string path, out GameState loaded)
        {
            loaded = null;
            try
            {
                loaded = JsonUtility.FromJson<GameState>(File.ReadAllText(path));
                if (loaded == null || (loaded.version != 1 && loaded.version != 2) || loaded.settlements == null || loaded.settlements.Count != catalog.settlements.Count) return false;
                MigrateSave(loaded);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("Save load failed: " + e.Message); return false; }
        }

        public bool LoadFrom(string path)
        {
            if (!TryRead(path, out var loaded)) return false;
            state = loaded;
            state.attachedWagons = Mathf.Clamp(state.attachedWagons, 1, Mathf.Max(1, state.wagons));
            Say("Kayıt yüklendi.");
            return true;
        }

        public bool DeleteSave(string path)
        {
            if (path != SavePath && Path.GetDirectoryName(path) != Path.Combine(Application.persistentDataPath, "Saves")) return false;
            try { File.Delete(path); return true; }
            catch (Exception e) { Say("Kayıt silinemedi: " + e.Message); return false; }
        }

        public bool CreateManualSave()
        {
            CapturePosition();
            string directory = Path.Combine(Application.persistentDataPath, "Saves");
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(state, true));
                Say("Yeni kayıt oluşturuldu.");
                return true;
            }
            catch (Exception e) { Say("Kayıt başarısız: " + e.Message); return false; }
        }

        public static void MigrateSave(GameState loaded)
        {
            if (loaded.version == 1) { loaded.version = 2; loaded.hasWorldPosition = false; }
            if (loaded.hasWorldPosition && !WorldGeography.IsLand(loaded.worldPosition.x, loaded.worldPosition.z, 3f)) loaded.hasWorldPosition = false;
        }

        public void Save()
        {
            CapturePosition();
            SaveSnapshot();
        }

        public bool SaveSnapshot()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(state, true)); Say("Oyun kaydedildi."); return true; }
            catch (Exception e) { Say("Kayıt başarısız: " + e.Message); return false; }
        }

        void CapturePosition()
        {
            if (WorldRuntime.Instance == null || state == null) return;
            Vector3 position = WorldRuntime.Instance.PlayerPosition;
            state.worldPosition = new Vector3(position.x, 0, position.z);
            state.hasWorldPosition = true;
        }

        public void Say(string message) { LastMessage = message; Changed?.Invoke(); }
        public void EnterSettlement(int id)
        {
            if (state.settlement == id) return;
            state.settlement = id;
            Say(catalog.Settlement(id).displayName + " konumuna ulaştınız.");
            SaveSilent();
        }
        public void SaveWorldPosition(Vector3 position)
        {
            state.worldPosition = new Vector3(position.x, 0, position.z);
            state.hasWorldPosition = true;
            SaveSilent();
        }
        public SettlementState CurrentMarket { get { return state.settlements.Find(x => x.id == state.settlement); } }
        public SettlementDef CurrentPlace { get { return catalog.Settlement(state.settlement); } }

        public bool Trade(string good, int count, bool buy, int salePrice = 0)
        {
            bool done = Economy.Trade(catalog, state, good, count, buy, salePrice, out string message);
            Say(message);
            if (done) SaveSilent();
            return done;
        }

        public void BuyWagon()
        {
            int price = 180 + 60 * Math.Max(0, state.wagons - 1);
            if (state.money < price) { Say("At arabası için yeterli para yok."); return; }
            state.money -= price;
            state.wagons++;
            Say("Yeni at arabası satın alındı. Arabacıdan konvoya bağlayın.");
            SaveSilent();
        }

        public void AttachWagon()
        {
            if (state.attachedWagons >= state.wagons) { Say("Bağlanacak boş araba yok."); return; }
            state.attachedWagons++;
            Say("Araba konvoyun arkasına bağlandı.");
            SaveSilent();
        }

        public void DetachWagon()
        {
            if (state.attachedWagons <= 1) { Say("Ana araba ayrılamaz."); return; }
            int newCapacity = 30 + (state.attachedWagons - 1) * 70;
            if (Economy.CargoWeight(catalog, state) > newCapacity) { Say("Önce fazla yükü boşaltın."); return; }
            state.attachedWagons--;
            Say("Son araba konvoydan ayrıldı.");
            SaveSilent();
        }

        public void BuyLicense()
        {
            var def = CurrentPlace;
            if (def == null) return;
            if (state.licenses.Exists(x => x.settlement == def.id)) { Say("Burada zaten ruhsatınız var."); return; }
            int price = def.city ? 300 : 180;
            if (state.money < price) { Say("Ruhsat için yeterli para yok."); return; }
            state.money -= price;
            state.licenses.Add(new LicenseState { settlement = def.id, good = def.specialty });
            Say(def.specialty + " üretim ruhsatı alındı. Günlük ürün yerel pazara aktarılır.");
            SaveSilent();
        }

        public void CollectProduction()
        {
            var license = state.licenses.Find(x => x.settlement == state.settlement);
            if (license == null || license.pending <= 0) { Say("Teslim alınacak üretim yok."); return; }
            var good = catalog.Good(license.good);
            int space = (Economy.Capacity(state) - Economy.CargoWeight(catalog, state)) / good.weight;
            int quantity = Math.Min(license.pending, space);
            if (quantity <= 0) { Say("Konvoyda yer yok."); return; }
            license.pending -= quantity;
            Economy.Add(state.cargo, license.good, quantity);
            Say(quantity + " birim ürün yüklendi.");
            SaveSilent();
        }

        public bool Travel(RouteDef route)
        {
            if (route == null || !catalog.routes.Contains(route) || (route.from != state.settlement && route.to != state.settlement)) { Say("Geçersiz rota."); return false; }
            if (route.kind == RouteKind.Sea && (!catalog.Settlement(route.from).port || !catalog.Settlement(route.to).port)) { Say("Gemi yalnızca limanlar arasında çalışır."); return false; }
            int cargo = Economy.CargoWeight(catalog, state);
            if (cargo > route.capacity) { Say("Bu sefer tüm yükünüzü taşıyamıyor."); return false; }
            int fare = route.fare + Math.Max(0, cargo / 10) * (route.kind == RouteKind.Road ? 0 : 2);
            if (state.money < fare) { Say("Yolculuk ücreti için yeterli para yok."); return false; }
            state.money -= fare;
            for (int i = 0; i < route.days; i++) Economy.AdvanceDay(catalog, state);
            state.settlement = catalog.OtherEnd(route, state.settlement);
            state.hasWorldPosition = false;
            SaveSilent();
            Say((route.kind == RouteKind.Sea ? "Gemiyle " : "") + catalog.Settlement(state.settlement).displayName + " konumuna ulaştınız.");
            WorldRuntime.Instance?.PlacePlayerAtCurrentSettlement();
            return true;
        }

        public void Rest()
        {
            Economy.AdvanceDay(catalog, state);
            Say("Bir gün geçti.");
            SaveSilent();
        }

        void SaveSilent()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(state, true)); }
            catch (Exception e) { Debug.LogWarning("Autosave failed: " + e.Message); }
            Changed?.Invoke();
        }
    }
}
