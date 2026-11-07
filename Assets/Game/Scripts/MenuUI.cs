using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VictorianAristocrat
{
    public class MenuUI : MonoBehaviour
    {
        public bool inGame;
        public bool IsOpen { get { return root != null && root.activeSelf; } }
        GameObject root;
        RectTransform body;
        TextMeshProUGUI heading;
        TextMeshProUGUI notice;
        string page = "home";
        int listPage;
        Action confirmed;
        string confirmation;
        Resolution[] resolutions;
        static readonly Color Ink = new Color(.055f, .085f, .09f, .98f);
        static readonly Color Card = new Color(.15f, .22f, .21f, .98f);
        static readonly Color Gold = new Color(.65f, .48f, .25f);

        void Start()
        {
            GameSettings.Apply();
            resolutions = Screen.resolutions.GroupBy(r => r.width + "x" + r.height).Select(g => g.First()).OrderBy(r => r.width * r.height).ToArray();
            var canvasObject = new GameObject("Main menu canvas", typeof(RectTransform), typeof(UnityEngine.Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            root = canvasObject;
            var backdrop = Panel(root.transform, "Backdrop", Vector2.zero, new Vector2(1280, 720), Ink);
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.sizeDelta = Vector2.zero;
            Panel(backdrop, "Gold rule", new Vector2(0, 307), new Vector2(760, 3), Gold);
            Label(backdrop, "Victorian Aristocrat", 43, new Vector2(0, 253), new Vector2(800, 75)).text = "VICTORIAN ARISTOCRAT";
            heading = Label(backdrop, "Section", 25, new Vector2(0, 184), new Vector2(800, 55));
            body = Panel(backdrop, "Menu content", new Vector2(0, -50), new Vector2(850, 420), Color.clear);
            notice = Label(backdrop, "Notice", 16, new Vector2(0, -312), new Vector2(850, 40));
            if (!inGame)
            {
                var events = new GameObject("Event System", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else root.SetActive(false);
            Refresh();
        }

        void Update()
        {
            if (!inGame && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame && page != "home") Back();
        }

        public void Open()
        {
            if (root == null) return;
            page = "home"; root.SetActive(true); Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Refresh();
        }

        public void HandleEscape()
        {
            if (!IsOpen) { Open(); return; }
            if (page == "home") Resume(); else Back();
        }

        void Resume()
        {
            if (!inGame) return;
            root.SetActive(false); Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        void Back() { page = "home"; Refresh(); }

        void Change(string next) { page = next; listPage = 0; Refresh(); }

        void Ask(string message, Action action)
        {
            confirmation = message; confirmed = action; page = "confirm"; Refresh();
        }

        void Clear()
        {
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                var child = body.GetChild(i).gameObject;
                child.SetActive(false); Destroy(child);
            }
            notice.text = "";
        }

        void Refresh()
        {
            if (body == null) return;
            Clear();
            heading.text = page == "home" ? (inGame ? "OYUN MENÜSÜ" : "ANA MENÜ") : page == "load" ? "KAYIT YÜKLE" : page == "settings" ? "AYARLAR" : "ONAY";
            var game = GameController.Instance;
            if (page == "home")
            {
                if (inGame)
                {
                    Button("DEVAM ET", 150, Resume);
                    Button("KAYDET", 91, () => { if (game.CreateManualSave()) notice.text = "Yeni kayıt oluşturuldu."; else notice.text = game.LastMessage; });
                    Button("YÜKLE", 32, () => Change("load"));
                    Button("AYARLAR", -27, () => Change("settings"));
                    Button("ANA MENÜYE DÖN", -86, () => Ask("Oyun kaydedilip ana menüye dönülsün mü?", () => Leave(false)));
                    Button("MASAÜSTÜNE ÇIK", -145, () => Ask("Oyun kaydedilip uygulama kapatılsın mı?", () => Leave(true)));
                }
                else
                {
                    Button("YENİ OYUN", 120, () =>
                    {
                        if (System.IO.File.Exists(game.SavePath)) Ask("Otomatik kayıt yeni oyunla değiştirilsin mi?", StartNew);
                        else StartNew();
                    });
                    var latest = game.Saves().FirstOrDefault(x => x.valid);
                    Button("DEVAM", 60, () => { if (game.LoadLatest()) EnterWorld(); else Refresh(); }, latest != null);
                    Button("YÜKLE", 0, () => Change("load"));
                    Button("AYARLAR", -60, () => Change("settings"));
                    Button("ÇIKIŞ", -120, Quit);
                }
            }
            else if (page == "load") ShowSaves(game);
            else if (page == "settings") ShowSettings();
            else if (page == "confirm")
            {
                Label(body, "Question", 22, new Vector2(0, 90), new Vector2(790, 85)).text = confirmation;
                Button("EVET", 0, () => { var action = confirmed; confirmed = null; action?.Invoke(); });
                Button("VAZGEÇ", -62, Back);
            }
            if (page != "home" && page != "confirm") Button("GERİ", -184, Back, true, 180);
        }

        void ShowSaves(GameController game)
        {
            var saves = game.Saves();
            int count = Math.Max(1, (saves.Count + 3) / 4);
            listPage = Mathf.Clamp(listPage, 0, count - 1);
            if (saves.Count == 0) Label(body, "Empty", 20, new Vector2(0, 40), new Vector2(760, 70)).text = "Henüz kayıt yok.";
            for (int i = listPage * 4; i < Math.Min(saves.Count, (listPage + 1) * 4); i++)
            {
                var save = saves[i];
                float y = 150 - (i % 4) * 64;
                var card = Panel(body, "Save", new Vector2(0, y), new Vector2(800, 56), Card);
                string place = save.valid ? game.catalog.Settlement(save.preview.settlement)?.displayName ?? "Bilinmeyen konum" : "Bozuk kayıt";
                string line = (save.automatic ? "OTOMATİK  ·  " : "KAYIT  ·  ") + save.writtenUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm") + (save.valid ? "  ·  Gün " + save.preview.day + "  ·  " + place : "  ·  Bozuk kayıt");
                Label(card, "Details", 15, new Vector2(-113, 0), new Vector2(550, 50)).text = line;
                SmallButton(card, "AÇ", 268, () =>
                {
                    Action load = () => { if (game.LoadFrom(save.path)) EnterWorld(); else { page = "load"; Refresh(); notice.text = "Kayıt yüklenemedi."; } };
                    if (inGame) Ask("Mevcut oyun durumu seçilen kayıtla değiştirilsin mi?", load); else load();
                }, save.valid);
                SmallButton(card, "SİL", 350, () => Ask("Bu kayıt silinsin mi?", () => { game.DeleteSave(save.path); page = "load"; Refresh(); }));
            }
            if (count > 1)
            {
                SmallButton(body, "←", -110, () => { listPage--; Refresh(); }, listPage > 0, -118);
                Label(body, "Page", 16, new Vector2(0, -118), new Vector2(120, 38)).text = (listPage + 1) + " / " + count;
                SmallButton(body, "→", 110, () => { listPage++; Refresh(); }, listPage < count - 1, -118);
            }
        }

        void ShowSettings()
        {
            Button("TAM EKRAN: " + (Screen.fullScreen ? "AÇIK" : "KAPALI"), 142, () => { GameSettings.SetFullScreen(!Screen.fullScreen); Refresh(); });
            Button("ÇÖZÜNÜRLÜK: " + Screen.width + " × " + Screen.height, 82, () =>
            {
                if (resolutions.Length == 0) return;
                int index = Array.FindIndex(resolutions, r => r.width == Screen.width && r.height == Screen.height);
                GameSettings.SetResolution(resolutions[(index + 1) % resolutions.Length]); Refresh();
            });
            Button("GRAFİK: " + QualitySettings.names[QualitySettings.GetQualityLevel()], 22, () => { GameSettings.SetQuality((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length); Refresh(); });
            Button("FARE HASSASİYETİ: " + GameSettings.Sensitivity.ToString("0.0") + "×", -38, () => { GameSettings.SetSensitivity(GameSettings.Sensitivity >= 2f ? .5f : GameSettings.Sensitivity + .25f); Refresh(); });
            Button("SES DÜZEYİ: %" + Mathf.RoundToInt(GameSettings.Volume * 100), -98, () => { GameSettings.SetVolume(GameSettings.Volume >= 1f ? 0f : GameSettings.Volume + .25f); Refresh(); });
        }

        void StartNew() { GameController.Instance.NewGame(); EnterWorld(); }
        void EnterWorld()
        {
            if (!GameController.Instance.SaveSnapshot()) { page = "home"; Refresh(); notice.text = GameController.Instance.LastMessage; return; }
            Time.timeScale = 1;
            SceneManager.LoadScene("Region_0");
        }
        void Leave(bool desktop)
        {
            GameController.Instance.Save();
            if (GameController.Instance.LastMessage.StartsWith("Kayıt başarısız")) { page = "home"; Refresh(); notice.text = GameController.Instance.LastMessage; return; }
            Time.timeScale = 1;
            if (desktop) Quit(); else SceneManager.LoadScene("MainMenu");
        }
        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = color.a > 0;
            return rect;
        }
        TextMeshProUGUI Label(Transform parent, string name, int size, Vector2 position, Vector2 bounds)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = bounds;
            var label = obj.GetComponent<TextMeshProUGUI>(); label.fontSize = size; label.color = new Color(.96f, .92f, .81f);
            label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
            return label;
        }
        void Button(string caption, float y, Action click, bool enabled = true, float width = 380)
        {
            var box = Panel(body, caption, new Vector2(0, y), new Vector2(width, 48), enabled ? Card : new Color(.12f, .14f, .14f));
            var button = box.gameObject.AddComponent<UnityEngine.UI.Button>(); button.interactable = enabled; button.onClick.AddListener(() => click());
            Label(box, "Text", 19, Vector2.zero, new Vector2(width - 20, 44)).text = caption;
        }
        void SmallButton(Transform parent, string caption, float x, Action click, bool enabled = true, float y = 0)
        {
            var box = Panel(parent, caption, new Vector2(x, y), new Vector2(72, 40), Gold);
            var button = box.gameObject.AddComponent<UnityEngine.UI.Button>(); button.interactable = enabled; button.onClick.AddListener(() => click());
            Label(box, "Text", 15, Vector2.zero, new Vector2(66, 36)).text = caption;
        }
    }
}
