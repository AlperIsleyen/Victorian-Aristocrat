using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VictorianAristocrat
{
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }
        public MenuUI GameMenu { get; private set; }
        RectTransform panel;
        RectTransform content;
        TextMeshProUGUI title;
        TextMeshProUGUI status;
        TextMeshProUGUI prompt;
        TextMeshProUGUI toast;
        TextMeshProUGUI wagonLabel;
        RectTransform promptBack;
        RectTransform toastBack;
        RectTransform miniMap;
        RawImage miniGeography;
        RectTransform miniPlayer;
        RectTransform miniWagon;
        readonly RectTransform[] miniSettlements = new RectTransform[24];
        readonly List<RectTransform> miniRoads = new List<RectTransform>();
        readonly List<RouteDef> miniRoadRoutes = new List<RouteDef>();
        RectTransform wagonButton;
        string lastMessage = "";
        float toastUntil;
        int selectedRegion;
        string page = "";
        string selectedGood = "grain";
        int marketCategory;
        int amount = 1;
        bool discount;
        int rowIndex;
        readonly string[] categories = { "Tümü", "Ham kaynak", "Gıda", "İşlenmiş", "Silah" };
        static Sprite roundedSprite;
        static Texture2D geographyTexture;
        static Sprite arrowSprite;
        public bool IsOpen { get { return panel != null && panel.gameObject.activeSelf; } }

        void Awake() { Instance = this; }
        void Start()
        {
            Build();
            GameMenu = gameObject.AddComponent<MenuUI>();
            GameMenu.inGame = true;
            GameController.Instance.Changed += Refresh;
            Refresh();
        }
        void OnDestroy()
        {
            if (GameController.Instance != null) GameController.Instance.Changed -= Refresh;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (toastBack != null && toastBack.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastBack.gameObject.SetActive(false);
            if (miniMap == null || WorldRuntime.Instance == null) return;
            var world = WorldRuntime.Instance;
            Vector3 player = world.PlayerPosition;
            if (miniGeography != null)
            {
                float patch = 880f / (WorldGeography.HalfSize * 2f);
                miniGeography.uvRect = new Rect(.5f + player.x / (WorldGeography.HalfSize * 2f) - patch * .5f,
                    .5f + player.z / (WorldGeography.HalfSize * 2f) - patch * .5f, patch, patch);
            }
            for (int i = 0; i < miniSettlements.Length; i++)
            {
                Vector3 delta = WorldRuntime.SettlementPosition(i % 3, i / 3) - player;
                miniSettlements[i].gameObject.SetActive(Mathf.Abs(delta.x) < 440 && Mathf.Abs(delta.z) < 440);
                miniSettlements[i].anchoredPosition = new Vector2(delta.x * .20f, delta.z * .20f);
            }
            for (int i = 0; i < miniRoads.Count; i++)
            {
                var route = miniRoadRoutes[i];
                Vector3 from = WorldRuntime.SettlementPosition(route.from % 3, route.from / 3) - player;
                Vector3 to = WorldRuntime.SettlementPosition(route.to % 3, route.to / 3) - player;
                PositionLine(miniRoads[i], new Vector2(from.x, from.z) * .20f, new Vector2(to.x, to.z) * .20f, 3);
            }
            Vector3 wagon = world.WagonPosition - player;
            miniWagon.anchoredPosition = new Vector2(Mathf.Clamp(wagon.x * .20f, -70, 70), Mathf.Clamp(wagon.z * .20f, -70, 70));
            miniPlayer.localRotation = Quaternion.Euler(0, 0, -world.PlayerHeading);
        }

        void Build()
        {
            var root = new GameObject("Game UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var eventSystem = new GameObject("Event System", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
            var statusBack = Block(root.transform, "Company status", new Vector2(16, -16), new Vector2(398, 84), new Color(.07f, .10f, .11f, .86f));
            statusBack.anchorMin = statusBack.anchorMax = statusBack.pivot = new Vector2(0, 1);
            statusBack.GetComponent<Image>().raycastTarget = false;
            Block(statusBack, "Accent", new Vector2(-193, 0), new Vector2(5, 68), new Color(.72f, .55f, .26f)).GetComponent<Image>().raycastTarget = false;
            status = Label(statusBack, "Status", 19, new Vector2(10, 0), new Vector2(360, 68), TextAlignmentOptions.Left);
            miniMap = Block(root.transform, "Minimap panel", new Vector2(-16, -16), new Vector2(210, 232), new Color(.07f, .10f, .11f, .88f));
            miniMap.anchorMin = miniMap.anchorMax = miniMap.pivot = new Vector2(1, 1);
            Label(miniMap, "Map title", 16, new Vector2(0, 102), new Vector2(186, 22), TextAlignmentOptions.Center).text = "YAKIN ÇEVRE  ·  [M]";
            var miniField = Block(miniMap, "Local map", new Vector2(0, -9), new Vector2(184, 184), new Color(.25f, .33f, .29f));
            miniField.gameObject.AddComponent<RectMask2D>();
            miniGeography = TexturePanel(miniField, "Kıyı ve arazi", Vector2.zero, new Vector2(184, 184));
            foreach (var route in GameController.Instance.catalog.routes)
            {
                if (route.kind != RouteKind.Road) continue;
                miniRoadRoutes.Add(route);
                miniRoads.Add(Block(miniField, "Road", Vector2.zero, new Vector2(1, 3), new Color(.69f, .58f, .38f)));
                miniRoads[miniRoads.Count - 1].GetComponent<Image>().raycastTarget = false;
            }
            for (int i = 0; i < miniSettlements.Length; i++)
            {
                bool city = i % 3 == 0;
                miniSettlements[i] = Block(miniField, "Settlement " + i, Vector2.zero, new Vector2(city ? 11 : 7, city ? 11 : 7), city ? new Color(.91f, .73f, .37f) : new Color(.89f, .84f, .70f));
                miniSettlements[i].GetComponent<Image>().raycastTarget = false;
            }
            miniWagon = Block(miniField, "Wagon marker", Vector2.zero, new Vector2(9, 9), new Color(.76f, .45f, .20f));
            miniWagon.GetComponent<Image>().raycastTarget = false;
            miniPlayer = Block(miniField, "Player marker", Vector2.zero, new Vector2(13, 13), new Color(.20f, .72f, .80f));
            var miniArrow = miniPlayer.GetComponent<Image>();
            miniArrow.sprite = ArrowSprite(); miniArrow.type = Image.Type.Simple; miniArrow.raycastTarget = false;
            Label(miniMap, "North", 19, new Vector2(77, 75), new Vector2(26, 28), TextAlignmentOptions.Center).text = "K";
            toastBack = Block(root.transform, "Message", new Vector2(16, 16), new Vector2(420, 54), new Color(.08f, .11f, .11f, .88f));
            toastBack.anchorMin = toastBack.anchorMax = toastBack.pivot = Vector2.zero;
            toast = Label(toastBack, "Message text", 17, Vector2.zero, new Vector2(394, 44), TextAlignmentOptions.Center);
            toastBack.gameObject.SetActive(false);
            promptBack = Block(root.transform, "Interaction card", new Vector2(0, 88), new Vector2(360, 52), new Color(.10f, .15f, .15f, .94f));
            promptBack.anchorMin = promptBack.anchorMax = promptBack.pivot = new Vector2(.5f, 0);
            promptBack.GetComponent<Image>().raycastTarget = false;
            prompt = Label(promptBack, "Prompt", 18, Vector2.zero, new Vector2(340, 44), TextAlignmentOptions.Center);
            promptBack.gameObject.SetActive(false);
            wagonButton = Block(root.transform, "Wagon boarding button", new Vector2(0, 26), new Vector2(245, 50), new Color(.50f, .34f, .18f, .96f));
            wagonButton.anchorMin = wagonButton.anchorMax = wagonButton.pivot = new Vector2(.5f, 0);
            wagonButton.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (WorldRuntime.Instance != null) WorldRuntime.Instance.ToggleRiding(); });
            wagonLabel = Label(wagonButton, "Board label", 18, Vector2.zero, new Vector2(225, 43), TextAlignmentOptions.Center);
            wagonLabel.text = "[Q]  ARABAYA BİN";
            wagonButton.gameObject.SetActive(false);
            panel = Block(root.transform, "Menu", Vector2.zero, new Vector2(1120, 660), new Color(.08f, .11f, .12f, .98f));
            Block(panel, "Header rule", new Vector2(0, 261), new Vector2(1040, 2), new Color(.56f, .42f, .23f));
            title = Label(panel, "Title", 29, new Vector2(-315, 294), new Vector2(420, 48), TextAlignmentOptions.Left);
            content = Block(panel, "Content", new Vector2(0, -10), new Vector2(1060, 510), new Color(0, 0, 0, 0));
            Button(panel, "KAPAT  ×", new Vector2(483, 296), new Vector2(124, 40), Close, new Color(.34f, .24f, .21f));
            Button(panel, "DÜNYA HARİTASI  [M]", new Vector2(-393, -299), new Vector2(255, 39), () => Open("map"), new Color(.23f, .32f, .29f));
            Button(panel, "ŞİRKET DEFTERİ  [Tab]", new Vector2(-113, -299), new Vector2(255, 39), () => Open("ledger"), new Color(.23f, .32f, .29f));
            panel.gameObject.SetActive(false);
        }

        RectTransform Block(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = color.a > 0;
            if (size.x >= 18 && size.y >= 18) { image.sprite = RoundedSprite(); image.type = Image.Type.Sliced; }
            return rect;
        }

        static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "UI soft corners"; texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(0, 7 - Mathf.Min(x, 31 - x));
                float dy = Mathf.Max(0, 7 - Mathf.Min(y, 31 - y));
                float alpha = Mathf.Clamp01(8 - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            roundedSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
            return roundedSprite;
        }

        static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "Player direction arrow";
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float halfWidth = y < 10 ? 5f : Mathf.Lerp(11f, 0f, (y - 10f) / 21f);
                texture.SetPixel(x, y, Mathf.Abs(x - 15.5f) <= halfWidth && y >= 3 ? Color.white : Color.clear);
            }
            texture.Apply();
            arrowSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100f);
            return arrowSprite;
        }

        TextMeshProUGUI Label(Transform parent, string name, int fontSize, Vector2 position, Vector2 size, TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = obj.GetComponent<TextMeshProUGUI>();
            label.fontSize = fontSize; label.color = new Color(.96f, .92f, .81f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = alignment; label.raycastTarget = false;
            label.fontStyle = FontStyles.Normal;
            return label;
        }

        void Button(Transform parent, string text, Vector2 position, Vector2 size, Action click, Color color)
        {
            var rect = Block(parent, text, position, size, color);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => click());
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.16f, 1.12f, 1.04f);
            colors.pressedColor = new Color(.78f, .77f, .73f); colors.selectedColor = Color.white;
            button.colors = colors;
            var label = Label(rect, "Text", 18, Vector2.zero, size - new Vector2(16, 0), TextAlignmentOptions.Center);
            label.text = text;
        }

        void ClearContent()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            rowIndex = 0;
        }
        void Row(string text, Action click = null, Color? color = null)
        {
            float y = 212 - rowIndex * 53;
            rowIndex++;
            var tint = color ?? (click == null ? new Color(.16f, .20f, .21f) : new Color(.22f, .32f, .28f));
            if (click == null)
            {
                var background = Block(content, "Info", new Vector2(0, y), new Vector2(970, 46), tint);
                Block(background, "Accent", new Vector2(-476, 0), new Vector2(4, 34), new Color(.68f, .52f, .28f)).GetComponent<Image>().raycastTarget = false;
                Label(background, "Text", 18, new Vector2(8, 0), new Vector2(920, 44), TextAlignmentOptions.Left).text = text;
            }
            else
            {
                var background = Block(content, "Action", new Vector2(0, y), new Vector2(970, 46), new Color(.17f, .23f, .22f));
                Block(background, "Accent", new Vector2(-476, 0), new Vector2(4, 34), tint).GetComponent<Image>().raycastTarget = false;
                Label(background, "Text", 18, new Vector2(-62, 0), new Vector2(780, 42), TextAlignmentOptions.Left).text = text;
                Button(background, "SEÇ  →", new Vector2(420, 0), new Vector2(105, 36), click, tint);
            }
        }

        public void SetPrompt(string value)
        {
            if (prompt == null || IsOpen) return;
            prompt.text = value;
            promptBack.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }
        public void SetWagonButton(bool visible, bool mounted)
        {
            if (wagonButton == null) return;
            wagonButton.gameObject.SetActive(visible && !IsOpen);
            if (visible) wagonLabel.text = mounted ? "[Q]  ARABADAN İN" : "[Q]  ARABAYA BİN";
        }
        public void Open(string newPage)
        {
            page = newPage;
            if (newPage == "map" && GameController.Instance != null) selectedRegion = GameController.Instance.CurrentPlace.region;
            panel.gameObject.SetActive(true);
            if (wagonButton != null) wagonButton.gameObject.SetActive(false);
            if (promptBack != null) promptBack.gameObject.SetActive(false);
            if (miniMap != null) miniMap.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Refresh();
        }
        public void Close()
        {
            page = "";
            panel.gameObject.SetActive(false);
            if (miniMap != null) miniMap.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        public void Refresh()
        {
            var game = GameController.Instance;
            if (status == null || game == null || game.catalog == null) return;
            var place = game.CurrentPlace;
            string country = place == null || game.catalog.countries.Count <= place.country ? "" : game.catalog.countries[place.country].name;
            status.text = $"{place?.displayName}\n{country}   ·   Gün {game.state.day}   ·   £{game.state.money}   ·   {Economy.CargoWeight(game.catalog, game.state)}/{Economy.Capacity(game.state)} yük";
            if (game.LastMessage != lastMessage && !string.IsNullOrEmpty(game.LastMessage))
            {
                lastMessage = game.LastMessage;
                toast.text = lastMessage;
                toastUntil = Time.unscaledTime + 4f;
                toastBack.gameObject.SetActive(true);
            }
            if (!IsOpen) return;
            ClearContent();
            title.text = page == "market" ? "PAZAR" : page == "routes" ? "ULAŞIM" : page == "sea" ? "GEMİ SEFERLERİ" : page == "coach" ? "ARABACI VE KONVOY" : page == "license" ? "KAYNAK İŞLETMESİ" : page == "map" ? "DÜNYA HARİTASI" : "ŞİRKET DEFTERİ";
            if (page == "market") Market(game);
            else if (page == "routes" || page == "sea") Routes(game);
            else if (page == "coach") Coach(game);
            else if (page == "license") License(game);
            else if (page == "map") Map(game);
            else Ledger(game);
        }

        void Market(GameController game)
        {
            var filtered = new List<GoodDef>();
            foreach (var good in game.catalog.goods)
                if (marketCategory == 0 || marketCategory == 1 && good.extractable || marketCategory == 2 && good.kind == GoodKind.Food || marketCategory == 3 && good.kind == GoodKind.Manufactured || marketCategory == 4 && good.kind == GoodKind.Arms)
                    filtered.Add(good);
            if (filtered.Count == 0) return;
            var selected = filtered.Find(x => x.id == selectedGood);
            if (selected == null) { selected = filtered[0]; selectedGood = selected.id; }
            for (int i = 0; i < categories.Length; i++)
            {
                int category = i;
                Button(content, categories[i], new Vector2(-414 + i * 206, 218), new Vector2(194, 43), () => { marketCategory = category; Refresh(); }, i == marketCategory ? new Color(.57f, .44f, .24f) : new Color(.24f, .29f, .28f));
            }
            for (int i = 0; i < filtered.Count; i++)
            {
                var good = filtered[i];
                int col = i % 3, row = i / 3;
                var card = Block(content, good.id, new Vector2(-392 + col * 249, 130 - row * 105), new Vector2(235, 92), good.id == selectedGood ? new Color(.45f, .39f, .25f) : new Color(.20f, .26f, .27f));
                card.gameObject.AddComponent<Button>().onClick.AddListener(() => { selectedGood = good.id; Refresh(); });
                var icon = Block(card, "Icon", new Vector2(-79, 0), new Vector2(65, 65), new Color(0, 0, 0, 0));
                var image = icon.GetComponent<Image>(); image.sprite = GoodIcon.Get(good.id); image.color = Color.white; image.raycastTarget = false;
                var label = Label(card, "Good", 18, new Vector2(33, 0), new Vector2(145, 82), TextAlignmentOptions.Center);
                label.text = $"{good.displayName}\n£{Economy.Price(game.catalog, game.CurrentMarket, good)}  ·  Stok {Economy.Get(game.CurrentMarket.stock, good.id)}";
            }
            var detail = Block(content, "Selected good", new Vector2(390, -8), new Vector2(265, 450), new Color(.15f, .19f, .20f));
            var bigIcon = Block(detail, "Large icon", new Vector2(0, 170), new Vector2(75, 75), Color.clear);
            var bigImage = bigIcon.GetComponent<Image>(); bigImage.sprite = GoodIcon.Get(selected.id); bigImage.color = Color.white; bigImage.raycastTarget = false;
            Label(detail, "Name", 24, new Vector2(0, 114), new Vector2(245, 40), TextAlignmentOptions.Center).text = selected.displayName;
            int price = Economy.Price(game.catalog, game.CurrentMarket, selected);
            int owned = Economy.Get(game.state.cargo, selected.id);
            Label(detail, "Price", 20, new Vector2(0, 72), new Vector2(240, 45), TextAlignmentOptions.Center).text = $"Birim fiyat £{price}\nPazar {Economy.Get(game.CurrentMarket.stock, selected.id)}  ·  Sende {owned}";
            string held = "YÜKÜMDEKİLER\n";
            int shown = 0;
            foreach (var good in game.catalog.goods)
            {
                int quantity = Economy.Get(game.state.cargo, good.id);
                if (quantity > 0 && shown++ < 3) held += good.displayName + ": " + quantity + "   ";
            }
            if (shown == 0) held += "Yük yok";
            Label(detail, "Owned", 17, new Vector2(0, -3), new Vector2(245, 85), TextAlignmentOptions.Center).text = held;
            Button(detail, "−", new Vector2(-83, -80), new Vector2(47, 43), () => { amount = Mathf.Max(1, amount - 1); Refresh(); }, new Color(.28f, .32f, .31f));
            Label(detail, "Amount", 22, new Vector2(0, -80), new Vector2(90, 42), TextAlignmentOptions.Center).text = "Adet " + amount;
            Button(detail, "+", new Vector2(83, -80), new Vector2(47, 43), () => { amount = Mathf.Min(99, amount + 1); Refresh(); }, new Color(.28f, .32f, .31f));
            Button(detail, discount ? "İndirim: açık" : "İndirim: kapalı", new Vector2(0, -128), new Vector2(218, 37), () => { discount = !discount; Refresh(); }, discount ? new Color(.42f, .37f, .22f) : new Color(.27f, .29f, .29f));
            int sale = Mathf.Max(1, Mathf.RoundToInt(price * (discount ? .60f : .85f)));
            Button(detail, "AL  £" + price * amount, new Vector2(-59, -183), new Vector2(108, 53), () => { game.Trade(selectedGood, amount, true); Refresh(); }, new Color(.19f, .43f, .31f));
            Button(detail, "SAT  £" + sale * amount, new Vector2(59, -183), new Vector2(108, 53), () => { game.Trade(selectedGood, amount, false, sale); Refresh(); }, new Color(.46f, .32f, .22f));
        }

        void Routes(GameController game)
        {
            Row($"{game.CurrentPlace.displayName}   ·   Taşınan yük {Economy.CargoWeight(game.catalog, game.state)}");
            foreach (var route in game.catalog.RoutesFrom(game.state.settlement))
            {
                if ((page == "sea") != (route.kind == RouteKind.Sea)) continue;
                var chosen = route;
                var target = game.catalog.Settlement(game.catalog.OtherEnd(route, game.state.settlement));
                string mode = route.kind == RouteKind.Road ? "Karayolu" : route.kind == RouteKind.Rail ? "Tren" : "Gemi";
                Row($"{mode} → {target.displayName}    {route.days} gün   £{route.fare}+yük   kapasite {route.capacity}", () => { string currentPage = page; Close(); if (!game.Travel(chosen)) Open(currentPage); });
            }
            Row(page == "sea" ? "Gemi seferleri liman görevlisinden alınır." : "Tüm şehirleri görmek için M tuşuyla dünya haritasını açın.");
        }

        void Coach(GameController game)
        {
            Row($"Sahip olunan: {game.state.wagons} araba   ·   Konvoya bağlı: {game.state.attachedWagons}   ·   Kapasite: {Economy.Capacity(game.state)}");
            Row($"At arabası satın al   £{180 + 60 * Mathf.Max(0, game.state.wagons - 1)}", game.BuyWagon);
            if (game.state.attachedWagons < game.state.wagons) Row("Arabadaki boş aracı konvoyun arkasına bağla", game.AttachWagon);
            if (game.state.attachedWagons > 1) Row("Son aracı konvoydan ayır", game.DetachWagon);
            Row("Ana arabanın yanına gidip Q ile binin. W/S sürer, A/D yön verir.");
        }

        void License(GameController game)
        {
            var license = game.state.licenses.Find(x => x.settlement == game.state.settlement);
            var good = game.catalog.Good(game.CurrentPlace.specialty);
            Row($"{game.CurrentPlace.displayName}   ·   Kaynak: {good.displayName}");
            if (license == null) Row($"İşletme ruhsatı al   £{(game.CurrentPlace.city ? 300 : 180)}", game.BuyLicense);
            else Row($"Birikmiş ürün: {license.pending}   ·   Konvoya yükle", game.CollectProduction);
            Row("Ruhsatlı nokta günde 4 birim hammadde üretir.");
        }

        void Map(GameController game)
        {
            var map = Block(content, "Open world map", new Vector2(-265, -6), new Vector2(470, 470), new Color(.24f, .31f, .28f));
            map.GetComponent<Image>().raycastTarget = false;
            TexturePanel(map, "Coğrafya", Vector2.zero, new Vector2(460, 460));
            var cityList = Block(content, "City list", new Vector2(105, -6), new Vector2(270, 470), new Color(.15f, .20f, .20f));
            Label(cityList, "List heading", 17, new Vector2(0, 211), new Vector2(250, 35), TextAlignmentOptions.Center).text = "ŞEHİRLER";
            foreach (var route in game.catalog.routes)
            {
                Color color = route.kind == RouteKind.Sea ? new Color(.40f,.83f,.93f) :
                    route.kind == RouteKind.Rail ? new Color(.34f,.35f,.33f) : new Color(.88f,.75f,.48f);
                if (route.kind == RouteKind.Sea && route.from == 6 && route.to == 18)
                {
                    Vector2 north = new Vector2(1900f * .0592f, 1560f * .0592f);
                    Vector2 south = new Vector2(1900f * .0592f, -1400f * .0592f);
                    MapLine(map, MapPosition(6), north, color, 1.5f);
                    MapLine(map, north, south, color, 1.5f);
                    MapLine(map, south, MapPosition(18), color, 1.5f);
                }
                else MapLine(map, MapPosition(route.from), MapPosition(route.to), color, route.kind == RouteKind.Road ? 2.5f : 1.5f);
            }
            for (int i = 0; i < game.catalog.settlements.Count; i++)
            {
                var place = game.catalog.settlements[i];
                Vector2 position = MapPosition(i);
                if (!place.city)
                {
                    var village = Block(map, "Village " + i, position, new Vector2(9, 9), new Color(.95f, .87f, .65f));
                    village.GetComponent<Image>().raycastTarget = false;
                    continue;
                }
                int chosen = place.region;
                var marker = Block(map, "City " + i, position, new Vector2(17, 17),
                    chosen == selectedRegion ? new Color(.65f, .48f, .25f) : game.catalog.countries[place.country].primary * 1.5f);
                marker.gameObject.AddComponent<Button>().onClick.AddListener(() => { selectedRegion = chosen; Refresh(); });
                var listEntry = Block(cityList, "City selection", new Vector2(0, 168 - chosen * 47), new Vector2(246, 41),
                    chosen == selectedRegion ? new Color(.48f,.35f,.23f) : new Color(.22f,.29f,.28f));
                listEntry.gameObject.AddComponent<Button>().onClick.AddListener(() => { selectedRegion = chosen; Refresh(); });
                Label(listEntry, "City name", 14, Vector2.zero, new Vector2(240, 36), TextAlignmentOptions.Center).text = place.displayName.Split('·')[0].Trim();
            }
            Vector3 player = WorldRuntime.Instance.PlayerPosition;
            var playerMarker = Block(map, "You are here", new Vector2(player.x * .0592f, player.z * .0592f), new Vector2(15, 15), new Color(.20f, .85f, .98f));
            var playerImage = playerMarker.GetComponent<Image>(); playerImage.sprite = ArrowSprite(); playerImage.type = Image.Type.Simple; playerImage.raycastTarget = false;
            playerMarker.localRotation = Quaternion.Euler(0, 0, -WorldRuntime.Instance.PlayerHeading);
            Label(map, "North", 19, new Vector2(208, 205), new Vector2(25, 25), TextAlignmentOptions.Center).text = "K";
            var selected = game.catalog.Settlement(selectedRegion * 3);
            var nation = game.catalog.countries[selected.country];
            var details = Block(content, "Region details", new Vector2(380, -6), new Vector2(252, 470), new Color(.15f, .20f, .20f));
            Block(details, "Country stripe", new Vector2(-118, 0), new Vector2(7, 440), nation.primary).GetComponent<Image>().raycastTarget = false;
            Label(details, "Region heading", 22, new Vector2(0, 166), new Vector2(225, 77), TextAlignmentOptions.Center).text = selected.displayName;
            Label(details, "Country", 17, new Vector2(0, 108), new Vector2(225, 45), TextAlignmentOptions.Center).text = nation.name;
            Label(details, "Locations", 16, new Vector2(0, 16), new Vector2(220, 125), TextAlignmentOptions.Center).text = "KÖYLER\n" + game.catalog.Settlement(selectedRegion * 3 + 1).displayName + "\n" + game.catalog.Settlement(selectedRegion * 3 + 2).displayName;
            Label(details, "Resource", 16, new Vector2(0, -84), new Vector2(220, 68), TextAlignmentOptions.Center).text = "Kaynak: " + game.catalog.Good(selected.specialty).displayName + (selectedRegion == game.CurrentPlace.region ? "\nBu bölgedesin" : "");
            Label(details, "Map note", 15, new Vector2(0, -169), new Vector2(220, 84), TextAlignmentOptions.Center).text = "Altın: yol  ·  Koyu: tren\nMavi: gemi  ·  Turkuaz: konumun";
        }

        static Vector2 MapPosition(int settlement)
        {
            Vector3 world = WorldRuntime.SettlementPosition(settlement % 3, settlement / 3);
            return new Vector2(world.x * .0592f, world.z * .0592f);
        }

        static RawImage TexturePanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.GetComponent<RawImage>();
            image.texture = GeographyTexture(); image.raycastTarget = false;
            return image;
        }

        static Texture2D GeographyTexture()
        {
            if (geographyTexture != null) return geographyTexture;
            const int size = 512;
            geographyTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            geographyTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            var owners = new byte[size * size];
            var countries = GameController.Instance.catalog.countries;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float wx = (x / (float)(size - 1) * 2f - 1f) * WorldGeography.HalfSize;
                    float wz = (y / (float)(size - 1) * 2f - 1f) * WorldGeography.HalfSize;
                    float land = WorldGeography.LandDistance(wx, wz);
                    if (land <= 0) { pixels[y * size + x] = new Color(.12f,.31f,.40f); owners[y * size + x] = 255; continue; }
                    int country = 0;
                    if (Vector2.Distance(new Vector2(wx,wz), WorldGeography.Places[7]) < 330f) country = 1;
                    else if (Vector2.Distance(new Vector2(wx,wz), WorldGeography.Places[19]) < 330f) country = 3;
                    else
                    {
                        float best = float.MaxValue;
                        for (int i = 0; i < 8; i++)
                        {
                            Vector2 city = WorldGeography.Places[i * 3];
                            float distance = (city - new Vector2(wx,wz)).sqrMagnitude
                                + 160000f * Mathf.PerlinNoise(wx * .0012f + i * 17f, wz * .0012f + i * 7f);
                            if (distance < best) { best = distance; country = i / 2; }
                        }
                    }
                    Color ground = land < 100 ? new Color(.68f,.61f,.43f) :
                        wz > 1700 ? new Color(.37f,.42f,.36f) :
                        wx < -1800 && wz < -1000 ? new Color(.27f,.39f,.29f) : new Color(.40f,.48f,.34f);
                    pixels[y * size + x] = Color.Lerp(ground, countries[country].primary, .25f);
                    owners[y * size + x] = (byte)country;
                }
            for (int y = 1; y < size - 1; y++)
                for (int x = 1; x < size - 1; x++)
                {
                    int index = y * size + x;
                    if (owners[index] != 255 && (owners[index] != owners[index + 1] || owners[index] != owners[index + size]))
                        pixels[index] *= .65f;
                }
            geographyTexture.SetPixels(pixels); geographyTexture.Apply();
            return geographyTexture;
        }

        void MapLine(Transform parent, Vector2 a, Vector2 b, Color color, float thickness)
        {
            var line = Block(parent, "Road connection", Vector2.zero, Vector2.one, color);
            PositionLine(line, a, b, thickness);
            line.GetComponent<Image>().raycastTarget = false;
        }

        static void PositionLine(RectTransform line, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 difference = b - a;
            line.anchoredPosition = (a + b) * .5f;
            line.sizeDelta = new Vector2(difference.magnitude, thickness);
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
        }

        void Ledger(GameController game)
        {
            Row($"Gün {game.state.day}   ·   Kasa £{game.state.money}   ·   Konvoy {game.state.attachedWagons}/{game.state.wagons}");
            Row($"{game.CurrentPlace.displayName}   ·   Kriz: {game.CurrentMarket.crisis} {game.CurrentMarket.severity}   ·   İlişki: {game.CurrentMarket.relationship}");
            int shown = 0;
            foreach (var good in game.catalog.goods)
            {
                int count = Economy.Get(game.state.cargo, good.id);
                if (count > 0 && shown++ < 4) Row($"Yük: {good.displayName}  × {count}");
            }
            Row("Bir gün dinlen", game.Rest);
            Row("Yeni kayıt oluştur", () => { game.CreateManualSave(); });
            Row("Dünya haritasını aç", () => Open("map"));
        }
    }
}
