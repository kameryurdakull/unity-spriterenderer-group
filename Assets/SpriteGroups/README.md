# Sprite Renderer Group

Üst GameObject'e **Rendering > Sprite Renderer Group** ekleyin. `Alpha`, aynı nesnedeki ve alt nesnelerdeki SpriteRenderer'ların özgün alfasıyla çarpılır. Inactive sprite'lar da kapsanır. İç içe aktif gruplar kendi sprite'larını yönetir; sonuç `spriteBaseAlpha × childAlpha × parentAlpha` olur. `Ignore Parent Groups` üst gruplardan alfa mirasını keser. Component kapatıldığında özgün alfa geri yüklenir; varsa aktif üst grup sahipliği devralır.

Inspector alfa önizlemesi Edit Mode'da da çalışır. Shader'ın SpriteRenderer renginin alfasını kullanması gerekir. RGB, materyal ve renderer'ın `enabled` değeri değiştirilmez. CanvasGroup'un UI etkileşimi/raycast özellikleri bu component'in kapsamına girmez.

```csharp
using SpriteGroups;

group.Alpha = 0.5f;
group.IgnoreParentGroups = true;
group.SetBaseAlpha(spriteRenderer, 0.8f);
group.Refresh();
```

Grup sprite alfasının sahibidir. Başka bir sistemin `SpriteRenderer.color.a` yazması yerine `SetBaseAlpha` kullanın. RGB değiştirirken mevcut alfa değerini koruyun; grup her kare rengi kontrol etmez. Doğrudan grup çocuklarının hiyerarşi değişimleri otomatik izlenir. Derin çocuklara runtime'da sprite ekleme/silme, renderer component ekleme/silme veya yeniden parent etme sonrasında aktif bir grubun `Refresh()` metodunu çağırın. Yenileme ilgili üst grup ağacını tarar; bağımsız kökleri taramaz. Yeni sahibin önce yenilendiği durumlarda da özgün alfa korunur. Runtime API Unity ana thread'inde çağrılmalıdır.

## Runtime maliyeti

- `Update`, `LateUpdate` veya sürekli hiyerarşi/sprite kontrolü yoktur. Boşta component'in frame başına işi yoktur.
- Alfa değiştiğinde yalnızca grubun sahip olduğu sprite'lar ve alfa miras alan alt gruplar güncellenir. Etkin alfa değişmemişse dal atlanır. `Ignore Parent Groups` alt dalları üst değişimden etkilenmez.
- `SetBaseAlpha` ownership lookup üzerinden sadece hedef sprite'ı günceller; tüm listeyi taramaz.
- Özgün alfalar value-type kayıtlarla tutulur. `Refresh` kayıtları yeniden nesne olarak oluşturmaz. Listeler ve lookup yalnızca kapasite büyüdüğünde managed bellek ayırır; scratch buffer'lar yenileme sonunda referanslarını temizler.
- Tutulan kalıcı veri: sahip olunan sprite ve özgün alfa kayıtları, doğrudan üst/alt grup ilişkileri, etkin alfa ve transferler için renderer ownership lookup. Ownership lookup yeni sahibin önce callback aldığı durumlarda çarpılmış rengi temel alfa olarak kaydetmeyi önler.
- Inspector alfa değişikliği hiyerarşi yenilemesi yapmaz. Serialize edilmiş alanları harici bir Editor aracı değiştirdiğinde `ApplySettings()` çağırın; Animator callback'i bunu otomatik yapar.

Unity Edit Mode testlerinde ısınmış koleksiyonlarla 1.000 alfa + temel alfa güncellemesi ve 100 aynı-hiyerarşi yenilemesi ayrı ayrı `GC.GetAllocatedBytesForCurrentThread()` üzerinden **0 byte** managed allocation ile doğrulandı. Bu ölçüm yeni kapasite ihtiyacını, event abonelerinin kodunu, Unity'nin native belleğini veya tween oluşturmayı kapsamaz; FPS benchmark'ı değildir.

## Fade

İsteğe bağlı **Rendering > Sprite Renderer Group Fade** component'ini ekleyin. Temel runtime assembly DOTween veya UniTask gerektirmez; ayrı Tweening assembly'si bu paketleri kullanır.

```csharp
using DG.Tweening;
using SpriteGroups.Tweening;

fade.FadeTo(0f, 0.3f, Ease.OutQuad);
await fade.FadeToAsync(1f, 0.3f, Ease.OutQuad,
    unscaledTime: true, cancellationToken: cancellationToken);
fade.StopFade();
```

Fade Play Mode'da çalışır. Yeni fade mevcut fade'i keser. `StopFade`, fade component'ini veya GameObject'i kapatma ve cancellation token iptali mevcut alfa değerinde durur. Kesilen async fade `OperationCanceledException` üretir. Async tamamlanma `OnComplete`/`OnKill` callback'leriyle bildirilir; `WaitUntil` veya polling yoktur. Unity ana thread'inde token iptali hemen işlenir; başka thread'den iptalde tween temizliği ana thread'e aktarılır. Await tamamlanınca tween temizlenir; while/coroutine kullanılmaz.

DOTween getter/setter delegate'leri ilk fade'den sonra yeniden kullanılır. Yeni tween ve async completion/cancellation callback'leri oluşturulurken managed allocation olabilir; tüm fade API'si için sıfır GC garantisi yoktur. Dışarı verilen Tween referanslarının başka tween'lere dönüşmemesi için tween recycling kapalıdır.

`AlphaChanged` yalnızca runtime `Alpha` setter'ındaki yerel değişimleri bildirir; üst grup değişimleri ve Inspector/Animator değişimleri bu event'i üretmez. Event'i mevcut Event Bus'a adapter üzerinden aktarabilirsiniz; bu component'in DI container'a ihtiyacı yoktur.

Paketler projenin Package Structure kurulum servisiyle eklendi: UniTask 2.5.11 ve DOTween 1.3.030.
