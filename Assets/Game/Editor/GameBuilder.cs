using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VictorianAristocrat;

public static class GameBuilder
{
    const string Root = "Assets/Game";
    const string CatalogPath = Root + "/Resources/GameCatalog.asset";
    static double importDeadline;

    public static void ImportTMP()
    {
        if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
        {
            Debug.Log("[Victorian Aristocrat] TMP Essentials already present.");
            EditorApplication.Exit(0);
            return;
        }
        importDeadline = EditorApplication.timeSinceStartup + 180;
        TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        EditorApplication.update += PollTMP;
    }

    static void PollTMP()
    {
        if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
        {
            EditorApplication.update -= PollTMP;
            AssetDatabase.Refresh();
            Debug.Log("[Victorian Aristocrat] TMP Essentials imported.");
            EditorApplication.Exit(0);
        }
        else if (EditorApplication.timeSinceStartup > importDeadline)
        {
            EditorApplication.update -= PollTMP;
            Debug.LogError("[Victorian Aristocrat] TMP Essentials import timed out.");
            EditorApplication.Exit(2);
        }
    }

    [MenuItem("Victorian Aristocrat/Build Game World")]
    public static void Build()
    {
        if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            Debug.LogWarning("TMP Essentials missing. Run GameBuilder.ImportTMP before building.");
        Directory.CreateDirectory(Root + "/Resources");
        Directory.CreateDirectory(Root + "/Scenes");
        var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (File.Exists(CatalogPath)) AssetDatabase.DeleteAsset(CatalogPath);
            catalog = ScriptableObject.CreateInstance<GameCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.goods = Goods();
        catalog.countries = Countries();
        catalog.settlements = Settlements();
        catalog.routes = Routes(catalog.settlements);
        EditorUtility.SetDirty(catalog);
        const string materialPath = Root + "/Resources/WorldMaterial.mat";
        var worldMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (worldMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.Exception("URP Lit shader missing in Editor.");
            worldMaterial = new Material(shader);
            AssetDatabase.CreateAsset(worldMaterial, materialPath);
        }
        const string terrainMaterialPath = Root + "/Resources/TerrainMaterial.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(terrainMaterialPath) == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) throw new System.Exception("URP Terrain/Lit shader missing in Editor.");
            AssetDatabase.CreateAsset(new Material(shader), terrainMaterialPath);
        }
        var scenes = new List<EditorBuildSettingsScene>();
        var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var menuController = new GameObject("Game Controller");
        menuController.AddComponent<GameController>().catalog = catalog;
        new GameObject("Main Menu").AddComponent<MenuUI>();
        const string menuPath = Root + "/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(menuScene, menuPath);
        scenes.Add(new EditorBuildSettingsScene(menuPath, true));
        for (int region = 0; region < 8; region++)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Game Controller");
            bootstrap.AddComponent<GameController>().catalog = catalog;
            var world = new GameObject("Region World");
            world.AddComponent<WorldRuntime>().region = region;
            string path = Root + "/Scenes/Region_" + region + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Victorian Aristocrat] Main menu, 4 countries, 8 cities, 16 villages, 12 goods, " + catalog.routes.Count + " routes, 9 scenes generated.");
    }

    [MenuItem("Victorian Aristocrat/Validate Game World")]
    public static void Validate()
    {
        var c = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
        if (c == null || c.goods.Count != 12 || c.countries.Count != 4 || c.settlements.Count != 24 || EditorBuildSettings.scenes.Length != 9 || EditorBuildSettings.scenes[0].path != Root + "/Scenes/MainMenu.unity")
            throw new System.Exception($"Game world incomplete: catalog={c != null}, goods={c?.goods.Count}, settlements={c?.settlements.Count}, scenes={EditorBuildSettings.scenes.Length}.");
        int cityCount = 0;
        foreach (var def in c.settlements)
        {
            if (def.city) cityCount++;
            if (def.country < 0 || def.country >= 4) throw new System.Exception("Invalid country assignment.");
            if (c.Good(def.specialty)?.extractable != true) throw new System.Exception("Resource site uses a manufactured good.");
        }
        if (cityCount != 8) throw new System.Exception("World must contain eight cities.");
        int islandCount = 0;
        foreach (var def in c.settlements)
        {
            if (def.island) islandCount++;
            if (def.position != WorldGeography.Places[def.id] || !WorldGeography.IsLand(def.position.x, def.position.y, 12f))
                throw new System.Exception("Settlement is outside its intended land: " + def.displayName);
            bool expectedPort = def.id == 6 || def.id == 7 || def.id == 18 || def.id == 19;
            if (def.port != expectedPort) throw new System.Exception("Incorrect port: " + def.displayName);
            if (def.port && !def.island && WorldGeography.CoastX(def.position.y) - def.position.x > 550f)
                throw new System.Exception("Inland port: " + def.displayName);
        }
        if (islandCount != 2 || !c.Settlement(7).island || !c.Settlement(19).island || c.Settlement(7).country == c.Settlement(19).country)
            throw new System.Exception("Two villages from different countries must be islands.");
        foreach (var route in c.routes)
        {
            var a = c.Settlement(route.from); var b = c.Settlement(route.to);
            if (route.kind == RouteKind.Sea)
            {
                if (!a.port || !b.port) throw new System.Exception("Sea route without two ports.");
            }
            else
            {
                if (a.island || b.island) throw new System.Exception("Land route reaches an island.");
                for (int step = 0; step <= 100; step++)
                {
                    Vector2 point = Vector2.Lerp(a.position, b.position, step / 100f);
                    if (!WorldGeography.IsLand(point.x, point.y, 10f)) throw new System.Exception("Land route crosses water.");
                }
            }
        }
        int sourceCount = 0;
        foreach (var good in c.goods)
        {
            if (!good.extractable) continue;
            sourceCount++;
            if (!c.settlements.Exists(x => x.specialty == good.id)) throw new System.Exception("Missing resource site for " + good.id);
        }
        if (sourceCount != 7) throw new System.Exception("Expected seven extractable resources.");
        foreach (var def in c.settlements)
        {
            bool linked = false;
            foreach (var route in c.RoutesFrom(def.id)) linked = true;
            if (!linked) throw new System.Exception("Unreachable settlement: " + def.displayName);
        }
        var visited = new HashSet<int> { 0 };
        var queue = new Queue<int>(); queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (var route in c.RoutesFrom(current))
            {
                int next = c.OtherEnd(route, current);
                if (visited.Add(next)) queue.Enqueue(next);
            }
        }
        if (visited.Count != 24) throw new System.Exception("Route network does not reach all settlements.");
        var state = new GameState();
        foreach (var def in c.settlements)
        {
            var market = new SettlementState { id = def.id, crisis = def.initialCrisis, severity = def.crisisSeverity };
            foreach (var good in c.goods) market.stock.Add(new StockEntry { good = good.id, amount = 20 });
            state.settlements.Add(market);
        }
        var war = state.settlements.Find(x => x.crisis == CrisisKind.War);
        var famine = state.settlements.Find(x => x.crisis == CrisisKind.Famine);
        if (war == null || famine == null) throw new System.Exception("Crisis fixtures missing.");
        state.settlement = war.id;
        Economy.Add(state.cargo, "arms", 20);
        int before = war.severity;
        int reference = Economy.Price(c, war, c.Good("arms"));
        if (!Economy.Trade(c, state, "arms", 10, false, reference / 2, out _) || war.severity >= before || war.relationship <= 0)
            throw new System.Exception("War trade failed.");
        state.settlement = famine.id;
        Economy.Add(state.cargo, "food", 20);
        before = famine.severity;
        if (!Economy.Trade(c, state, "food", 10, false, 0, out _) || famine.severity >= before)
            throw new System.Exception("Famine trade failed.");
        Economy.AdvanceDay(c, state);
        if (state.day != 2) throw new System.Exception("Economy day failed.");
        state.settlement = 0;
        state.money = 10000;
        state.cargo.Clear();
        var firstMarket = state.settlements.Find(x => x.id == 0);
        int grainBefore = Economy.Get(firstMarket.stock, "grain");
        int cost = Economy.Price(c, firstMarket, c.Good("grain")) * 2;
        if (!Economy.Trade(c, state, "grain", 2, true, 0, out _) || state.money != 10000 - cost ||
            Economy.Get(state.cargo, "grain") != 2 || Economy.Get(firstMarket.stock, "grain") != grainBefore - 2)
            throw new System.Exception("Buying did not transfer money and stock correctly.");
        int moneyBefore = state.money;
        if (Economy.Trade(c, state, "grain", 1000, true, 0, out _) || state.money != moneyBefore)
            throw new System.Exception("Cargo capacity or stock check failed.");
        state.licenses.Add(new LicenseState { settlement = 0, good = "coal" });
        Economy.AdvanceDay(c, state);
        if (state.licenses[0].pending != 4) throw new System.Exception("Licensed production failed.");
        state.wagons = 3;
        state.attachedWagons = 1;
        if (Economy.Capacity(state) != 100) throw new System.Exception("Unattached wagons increased capacity.");
        state.attachedWagons = 3;
        if (Economy.Capacity(state) != 240) throw new System.Exception("Attached wagon capacity failed.");
        var serialized = JsonUtility.ToJson(state);
        var restored = JsonUtility.FromJson<GameState>(serialized);
        if (restored.day != state.day || restored.settlements.Count != 24 || restored.cargo.Count != state.cargo.Count)
            throw new System.Exception("Save roundtrip failed.");
        restored.version = 1;
        restored.hasWorldPosition = true;
        restored.worldPosition = new Vector3(2600, 0, 0);
        GameController.MigrateSave(restored);
        if (restored.version != 2 || restored.hasWorldPosition || restored.cargo.Count != state.cargo.Count || restored.money != state.money)
            throw new System.Exception("Old save migration failed.");
        Debug.Log("[Victorian Aristocrat] Validation passed: eight cities, four countries, resource sites, reachability, trade, crises, wagon capacity, save.");
    }

    [MenuItem("Victorian Aristocrat/Build Windows Game")]
    public static void BuildWindows() { BuildWindowsAt("Builds/Windows/Victorian Aristocrat.exe"); }

    [MenuItem("Victorian Aristocrat/Build Windows Game (New UI)")]
    public static void BuildWindowsNewUI() { BuildWindowsAt("Builds/Windows-NewUI/Victorian Aristocrat.exe"); }

    [MenuItem("Victorian Aristocrat/Build Windows Game (Detailed Models)")]
    public static void BuildWindowsDetailedModels() { BuildWindowsAt("Builds/Windows-Models/Victorian Aristocrat.exe"); }

    static void BuildWindowsAt(string path)
    {
        Validate();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, x => x.path),
            locationPathName = path,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Windows build failed: " + report.summary.result);
        Debug.Log("[Victorian Aristocrat] Windows build succeeded: " + report.summary.outputPath);
    }

    static List<GoodDef> Goods()
    {
        var list = new List<GoodDef>();
        AddGood(list, "wood", "Odun", GoodKind.Raw, 7, 2);
        AddGood(list, "grain", "Tahıl", GoodKind.Food, 8, 2);
        AddGood(list, "iron", "Demir cevheri", GoodKind.Raw, 13, 3);
        AddGood(list, "coal", "Kömür", GoodKind.Raw, 11, 3);
        AddGood(list, "sulfur", "Sülfür", GoodKind.Raw, 18, 2);
        AddGood(list, "cotton", "Pamuk", GoodKind.Raw, 9, 2);
        AddGood(list, "wool", "Yün", GoodKind.Raw, 10, 2);
        AddGood(list, "food", "İşlenmiş gıda", GoodKind.Food, 18, 1);
        AddGood(list, "cloth", "Kumaş", GoodKind.Manufactured, 22, 1);
        AddGood(list, "metal", "Metal", GoodKind.Manufactured, 28, 2);
        AddGood(list, "arms", "Silah", GoodKind.Arms, 65, 2);
        AddGood(list, "timber", "Kereste", GoodKind.Manufactured, 16, 2);
        for (int i = 0; i < 7; i++) list[i].extractable = true;
        return list;
    }

    static List<CountryDef> Countries()
    {
        return new List<CountryDef>
        {
            new CountryDef { name = "Alder Krallığı", primary = new Color(.12f,.25f,.48f), secondary = new Color(.94f,.75f,.35f) },
            new CountryDef { name = "Vesper Cumhuriyeti", primary = new Color(.60f,.15f,.18f), secondary = new Color(.96f,.88f,.72f) },
            new CountryDef { name = "Caldria Birliği", primary = new Color(.16f,.44f,.30f), secondary = new Color(.91f,.86f,.61f) },
            new CountryDef { name = "Meridia Federasyonu", primary = new Color(.40f,.23f,.52f), secondary = new Color(.92f,.68f,.26f) }
        };
    }
    static void AddGood(List<GoodDef> list, string id, string name, GoodKind kind, int price, int weight)
    { list.Add(new GoodDef { id = id, displayName = name, kind = kind, basePrice = price, weight = weight }); }

    static List<SettlementDef> Settlements()
    {
        string[] regions = { "Kuzey Vadisi", "Kömür Sırtı", "Kıyı Ovası", "Demir Dağları", "Pamuk Deltası", "Batı Ormanları", "Güney Limanı", "Doğu Yaylası" };
        string[] cities = { "Alderwick", "Blackford", "Westhaven", "Ironmere", "Cottonbridge", "Oakford", "Southport", "Eastmoor" };
        string[] resources = { "grain", "coal", "wood", "iron", "cotton", "wood", "sulfur", "wool" };
        string[] villageA = { "Millbrook", "Cinderfield", "Seabury", "Forgeham", "Reedbank", "Pineford", "Saltwick", "Heatherfield" };
        string[] villageB = { "Wheatford", "Ashdale", "Harborfell", "Orestead", "Loomstead", "Woodmere", "Cliffport", "Shepherdsby" };
        string[] extra = { "grain", "sulfur", "wool", "coal", "grain", "wood", "cotton", "iron" };
        var list = new List<SettlementDef>();
        for (int r = 0; r < 8; r++)
        {
            list.Add(new SettlementDef { id = r * 3, region = r, country = r / 2, displayName = cities[r] + " · " + regions[r], city = true, specialty = resources[r], station = true, port = r == 2 || r == 6, position = WorldGeography.Places[r * 3], initialCrisis = r == 3 ? CrisisKind.War : r == 4 ? CrisisKind.Famine : CrisisKind.None, crisisSeverity = r == 3 || r == 4 ? 4 : 0 });
            list.Add(new SettlementDef { id = r * 3 + 1, region = r, country = r / 2, displayName = villageA[r], specialty = resources[r], island = r == 2 || r == 6, port = r == 2 || r == 6, position = WorldGeography.Places[r * 3 + 1] });
            list.Add(new SettlementDef { id = r * 3 + 2, region = r, country = r / 2, displayName = villageB[r], specialty = extra[r], position = WorldGeography.Places[r * 3 + 2] });
        }
        return list;
    }

    static List<RouteDef> Routes(List<SettlementDef> places)
    {
        var list = new List<RouteDef>();
        for (int r = 0; r < 8; r++)
        {
            if (!places[r * 3 + 1].island) AddRoute(list, r * 3, r * 3 + 1, RouteKind.Road, 1, 5, 500);
            AddRoute(list, r * 3, r * 3 + 2, RouteKind.Road, 1, 5, 500);
        }
        int[,] links = { {0,1}, {1,3}, {3,2}, {2,6}, {6,7}, {7,5}, {5,4}, {4,0}, {0,3}, {4,7} };
        for (int i = 0; i < links.GetLength(0); i++)
        {
            int a = links[i, 0] * 3, b = links[i, 1] * 3;
            AddRoute(list, a, b, RouteKind.Road, 2, 20, 500);
            if (i < 8) AddRoute(list, a, b, RouteKind.Rail, 1, 35, 180);
        }
        AddRoute(list, 6, 7, RouteKind.Sea, 1, 20, 250);
        AddRoute(list, 18, 19, RouteKind.Sea, 1, 20, 250);
        AddRoute(list, 6, 18, RouteKind.Sea, 2, 48, 250);
        foreach (var route in list)
            if (route.kind == RouteKind.Sea && (!places[route.from].port || !places[route.to].port))
                throw new System.Exception("Sea route requires two ports.");
        return list;
    }
    static void AddRoute(List<RouteDef> list, int a, int b, RouteKind kind, int days, int fare, int capacity)
    { list.Add(new RouteDef { from = a, to = b, kind = kind, days = days, fare = fare, capacity = capacity }); }
}
