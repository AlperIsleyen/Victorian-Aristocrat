using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace VictorianAristocrat
{
    public class WorldRuntime : MonoBehaviour
    {
        public static WorldRuntime Instance { get; private set; }
        static readonly Dictionary<Color, Material> sharedMaterials = new Dictionary<Color, Material>();
        public int region;
        CharacterController controller;
        Camera view;
        Transform player;
        readonly List<Transform> convoy = new List<Transform>();
        readonly List<Vector3> trail = new List<Vector3>();
        readonly List<InteractionPoint> points = new List<InteractionPoint>();
        bool riding;
        float pitch;
        float headYaw;
        float clock;
        float navigationClock;
        float verticalSpeed;
        const float BoardingDistance = 7.5f;
        public const float WorldHalfSize = WorldGeography.HalfSize;
        public Vector3 PlayerPosition { get { return player == null ? Vector3.zero : player.position; } }
        public Vector3 WagonPosition { get { return convoy.Count == 0 ? Vector3.zero : convoy[0].position; } }
        public float PlayerHeading { get { return player == null ? 0 : player.eulerAngles.y; } }
        public static Vector3 SettlementPosition(int local, int region) { return WorldGeography.Position(region * 3 + local); }

        struct InteractionPoint { public Vector3 position; public string page; public string label; public int settlement; }

        void Awake() { Instance = this; }
        void Start()
        {
            if (GameController.Instance == null) return;
            MakeWorld();
            MakePlayer();
            gameObject.AddComponent<GameUI>();
            PlacePlayerAtCurrentSettlement();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void PlacePlayerAtCurrentSettlement()
        {
            if (player == null) return;
            riding = false;
            int settlement = GameController.Instance.state.settlement;
            Vector3 center = SettlementPosition(settlement % 3, settlement / 3);
            controller.enabled = false;
            player.position = center + new Vector3(0, 1.5f, -12);
            if (GameController.Instance.state.hasWorldPosition)
            {
                Vector3 saved = GameController.Instance.state.worldPosition;
                player.position = new Vector3(saved.x, WorldGeography.Height(saved.x, saved.z) + 1.5f, saved.z);
            }
            player.rotation = Quaternion.identity;
            controller.enabled = true;
            pitch = headYaw = 0;
            verticalSpeed = -2f;
            trail.Clear();
            for (int i = 0; i < convoy.Count; i++)
            {
                Vector3 wagonPlace = player.position + new Vector3(0, 0, -6 - i * 7);
                convoy[i].position = new Vector3(wagonPlace.x, WorldGeography.Height(wagonPlace.x, wagonPlace.z) + .8f, wagonPlace.z);
                convoy[i].rotation = Quaternion.identity;
            }
            for (int i = 80; i >= 0; i--) trail.Add(convoy[0].position - Vector3.forward * i * .5f);
        }

        void Update()
        {
            var game = GameController.Instance;
            var ui = GameUI.Instance;
            if (game == null || ui == null || player == null || Keyboard.current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (ui.IsOpen) ui.Close();
                else if (ui.GameMenu != null) ui.GameMenu.HandleEscape();
            }
            if (ui.GameMenu != null && ui.GameMenu.IsOpen) return;
            if (keyboard.tabKey.wasPressedThisFrame) ui.Open("ledger");
            if (keyboard.mKey.wasPressedThisFrame) ui.Open("map");
            if (ui.IsOpen) return;

            Vector2 look = Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
            pitch = Mathf.Clamp(pitch - look.y * .12f * GameSettings.Sensitivity, -75, 75);
            if (keyboard.qKey.wasPressedThisFrame) ToggleRiding(game);
            if (riding) Drive(keyboard, look.x);
            else Walk(keyboard, look.x);
            navigationClock += Time.deltaTime;
            if (navigationClock >= 2f)
            {
                navigationClock = 0;
                for (int i = 0; i < game.catalog.settlements.Count; i++)
                {
                    var place = game.catalog.settlements[i];
                    if ((SettlementPosition(place.id % 3, place.region) - player.position).sqrMagnitude < 70f * 70f)
                    {
                        game.EnterSettlement(place.id);
                        break;
                    }
                }
            }
            bool nearWagon = riding || Vector3.Distance(player.position, convoy[0].position) <= BoardingDistance;
            ui.SetWagonButton(nearWagon, riding);
            bool pointerFree = nearWagon && keyboard.leftAltKey.isPressed;
            Cursor.lockState = pointerFree ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = pointerFree;

            clock += Time.deltaTime;
            if (clock > 180f) { clock = 0; game.Rest(); }
            if (Time.frameCount % 600 == 0) game.SaveWorldPosition(player.position);
            InteractionPoint? nearest = null;
            float best = 5.5f;
            foreach (var point in points)
            {
                float distance = Vector3.Distance(player.position, point.position);
                if (distance < best) { best = distance; nearest = point; }
            }
            if (nearest.HasValue && !riding)
            {
                var point = nearest.Value;
                ui.SetPrompt("[E]  " + point.label);
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    game.EnterSettlement(point.settlement);
                    game.Say(game.CurrentPlace.displayName);
                    ui.Open(point.page);
                }
            }
            else ui.SetPrompt("");
        }

        void Walk(Keyboard keyboard, float yaw)
        {
            player.Rotate(Vector3.up, yaw * .12f * GameSettings.Sensitivity);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += Vector3.forward;
            if (keyboard.sKey.isPressed) move += Vector3.back;
            if (keyboard.aKey.isPressed) move += Vector3.left;
            if (keyboard.dKey.isPressed) move += Vector3.right;
            float speed = keyboard.leftShiftKey.isPressed ? 6f : 4f;
            if (controller.isGrounded)
            {
                if (verticalSpeed < 0) verticalSpeed = -2f;
                if (keyboard.spaceKey.wasPressedThisFrame) verticalSpeed = 6.4f;
            }
            verticalSpeed += -18f * Time.deltaTime;
            Vector3 motion = (player.TransformDirection(move.normalized) * speed + Vector3.up * verticalSpeed) * Time.deltaTime;
            Vector3 next = player.position + motion;
            if (!WorldGeography.IsLand(next.x, next.z, 3f)) motion.x = motion.z = 0;
            controller.Move(motion);
        }

        void Drive(Keyboard keyboard, float mouseYaw)
        {
            var lead = convoy[0];
            float steer = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            float throttle = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            lead.Rotate(Vector3.up, steer * (throttle < 0 ? -1 : 1) * 65f * Time.deltaTime);
            Vector3 next = lead.position + lead.forward * throttle * 10f * Time.deltaTime;
            if (WorldGeography.IsLand(next.x, next.z, 5f))
                lead.position = new Vector3(next.x, WorldGeography.Height(next.x, next.z) + .8f, next.z);
            headYaw = Mathf.Clamp(headYaw + mouseYaw * .12f * GameSettings.Sensitivity, -60, 60);
            player.position = lead.TransformPoint(new Vector3(0, 1.2f, 1.05f));
            player.rotation = lead.rotation;
            view.transform.localRotation = Quaternion.Euler(pitch, headYaw, 0);
            if (trail.Count == 0 || Vector3.Distance(trail[trail.Count - 1], lead.position) > .35f)
            {
                trail.Add(lead.position);
                if (trail.Count > 700) trail.RemoveRange(0, 100);
            }
            for (int i = 1; i < convoy.Count; i++)
            {
                Vector3 target = TrailPoint(i * 7f);
                target.y = WorldGeography.Height(target.x, target.z) + .8f;
                Vector3 direction = target - convoy[i].position;
                if (direction.sqrMagnitude > .01f)
                {
                    convoy[i].position = Vector3.MoveTowards(convoy[i].position, target, 9f * Time.deltaTime);
                    convoy[i].rotation = Quaternion.Slerp(convoy[i].rotation, Quaternion.LookRotation(direction.normalized), Time.deltaTime * 5);
                }
            }
        }

        Vector3 TrailPoint(float distance)
        {
            for (int i = trail.Count - 1; i > 0; i--)
            {
                float segment = Vector3.Distance(trail[i], trail[i - 1]);
                if (distance <= segment) return Vector3.Lerp(trail[i], trail[i - 1], segment <= 0 ? 0 : distance / segment);
                distance -= segment;
            }
            return trail.Count == 0 ? convoy[0].position - convoy[0].forward * distance : trail[0];
        }

        public void ToggleRiding() { if (GameController.Instance != null) ToggleRiding(GameController.Instance); }

        void ToggleRiding(GameController game)
        {
            if (riding)
            {
                riding = false;
                controller.enabled = false;
                player.position = convoy[0].position + convoy[0].right * 3.3f + Vector3.up * .7f;
                player.rotation = convoy[0].rotation;
                controller.enabled = true;
                verticalSpeed = -2f;
                headYaw = 0;
                game.Say("Arabadan indiniz.");
                return;
            }
            if (Vector3.Distance(player.position, convoy[0].position) > BoardingDistance) { game.Say("Binmek için ana arabaya yaklaşın."); return; }
            riding = true;
            controller.enabled = false;
            game.Say("At arabasına bindiniz. W/S sürer, A/D yön verir.");
        }

        static Vector3 Center(int local, int region) { return SettlementPosition(local, region); }
        Vector3 Center(int local) { return Center(local, region); }

        void MakeWorld()
        {
            var catalog = GameController.Instance.catalog;
            MakeTerrainAndSea();
            ScatterLandscape();
            for (int regionIndex = 0; regionIndex < 8; regionIndex++)
            {
            region = regionIndex;
            for (int local = 0; local < 3; local++)
            {
                var def = catalog.Settlement(region * 3 + local);
                if (def == null) continue;
                var nation = catalog.countries[def.country];
                Vector3 c = Center(local);
                int buildingCount = def.city ? 16 : 6;
                for (int i = 0; i < buildingCount; i++)
                {
                    float angle = i * 2.39996f + region * .37f;
                    float radius = 23f + (i / 4) * (def.city ? 10f : 7f);
                    Vector3 p = c + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    p.y = WorldGeography.Height(p.x, p.z);
                    House(p, 4 + (i + region) % 3, (i + region) % 3, nation);
                }
                for (int i = -2; i <= 2; i++) StreetLamp(c + new Vector3(i * 11, 0, -15));
                MarketStall(c + new Vector3(-15, 0, -7), "Pazar: " + def.displayName, def.id, nation.primary);
                if (!def.island) Post(c + new Vector3(0, 0, -7), "routes", "Ulaşım ofisi", def.id, new Color(.20f, .36f, .43f));
                Vector3 coach = c + new Vector3(43, 0, -8);
                Post(coach, "coach", "Arabacı", def.id, new Color(.42f, .25f, .15f));
                ParkedWagon(coach + new Vector3(0, .8f, 8));
                ResourceSite(c + new Vector3(28, 0, 30), def, nation);
                Flag(c + new Vector3(8, 0, 7), nation);
                if (def.port) Dock(c, def.island, def.id);
                for (int i = 0; i < 8; i++) Tree(c + new Vector3((i - 4) * 7, 0, -37 + i % 3 * 5), new Color(.17f, .29f + region * .01f, .20f));
            }
            if (!catalog.Settlement(region * 3 + 1).island) Road(Center(0), Center(1), 1);
            Road(Center(0), Center(2), -1);
            }
            foreach (var route in catalog.routes)
            {
                if (route.kind != RouteKind.Road || route.from % 3 != 0 || route.to % 3 != 0) continue;
                LongRoad(Center(0, route.from / 3), Center(0, route.to / 3));
            }
            region = GameController.Instance.CurrentPlace.region;
            var sunObject = new GameObject("Güneş"); sunObject.transform.SetParent(transform);
            sunObject.transform.rotation = Quaternion.Euler(50, -30, 0);
            var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.63f, .68f, .65f);
            MakeConvoy();
        }

        void MakeTerrainAndSea()
        {
            const int resolution = 1025;
            var data = new TerrainData { heightmapResolution = resolution, alphamapResolution = 256 };
            data.size = new Vector3(WorldHalfSize * 2, 100, WorldHalfSize * 2);
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float wx = -WorldHalfSize + x * data.size.x / (resolution - 1);
                    float wz = -WorldHalfSize + z * data.size.z / (resolution - 1);
                    heights[z, x] = WorldGeography.Height(wx, wz) / data.size.y;
                }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] {
                TerrainLayer(new Color(.29f,.39f,.24f)),
                TerrainLayer(new Color(.65f,.57f,.38f)),
                TerrainLayer(new Color(.36f,.38f,.34f))
            };
            var splat = new float[256, 256, 3];
            for (int z = 0; z < 256; z++)
                for (int x = 0; x < 256; x++)
                {
                    float wx = -WorldHalfSize + x * data.size.x / 255f;
                    float wz = -WorldHalfSize + z * data.size.z / 255f;
                    float shore = WorldGeography.LandDistance(wx, wz);
                    float sand = shore < 105 ? .92f : .06f;
                    float rock = shore >= 105 && WorldGeography.Height(wx, wz) > 47 ? .62f : .04f;
                    splat[z, x, 0] = 1f - sand - rock;
                    splat[z, x, 1] = sand;
                    splat[z, x, 2] = rock;
                }
            data.SetAlphamaps(0, 0, splat);
            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Doğal arazi";
            terrainObject.transform.SetParent(transform);
            terrainObject.transform.position = new Vector3(-WorldHalfSize, 0, -WorldHalfSize);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = Resources.Load<Material>("TerrainMaterial");
            terrain.drawInstanced = true;

            var vertices = new List<Vector3>(); var triangles = new List<int>();
            const float step = 50f;
            for (float z = -WorldHalfSize; z < WorldHalfSize; z += step)
                for (float x = -WorldHalfSize; x < WorldHalfSize; x += step)
                {
                    AddWaterTriangle(x, z, x + step, z, x, z + step, vertices, triangles);
                    AddWaterTriangle(x + step, z, x + step, z + step, x, z + step, vertices, triangles);
                }
            var water = new GameObject("Doğu denizi", typeof(MeshFilter), typeof(MeshRenderer));
            water.transform.SetParent(transform);
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            water.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Resources.Load<Material>("WorldMaterial"));
            material.color = new Color(.16f, .36f, .45f);
            material.SetFloat("_Smoothness", .83f);
            water.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static TerrainLayer TerrainLayer(Color color)
        {
            var texture = new Texture2D(2, 2);
            var pixels = new[] { color, color, color, color };
            texture.SetPixels(pixels); texture.Apply();
            return new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(24, 24) };
        }

        static void AddWaterTriangle(float ax, float az, float bx, float bz, float cx, float cz, List<Vector3> vertices, List<int> triangles)
        {
            if (WorldGeography.LandDistance((ax + bx + cx) / 3f, (az + bz + cz) / 3f) > 0) return;
            int index = vertices.Count;
            vertices.Add(new Vector3(ax, WorldGeography.SeaLevel, az));
            vertices.Add(new Vector3(bx, WorldGeography.SeaLevel, bz));
            vertices.Add(new Vector3(cx, WorldGeography.SeaLevel, cz));
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 1);
        }

        void ScatterLandscape()
        {
            var random = new System.Random(4861);
            for (int i = 0; i < 420; i++)
            {
                float x = (float)(random.NextDouble() * 7200 - 3600);
                float z = (float)(random.NextDouble() * 7200 - 3600);
                if (!WorldGeography.IsLand(x, z, 80f)) continue;
                bool nearTown = false;
                foreach (Vector2 p in WorldGeography.Places)
                    if (Vector2.Distance(new Vector2(x,z), p) < 180f) { nearTown = true; break; }
                if (nearTown) continue;
                Vector3 pos = new Vector3(x, WorldGeography.Height(x, z), z);
                if (i % 5 == 0)
                    Cube("Arazi kayası", pos + Vector3.up, new Vector3(2f, 2f, 2.5f), new Color(.40f,.41f,.38f)).transform.SetParent(transform);
                else Tree(pos, z > 1500 ? new Color(.20f,.32f,.25f) : new Color(.18f,.31f,.20f));
            }
        }

        void Dock(Vector3 settlement, bool island, int settlementId)
        {
            float direction = island ? -1f : 1f;
            float shore = settlement.x;
            for (int step = 1; step <= 120; step++)
            {
                float x = settlement.x + direction * step * 5f;
                if (!WorldGeography.IsLand(x, settlement.z)) { shore = x; break; }
            }
            Vector3 landEnd = new Vector3(shore - direction * 25f, 0, settlement.z);
            landEnd.y = WorldGeography.Height(landEnd.x, landEnd.z);
            LongRoad(settlement, landEnd);
            Post(landEnd + new Vector3(0, 0, -11f), "sea", "Liman görevlisi", settlementId, new Color(.16f, .39f, .48f));
            for (int i = 0; i < 5; i++)
            {
                float x = shore + direction * (i * 17f + 7f);
                var deck = Cube("Liman iskelesi", new Vector3(x, WorldGeography.SeaLevel + 1.2f, settlement.z),
                    new Vector3(19f, .35f, 9f), new Color(.39f,.25f,.14f));
                deck.transform.SetParent(transform);
                for (int side = -1; side <= 1; side += 2)
                    Cube("İskele direği", new Vector3(x, WorldGeography.SeaLevel - .5f, settlement.z + side * 3.5f),
                        new Vector3(.8f, 4f, .8f), new Color(.30f,.19f,.11f)).transform.SetParent(transform);
            }
            Ship(new Vector3(shore + direction * 90f, WorldGeography.SeaLevel, settlement.z + 9f));
        }

        void Ship(Vector3 position)
        {
            var ship = new GameObject("Limanda demirli yolcu gemisi").transform;
            ship.SetParent(transform);
            ship.position = position;
            Color hull = new Color(.22f, .20f, .19f);
            Color wood = new Color(.45f, .28f, .15f);
            Color trim = new Color(.74f, .63f, .41f);
            WagonPart(ship, "Gemi gövdesi", new Vector3(0, -.35f, 0), new Vector3(8f, 3f, 29f), hull);
            WagonPart(ship, "Üst güverte", new Vector3(0, 1.35f, 0), new Vector3(9f, .45f, 29f), wood);
            WagonPart(ship, "Baş bodoslama", new Vector3(0, .2f, 15.3f), new Vector3(5f, 2f, 4f), hull).localRotation = Quaternion.Euler(0, 45, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                WagonPart(ship, "Küpeşte", new Vector3(side * 4.25f, 2.1f, 0), new Vector3(.35f, 1.25f, 26f), trim);
                for (int i = -5; i <= 5; i++)
                    WagonPrimitive(ship, PrimitiveType.Cylinder, "Yuvarlak lumboz", new Vector3(side * 4.04f, .1f, i * 2.2f), new Vector3(.45f, .08f, .45f), trim).localRotation = Quaternion.Euler(0, 0, 90);
            }
            WagonPart(ship, "Kaptan köşkü", new Vector3(0, 3.2f, -7f), new Vector3(6.5f, 3.1f, 6f), new Color(.67f, .62f, .47f));
            WagonPart(ship, "Köşk çatısı", new Vector3(0, 4.9f, -7f), new Vector3(7.2f, .35f, 6.7f), wood);
            WagonPrimitive(ship, PrimitiveType.Cylinder, "Baca", new Vector3(0, 5f, 1.5f), new Vector3(1.2f, 2.5f, 1.2f), new Color(.17f, .16f, .16f));
            WagonPrimitive(ship, PrimitiveType.Cylinder, "Direk", new Vector3(0, 7f, 9f), new Vector3(.35f, 5.5f, .35f), wood);
            WagonPart(ship, "Yelken", new Vector3(0, 9f, 9f), new Vector3(6.5f, 5.5f, .18f), new Color(.83f, .79f, .66f));
        }

        void MakeWorldBoundary()
        {
            Color ridge = new Color(.30f, .34f, .33f);
            for (int side = -1; side <= 1; side += 2)
            {
                var eastWest = Cube("Dünya sınırı", new Vector3(side * WorldHalfSize, 15, 0), new Vector3(16, 30, WorldHalfSize * 2), ridge);
                eastWest.transform.SetParent(transform); eastWest.GetComponent<Renderer>().enabled = false;
                var northSouth = Cube("Dünya sınırı", new Vector3(0, 15, side * WorldHalfSize), new Vector3(WorldHalfSize * 2, 30, 16), ridge);
                northSouth.transform.SetParent(transform); northSouth.GetComponent<Renderer>().enabled = false;
            }
            for (int edge = 0; edge < 4; edge++)
                for (int i = 0; i < 24; i++)
                {
                    float along = -WorldHalfSize + 25 + i * 50;
                    float height = 18 + (i * 13 + edge * 7 + region * 5) % 17;
                    Vector3 p = edge == 0 ? new Vector3(along, 0, -WorldHalfSize + 16) : edge == 1 ? new Vector3(along, 0, WorldHalfSize - 16) : edge == 2 ? new Vector3(-WorldHalfSize + 16, 0, along) : new Vector3(WorldHalfSize - 16, 0, along);
                    var rock = Cube("Sınır kayalıkları", p + Vector3.up * height * .48f, new Vector3(edge < 2 ? 53 : 38, height, edge < 2 ? 38 : 53), ridge * (i % 2 == 0 ? 1f : .82f));
                    rock.transform.rotation = Quaternion.Euler(0, (i % 3 - 1) * 8, (i % 3 - 1) * 5); rock.transform.SetParent(transform);
                }
            var random = new System.Random(1203 + region * 17);
            for (int i = 0; i < 65; i++)
            {
                float x = (float)(random.NextDouble() * 1000 - 500);
                float z = (float)(random.NextDouble() * 1000 - 500);
                if (Mathf.Abs(x) < 285 && z > -100 && z < 250) continue;
                Vector3 pos = new Vector3(x, 0, z);
                if (i % 4 == 0) Tree(pos, new Color(.19f, .31f, .20f));
                else
                {
                    var stone = Cube("Çayır taşı", pos + Vector3.up * .32f, new Vector3(1.1f + i % 3 * .4f, .65f, 1.2f), new Color(.44f, .45f, .39f));
                    stone.transform.rotation = Quaternion.Euler(0, i * 23, i * 7 % 12); stone.transform.SetParent(transform);
                }
            }
        }

        void MakePlayer()
        {
            var obj = new GameObject("Oyuncu", typeof(CharacterController));
            player = obj.transform; controller = obj.GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .35f;
            var eye = new GameObject("Birinci şahıs kamera"); eye.transform.SetParent(player, false);
            eye.transform.localPosition = new Vector3(0, .5f, 0);
            view = eye.AddComponent<Camera>(); view.tag = "MainCamera";
            view.fieldOfView = 75;
            view.farClipPlane = 1800f;
            var cameraData = eye.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = eye.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            eye.AddComponent<AudioListener>();
        }

        void Road(Vector3 a, Vector3 b, int side)
        {
            Vector3 start = a;
            Vector3 end = b;
            Vector3 p1 = Vector3.Lerp(start, end, .32f) + new Vector3(-side * 35, 0, 40 + region * 3);
            Vector3 p2 = Vector3.Lerp(start, end, .68f) + new Vector3(side * 25, 0, -25 - region * 2);
            Vector3 previous = start;
            for (int i = 1; i <= 28; i++)
            {
                float t = i / 28f;
                float u = 1 - t;
                Vector3 next = u * u * u * start + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * end;
                next.y = WorldGeography.Height(next.x, next.z);
                Vector3 difference = next - previous;
                var section = Cube("Kıvrımlı yol", (next + previous) * .5f + Vector3.up * .015f, new Vector3(5.5f, .12f, difference.magnitude + .25f), new Color(.51f, .43f, .32f));
                section.transform.rotation = Quaternion.LookRotation(difference);
                section.transform.SetParent(transform);
                if (i % 3 == 0)
                {
                    Vector3 edge = Vector3.Cross(Vector3.up, difference.normalized) * 2.85f;
                    Cube("Yol kenarı taşı", (next + previous) * .5f + edge + Vector3.up * .08f, new Vector3(.25f, .2f, difference.magnitude), new Color(.43f, .41f, .34f)).transform.rotation = Quaternion.LookRotation(difference);
                    Cube("Yol kenarı taşı", (next + previous) * .5f - edge + Vector3.up * .08f, new Vector3(.25f, .2f, difference.magnitude), new Color(.43f, .41f, .34f)).transform.rotation = Quaternion.LookRotation(difference);
                }
                previous = next;
            }
        }

        void LongRoad(Vector3 a, Vector3 b)
        {
            int sections = Mathf.CeilToInt(Vector3.Distance(a, b) / 32f);
            Vector3 previous = a;
            for (int i = 1; i <= sections; i++)
            {
                Vector3 next = Vector3.Lerp(a, b, i / (float)sections);
                next.y = WorldGeography.Height(next.x, next.z);
                Vector3 direction = next - previous;
                var road = Cube("Şehirler arası yol", (previous + next) * .5f + Vector3.up * .08f,
                    new Vector3(7f, .16f, direction.magnitude + .5f), new Color(.51f, .43f, .32f));
                road.transform.rotation = Quaternion.LookRotation(direction);
                road.transform.SetParent(transform);
                previous = next;
            }
        }

        void House(Vector3 pos, float height, int style, CountryDef nation)
        {
            string modelName = height >= 5 && style != 2 ? "dutch-canal-house" : "timber-frame-house";
            var imported = Resources.Load<GameObject>("Models/" + modelName);
            if (imported != null)
            {
                var house = Instantiate(imported, pos, Quaternion.Euler(0, 180, 0), transform);
                house.name = modelName == "dutch-canal-house" ? "Tuğla kent evi" : "Ahşap kır evi";
                house.transform.localScale = Vector3.one * (modelName == "dutch-canal-house" ? .88f : .70f);
                var solid = house.AddComponent<BoxCollider>();
                solid.center = modelName == "dutch-canal-house" ? new Vector3(0, 6.2f, 0) : new Vector3(0, 5.2f, 0);
                solid.size = modelName == "dutch-canal-house" ? new Vector3(6.4f, 12.2f, 11.5f) : new Vector3(8.3f, 10.3f, 7.2f);
                return;
            }
            Color wall = style == 0 ? new Color(.69f, .60f, .46f) : style == 1 ? new Color(.59f, .60f, .57f) : new Color(.72f, .67f, .55f);
            Cube("Taş temel", pos + Vector3.up * .35f, new Vector3(5.4f, .7f, 5.4f), new Color(.35f, .34f, .30f)).transform.SetParent(transform);
            Cube("Bina", pos + Vector3.up * height / 2, new Vector3(5, height, 5), wall).transform.SetParent(transform);
            var roofLeft = Cube("Eğimli çatı", pos + new Vector3(-1.45f, height + .65f, 0), new Vector3(3.5f, .28f, 6), new Color(.27f, .19f, .18f));
            roofLeft.transform.rotation = Quaternion.Euler(0, 0, 27); roofLeft.transform.SetParent(transform);
            var roofRight = Cube("Eğimli çatı", pos + new Vector3(1.45f, height + .65f, 0), new Vector3(3.5f, .28f, 6), new Color(.27f, .19f, .18f));
            roofRight.transform.rotation = Quaternion.Euler(0, 0, -27); roofRight.transform.SetParent(transform);
            Cube("Baca", pos + new Vector3(1.4f, height + 1.4f, 1), new Vector3(.65f, 1.6f, .65f), new Color(.30f, .28f, .27f)).transform.SetParent(transform);
            Cube("Baca tepesi", pos + new Vector3(1.4f, height + 2.25f, 1), new Vector3(.86f, .18f, .86f), new Color(.25f, .24f, .22f)).transform.SetParent(transform);
            Cube("Kapı", pos + new Vector3(0, 1.2f, -2.56f), new Vector3(1.25f, 2.3f, .12f), new Color(.23f, .14f, .08f)).transform.SetParent(transform);
            Cube("Kapı çerçevesi", pos + new Vector3(0, 2.43f, -2.68f), new Vector3(1.52f, .17f, .16f), new Color(.70f, .62f, .46f)).transform.SetParent(transform);
            Sphere("Kapı tokmağı", pos + new Vector3(.39f, 1.1f, -2.7f), new Vector3(.11f, .11f, .11f), new Color(.77f, .62f, .27f));
            for (int floor = 0; floor < (height > 4.5f ? 2 : 1); floor++)
            for (int s = -1; s <= 1; s += 2)
            {
                float y = 2.3f + floor * 2.0f;
                Cube("Pencere", pos + new Vector3(s * 1.55f, y, -2.57f), new Vector3(.9f, 1.1f, .12f), new Color(.25f, .43f, .48f)).transform.SetParent(transform);
                Cube("Pencere yatay çıta", pos + new Vector3(s * 1.55f, y, -2.68f), new Vector3(1.02f, .08f, .09f), new Color(.86f, .78f, .61f)).transform.SetParent(transform);
                Cube("Pencere dikey çıta", pos + new Vector3(s * 1.55f, y, -2.68f), new Vector3(.08f, 1.2f, .09f), new Color(.86f, .78f, .61f)).transform.SetParent(transform);
                Cube("Pencere denizliği", pos + new Vector3(s * 1.55f, y - .62f, -2.72f), new Vector3(1.22f, .14f, .32f), new Color(.72f, .67f, .55f)).transform.SetParent(transform);
                Cube("Kepenk", pos + new Vector3(s * 1.55f + .67f, y, -2.64f), new Vector3(.35f, 1.13f, .10f), nation.primary).transform.SetParent(transform);
            }
            for (int i = -2; i <= 2; i++) Cube("Tuğla kuşağı", pos + new Vector3(i, .78f, -2.55f), new Vector3(.84f, .11f, .12f), new Color(.57f, .49f, .39f)).transform.SetParent(transform);
            for (int side = -1; side <= 1; side += 2)
            {
                Cube("Cephe köşe taşı", pos + new Vector3(side * 2.45f, height * .5f, -2.59f), new Vector3(.25f, height, .16f), new Color(.76f, .70f, .57f)).transform.SetParent(transform);
                Cube("Saçak", pos + new Vector3(side * 2.6f, height - .15f, 0), new Vector3(.22f, .19f, 5.8f), new Color(.26f, .18f, .15f)).transform.SetParent(transform);
            }
            Cube("Ülke şeridi", pos + new Vector3(0, height - .45f, -2.58f), new Vector3(5.2f, .25f, .13f), nation.primary).transform.SetParent(transform);
        }

        void StreetLamp(Vector3 pos)
        {
            Color iron = new Color(.16f, .17f, .17f);
            Cylinder("Gaz lambası direği", pos + Vector3.up * 2, new Vector3(.16f, 2, .16f), iron);
            Cube("Gaz lambası kolu", pos + new Vector3(.35f, 3.8f, 0), new Vector3(.85f, .13f, .13f), iron).transform.SetParent(transform);
            Cube("Gaz lambası camı", pos + new Vector3(.72f, 3.48f, 0), new Vector3(.42f, .58f, .42f), new Color(.92f, .77f, .43f)).transform.SetParent(transform);
            Cube("Gaz lambası tepesi", pos + new Vector3(.72f, 3.84f, 0), new Vector3(.56f, .16f, .56f), iron).transform.SetParent(transform);
        }

        void ResourceSite(Vector3 pos, SettlementDef def, CountryDef nation)
        {
            var good = GameController.Instance.catalog.Good(def.specialty);
            if (good == null || !good.extractable) return;
            var circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circle.name = good.displayName + " kaynak alanı"; circle.transform.SetParent(transform);
            circle.transform.position = pos + Vector3.down * .15f; circle.transform.localScale = new Vector3(7, .18f, 7);
            circle.GetComponent<Renderer>().material = Material(new Color(.33f, .29f, .23f));
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                Vector3 item = pos + new Vector3(Mathf.Cos(angle) * 4, 0, Mathf.Sin(angle) * 4);
                if (good.id == "wood") Tree(item, new Color(.13f, .34f, .17f));
                else if (good.id == "grain")
                {
                    for (int stem = -1; stem <= 1; stem++)
                    {
                        Cylinder("Tahıl sapı", item + new Vector3(stem * .28f, .58f, 0), new Vector3(.04f, .58f, .04f), new Color(.53f, .50f, .21f));
                        Sphere("Tahıl başağı", item + new Vector3(stem * .28f, 1.24f, 0), new Vector3(.18f, .46f, .18f), new Color(.84f, .70f, .30f));
                    }
                }
                else if (good.id == "cotton")
                {
                    Cylinder("Pamuk fidanı", item + Vector3.up * .45f, new Vector3(.08f, .45f, .08f), new Color(.31f, .34f, .16f));
                    for (int bud = 0; bud < 4; bud++) Sphere("Pamuk kozası", item + new Vector3(Mathf.Cos(bud * 1.57f) * .40f, .85f, Mathf.Sin(bud * 1.57f) * .40f), new Vector3(.42f, .35f, .42f), new Color(.92f, .89f, .79f));
                }
                else if (good.id == "wool")
                {
                    Sphere("Koyun yünü", item + Vector3.up * .75f, new Vector3(1.25f, .88f, .83f), new Color(.82f, .79f, .69f));
                    Sphere("Koyun başı", item + new Vector3(0, .78f, .62f), new Vector3(.39f, .38f, .48f), new Color(.30f, .28f, .26f));
                    for (int leg = -1; leg <= 1; leg += 2) for (int end = -1; end <= 1; end += 2)
                        Cube("Koyun bacağı", item + new Vector3(leg * .37f, .28f, end * .35f), new Vector3(.15f, .55f, .15f), new Color(.29f, .27f, .25f)).transform.SetParent(transform);
                }
                else
                {
                    var rock = Cube("Maden damarı", item + Vector3.up * .55f, new Vector3(1.2f, 1.0f, 1.1f), good.id == "sulfur" ? new Color(.78f, .71f, .18f) : good.id == "coal" ? new Color(.15f, .16f, .17f) : new Color(.53f, .39f, .31f));
                    rock.transform.rotation = Quaternion.Euler(13 * i, 31 * i, 9 * i); rock.transform.SetParent(transform);
                    Cube("Damar ayrıntısı", item + new Vector3(.18f, 1.10f, 0), new Vector3(.35f, .16f, .38f), nation.secondary).transform.SetParent(transform);
                }
            }
            Post(pos + new Vector3(0, 0, -6), "license", good.displayName + " kaynağı / ruhsat", def.id, nation.secondary);
        }

        void Flag(Vector3 pos, CountryDef nation)
        {
            Cube("Bayrak direği", pos + Vector3.up * 4, new Vector3(.18f, 8, .18f), new Color(.48f, .45f, .36f)).transform.SetParent(transform);
            var banner = new GameObject("Dalgalanan " + nation.name + " bayrağı", typeof(MeshFilter), typeof(MeshRenderer), typeof(FlagWave));
            banner.transform.SetParent(transform);
            banner.transform.position = pos + Vector3.up * 6.9f;
            banner.GetComponent<FlagWave>().SetColors(nation.primary, nation.secondary);
        }

        void Tree(Vector3 pos, Color color)
        {
            Cylinder("Gövde", pos + Vector3.up * 1.4f, new Vector3(.45f, 1.4f, .45f), new Color(.35f, .23f, .12f));
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2 / 5;
                Sphere("Yaprak kümesi", pos + new Vector3(Mathf.Cos(angle) * .75f, 3.25f + i % 2 * .35f, Mathf.Sin(angle) * .75f), new Vector3(2.0f, 2.15f, 2.0f), color * (i % 2 == 0 ? 1f : .82f));
            }
            Sphere("Tepe yaprakları", pos + Vector3.up * 4.2f, new Vector3(1.8f, 1.5f, 1.8f), color);
        }

        void Post(Vector3 pos, string page, string label, int settlement, Color color)
        {
            Cube(label + " direği", pos + Vector3.up * 1.2f, new Vector3(.18f, 2.4f, .18f), new Color(.36f, .25f, .15f)).transform.SetParent(transform);
            Cube(label + " tabelası", pos + Vector3.up * 2.25f, new Vector3(2.3f, .65f, .20f), color).transform.SetParent(transform);
            if (page == "market" || page == "coach")
            {
                Cube(label + " tezgâhı", pos + new Vector3(0, .72f, .55f), new Vector3(2.7f, .25f, 1.1f), new Color(.48f, .33f, .19f)).transform.SetParent(transform);
                Cube(label + " örtüsü", pos + new Vector3(0, 2.65f, .5f), new Vector3(2.9f, .15f, 1.4f), color).transform.SetParent(transform);
            }
            Shopkeeper(pos + new Vector3(0, 0, 1.55f), page == "market" ? settlement % 2 == 0 : page == "routes", color);
            points.Add(new InteractionPoint { position = pos + Vector3.up * 1.5f, page = page, label = label, settlement = settlement });
        }

        void MarketStall(Vector3 pos, string label, int settlement, Color color)
        {
            Post(pos, "market", label, settlement, color);
            Color[] produce = { new Color(.78f, .25f, .15f), new Color(.82f, .68f, .25f), new Color(.26f, .48f, .20f) };
            for (int i = 0; i < 8; i++)
                Sphere("Pazar yiyeceği", pos + new Vector3((i % 4 - 1.5f) * .52f, 1.02f, .28f + i / 4 * .42f),
                    new Vector3(.38f, .32f, .38f), produce[(i + settlement) % produce.Length]);
        }

        void Shopkeeper(Vector3 pos, bool woman, Color coat)
        {
            var imported = Resources.Load<GameObject>("Models/" + (woman ? "shopkeeper-woman" : "shopkeeper-man"));
            if (imported != null)
            {
                var personModel = Instantiate(imported, pos, Quaternion.Euler(0, 180, 0), transform);
                personModel.name = woman ? "Dükkân görevlisi kadın" : "Dükkân görevlisi adam";
                personModel.transform.localScale = Vector3.one * (woman ? 1.18f : 1f);
                return;
            }
            var person = new GameObject(woman ? "Dükkân görevlisi kadın" : "Dükkân görevlisi adam").transform;
            person.SetParent(transform); person.position = pos;
            Color skin = new Color(.69f, .48f, .34f);
            Color dark = new Color(.20f, .17f, .16f);
            WagonPrimitive(person, PrimitiveType.Capsule, "Gövde", new Vector3(0, 1.22f, 0), new Vector3(.58f, .67f, .36f), coat);
            WagonPrimitive(person, PrimitiveType.Sphere, "Baş", new Vector3(0, 2.17f, 0), new Vector3(.43f, .52f, .43f), skin);
            WagonPrimitive(person, PrimitiveType.Sphere, "Saç", new Vector3(0, 2.46f, .02f), new Vector3(.47f, .27f, .46f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                WagonPart(person, "Kol", new Vector3(side * .44f, 1.38f, 0), new Vector3(.22f, .88f, .23f), coat).localRotation = Quaternion.Euler(0, 0, side * 13);
                WagonPart(person, "El", new Vector3(side * .55f, .92f, 0), new Vector3(.19f, .20f, .19f), skin);
                WagonPart(person, "Bacak", new Vector3(side * .18f, .50f, 0), new Vector3(.23f, .95f, .24f), dark);
                WagonPart(person, "Ayakkabı", new Vector3(side * .18f, .08f, -.12f), new Vector3(.28f, .18f, .45f), new Color(.12f, .10f, .09f));
                WagonPrimitive(person, PrimitiveType.Sphere, "Göz", new Vector3(side * .14f, 2.20f, -.38f), new Vector3(.055f, .055f, .055f), dark);
            }
            if (woman)
            {
                WagonPrimitive(person, PrimitiveType.Cylinder, "Etek", new Vector3(0, .78f, 0), new Vector3(.60f, .41f, .60f), coat * .78f);
                WagonPart(person, "Saç arkası", new Vector3(0, 2.0f, .26f), new Vector3(.47f, .68f, .27f), dark);
            }
            else
            {
                WagonPrimitive(person, PrimitiveType.Cylinder, "Şapka", new Vector3(0, 2.61f, 0), new Vector3(.37f, .28f, .37f), dark);
                WagonPrimitive(person, PrimitiveType.Cylinder, "Şapka kenarı", new Vector3(0, 2.43f, 0), new Vector3(.55f, .05f, .55f), dark);
            }
        }

        void MakeConvoy()
        {
            for (int i = 0; i < GameController.Instance.state.attachedWagons; i++) MakeOneWagon();
        }

        void ParkedWagon(Vector3 position)
        {
            var wagon = CreateWagon("Arabacının satılık at arabası", position);
            wagon.rotation = Quaternion.Euler(0, 90, 0);
        }

        void MakeOneWagon()
        {
            int index = convoy.Count;
            Vector3 position = player == null ? new Vector3(0, .8f, -18 - index * 7) : convoy[0].position - convoy[0].forward * (7 * index);
            convoy.Add(CreateWagon("At arabası " + (index + 1), position));
        }

        Transform CreateWagon(string name, Vector3 position)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform);
            root.position = position;
            var cartModel = Resources.Load<GameObject>("Models/horse-cart");
            var horseModel = Resources.Load<GameObject>("Models/bay-horse");
            if (cartModel != null && horseModel != null)
            {
                var cart = Instantiate(cartModel, root);
                cart.name = "Dokulu ahşap araba";
                cart.transform.localPosition = new Vector3(0, -.8f, 0);
                cart.transform.localRotation = Quaternion.identity;
                var horse = Instantiate(horseModel, root);
                horse.name = "Koşum atı";
                horse.transform.localPosition = new Vector3(0, -.8f, 3.1f);
                horse.transform.localRotation = Quaternion.identity;
                horse.transform.localScale = Vector3.one * .92f;
                WagonPart(root, "Sürücü oturağı", new Vector3(0, .22f, .9f), new Vector3(1.55f, .18f, .5f), new Color(.29f, .16f, .08f));
                for (int side = -1; side <= 1; side += 2)
                    WagonPart(root, "Deri koşum bağı", new Vector3(side * .30f, .1f, 2.25f), new Vector3(.06f, .06f, 1.85f), new Color(.17f, .10f, .06f));
                return root;
            }
            WagonPart(root, "Ahşap kasa", new Vector3(0, .1f, 0), new Vector3(2.3f, .8f, 3), new Color(.42f, .24f, .13f));
            WagonPart(root, "Yük yatağı", new Vector3(0, .75f, -.2f), new Vector3(1.9f, .55f, 2), new Color(.61f, .48f, .30f));
            for (int side = -1; side <= 1; side += 2)
            {
                WagonPart(root, "Kasa bordası", new Vector3(side * 1.1f, .65f, -.2f), new Vector3(.16f, .75f, 2.8f), new Color(.34f, .19f, .10f));
                WagonPart(root, "Oturma koltuğu", new Vector3(side * .55f, .83f, -1.2f), new Vector3(1.04f, .22f, .75f), new Color(.55f, .34f, .17f));
                for (int axle = -1; axle <= 1; axle += 2)
                {
                    var wheel = WagonPrimitive(root, PrimitiveType.Cylinder, "Demir çemberli tekerlek", new Vector3(side * 1.35f, -.35f, axle * 1.1f), new Vector3(1.07f, .14f, 1.07f), new Color(.14f, .12f, .10f));
                    wheel.localRotation = Quaternion.Euler(0, 0, 90);
                    for (int spoke = 0; spoke < 6; spoke++)
                    {
                        var spokePart = WagonPart(root, "Tekerlek parmağı", new Vector3(side * 1.52f, -.35f, axle * 1.1f), new Vector3(.08f, .95f, .08f), new Color(.60f, .42f, .22f));
                        spokePart.localRotation = Quaternion.Euler(spoke * 30f, 0, 0);
                    }
                }
            }
            WagonPrimitive(root, PrimitiveType.Capsule, "At gövdesi", new Vector3(0, .25f, 3.4f), new Vector3(.85f, 1.25f, .85f), new Color(.34f, .22f, .14f)).localRotation = Quaternion.Euler(90, 0, 0);
            WagonPrimitive(root, PrimitiveType.Capsule, "At boynu", new Vector3(0, .9f, 4.1f), new Vector3(.44f, .72f, .44f), new Color(.34f, .22f, .14f)).localRotation = Quaternion.Euler(-24, 0, 0);
            WagonPrimitive(root, PrimitiveType.Capsule, "At başı", new Vector3(0, 1.43f, 4.45f), new Vector3(.55f, .48f, .70f), new Color(.32f, .20f, .13f)).localRotation = Quaternion.Euler(75, 0, 0);
            WagonPrimitive(root, PrimitiveType.Sphere, "At burnu", new Vector3(0, 1.16f, 4.87f), new Vector3(.55f, .31f, .35f), new Color(.43f, .29f, .21f));
            for (int side = -1; side <= 1; side += 2)
            {
                WagonPart(root, "At kulağı", new Vector3(side * .23f, 1.91f, 4.35f), new Vector3(.18f, .42f, .18f), new Color(.30f, .18f, .12f));
                WagonPrimitive(root, PrimitiveType.Sphere, "At gözü", new Vector3(side * .29f, 1.48f, 4.72f), new Vector3(.09f, .09f, .09f), Color.black);
                WagonPart(root, "At bacağı", new Vector3(side * .32f, -.55f, 2.9f), new Vector3(.23f, 1.1f, .23f), new Color(.29f, .19f, .12f));
                WagonPart(root, "At bacağı", new Vector3(side * .32f, -.55f, 3.9f), new Vector3(.23f, 1.1f, .23f), new Color(.29f, .19f, .12f));
                WagonPart(root, "Toynak", new Vector3(side * .32f, -1.12f, 2.9f), new Vector3(.3f, .22f, .38f), new Color(.17f, .15f, .14f));
                WagonPart(root, "Toynak", new Vector3(side * .32f, -1.12f, 3.9f), new Vector3(.3f, .22f, .38f), new Color(.17f, .15f, .14f));
                WagonPart(root, "Koşum", new Vector3(side * .65f, .05f, 2.2f), new Vector3(.14f, .14f, 2.3f), new Color(.20f, .14f, .10f));
                WagonPart(root, "Dizgin", new Vector3(side * .37f, .47f, 3.1f), new Vector3(.05f, .05f, 2.8f), new Color(.16f, .10f, .06f));
            }
            WagonPart(root, "At yelesi", new Vector3(0, 1.20f, 3.87f), new Vector3(.24f, .62f, .70f), new Color(.17f, .10f, .07f));
            WagonPart(root, "At kuyruğu", new Vector3(0, .31f, 2.25f), new Vector3(.23f, .90f, .24f), new Color(.18f, .11f, .08f)).localRotation = Quaternion.Euler(-25, 0, 0);
            return root;
        }

        Transform WagonPart(Transform root, string name, Vector3 localPosition, Vector3 scale, Color color)
        {
            var part = Cube(name, Vector3.zero, scale, color);
            var collider = part.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            part.transform.SetParent(root, false); part.transform.localPosition = localPosition;
            return part.transform;
        }

        Transform WagonPrimitive(Transform root, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Color color)
        {
            var part = Primitive(type, name, Vector3.zero, scale, color);
            var collider = part.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            part.transform.SetParent(root, false); part.transform.localPosition = localPosition;
            return part.transform;
        }

        void LateUpdate()
        {
            if (GameController.Instance == null || convoy.Count == 0) return;
            while (convoy.Count < GameController.Instance.state.attachedWagons) MakeOneWagon();
            while (convoy.Count > GameController.Instance.state.attachedWagons)
            {
                var last = convoy[convoy.Count - 1]; convoy.RemoveAt(convoy.Count - 1); Destroy(last.gameObject);
            }
        }

        static GameObject Cube(string name, Vector3 position, Vector3 scale, Color color)
        {
            return Primitive(PrimitiveType.Cube, name, position, scale, color);
        }
        GameObject Sphere(string name, Vector3 position, Vector3 scale, Color color)
        {
            var obj = Primitive(PrimitiveType.Sphere, name, position, scale, color);
            obj.transform.SetParent(transform); return obj;
        }
        GameObject Cylinder(string name, Vector3 position, Vector3 scale, Color color)
        {
            var obj = Primitive(PrimitiveType.Cylinder, name, position, scale, color);
            obj.transform.SetParent(transform); return obj;
        }
        static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name; obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().material = Material(color);
            return obj;
        }
        public static Material Material(Color color)
        {
            if (sharedMaterials.TryGetValue(color, out var cached)) return cached;
            var template = Resources.Load<Material>("WorldMaterial");
            if (template == null) throw new System.InvalidOperationException("WorldMaterial missing from Resources.");
            var material = new Material(template); material.color = color;
            sharedMaterials[color] = material;
            return material;
        }
    }
}
