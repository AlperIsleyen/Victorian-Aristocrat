# Victorian Aristocrat — oynanabilir temel

Başlangıç sahnesi `Scenes/MainMenu.unity` dosyasıdır. Ana menü Yeni Oyun, Devam, Yükle, Ayarlar ve Çıkış seçeneklerini sunar. Devam en son geçerli kaydı açar; Yükle kayıt seçtirir. Windows derlemesi `Builds/Windows/Victorian Aristocrat.exe` konumuna üretilir. Oyunun açık olduğu sırada yeni arayüz sürümü `Builds/Windows-NewUI/Victorian Aristocrat.exe` konumuna ayrı derlenebilir. Unity menüsündeki **Victorian Aristocrat > Build Game World** veri varlıklarını, ana menüyü ve sekiz bölge sahnesini yeniler; **Build Windows Game** ve **Build Windows Game (New UI)** ekonomi doğrulamasını çalıştırıp oyunu derler.

## Kontroller

- **WASD / fare:** yürüme ve bakış; **Shift:** hızlı yürüme; **Boşluk:** zıplama.
- **Q:** ana at arabasına yaklaşınca ekranda beliren düğmeyle binme veya inme. Düğmeye fareyle tıklamak için **Alt** basılı tutulabilir. Binince **W/S** sürer, **A/D** yön verir. Arabacıdan satın alınan ek araçlar ayrıca konvoya bağlanır.
- **E:** yakındaki pazar, yolculuk, arabacı veya ruhsat noktasıyla konuşma.
- **M:** dört ülkeyi, sekiz şehri, on altı köyü, kara yollarını ve oyuncunun konumunu gösteren büyük dünya haritası; **Tab:** şirket defteri; **Esc:** açık paneli kapatma veya oyun menüsünü açma. Oyun menüsünden yeni kayıt oluşturulabilir, kayıt yüklenebilir, ayarlar değiştirilebilir, ana menüye dönülebilir veya uygulamadan çıkılabilir. Sağ üstteki minimap yakın yolları, yerleşimleri, yön gösteren oyuncu okunu, arabayı ve sabit kuzey işaretini gösterir.

Pazarda mal kategorisini ve görselini seçip miktarı ve indirimli satış seçeneğini değiştirin. Yükünüz sağ panelde görünür. Bir şehrin savaş veya kıtlık durumunu şirket defterinde görebilirsiniz. Hammadde üretim ruhsatları yerleşimlerin yakınındaki kaynak noktalarından alınır; üretim günlük birikir ve aynı noktadan yüke alınır. Karayolu ve tren seferleri ulaşım ofisindedir; gemi seferleri kıyı limanlarındaki görevliden alınır. Oyun `Application.persistentDataPath` altında otomatik kaydedilir.

Bu sürüm oyun içinde üretilen, çatı, pencere, taş işçiliği, gaz lambası, tekerlek, koşum ve hayvan ayrıntıları olan basit 3B modeller kullanır. Ekonomi ve rota verileri `Resources/GameCatalog.asset` dosyasında tutulur; dünya üretim menüsü bu dosyayı yeniden oluşturur.

Dört kurmaca ülkenin bütün yerleşimleri tek, yaklaşık 7,6 km kenarlı açık dünyada bulunur. Doğuda deniz, kıvrımlı ana kara kıyısı ve Vesper ile Meridia'ya ait birer ada köyü vardır. Seabury ve Saltwick adalarına yalnızca kıyı limanlarından gemi seferiyle ulaşılır; iç bölgelerde gemi seferi bulunmaz. Her kıyı limanında iskele, demirli yolcu gemisi ve seferleri düzenleyen görevli bulunur. Ana karadaki şehirler ve köyler görünür kara yollarıyla bağlıdır; yürüyerek veya at arabasıyla aralarında gidilebilir. Ulaşım ofisindeki ücretli hızlı seyahat de kullanılabilir. Oyuncunun açık dünya konumu kaydedilir; eski kayıtlardaki konum yeni coğrafyada güvenli yerleşim başlangıcına taşınır. Pazar, arabacı ve ulaşım noktalarında görevli karakterler bulunur. Etkileşim kartları yalnızca yakındayken görünür; mesajlar kısa süreli bildirim olarak gösterilir.
