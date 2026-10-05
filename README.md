# Sprite Renderer Group

Unity'de CanvasGroup benzeri ortak alfa kontrolünü SpriteRenderer hiyerarşilerine uygular. Sprite'ların özgün alfasını korur, iç içe grupları destekler ve boşta her kare çalışan bir Update/LateUpdate içermez.

## Package Manager ile kurulum

**Unity 6000.3 veya üzeri** gerekir. Doğrulanan sürüm: **6000.3.15f1**. Bilgisayarınızda Git kurulu olmalıdır.

1. Unity'de **Window > Package Management > Package Manager** penceresini açın.
2. **+ > Install package from Git URL...** seçin.
3. Aşağıdaki URL'yi yapıştırın:

```text
https://github.com/kameryurdakull/unity-spriterenderer-group.git?path=/Packages/com.kameryurdakull.sprite-renderer-group#v1.0.0
```

Bu URL sabit **1.0.0** sürümünü kurar. Ana daldaki son değişiklikleri almak için:

```text
https://github.com/kameryurdakull/unity-spriterenderer-group.git?path=/Packages/com.kameryurdakull.sprite-renderer-group#main
```

Paket kimliği: `com.kameryurdakull.sprite-renderer-group`.

Temel grup ve Inspector için ek paket gerekmez. DOTween, UniTask, VContainer ve Event Bus temel kurulumun bağımlılığı değildir. Eski `Assets/SpriteGroups` kopyasını kullanıyorsanız UPM kurulumundan önce kaldırın; aynı assembly'nin iki kopyası birlikte bulunmamalıdır.

## İlk kullanım: üç sprite'ı birlikte soldurmak

Önce aşağıdaki hiyerarşiyi oluşturun ve sprite renklerini ayarlayın; sonra üst nesneye **Rendering > Sprite Renderer Group** ekleyin:

```text
Character                 ← SpriteRendererGroup
├── Body                  ← SpriteRenderer, özgün alfa: 1
├── Shadow                ← SpriteRenderer, özgün alfa: 0.5
└── Weapon                ← SpriteRenderer, özgün alfa: 1
```

Inspector'da grubun **Alpha** değerini `0.5` yapın. Sonuç:

| Sprite | Özgün alfa | Grup alfası | Sonuç alfa |
| --- | ---: | ---: | ---: |
| Body | 1 | 0.5 | 0.5 |
| Shadow | 0.5 | 0.5 | 0.25 |
| Weapon | 1 | 0.5 | 0.5 |

`Alpha = 0` hepsini gizler. `Alpha = 1` özgün alfaları geri getirir; Shadow tekrar `0.5` olur.

Özgün alfa kaydedildikten sonra sprite alfasını değiştirmek için `SetBaseAlpha` kullanın. Grup component'ini devre dışı bırakmak da özgün alfalara döndürür; varsa aktif üst grup sahipliği devralır. RGB, materyal ve renderer'ın `enabled` değeri değiştirilmez.

### Import edilebilir örnek

Package Manager'da **Sprite Renderer Group > Samples > Basic Alpha Control > Import** seçin. İçe aktarılan örnekteki README'yi takip edin. `Group Alpha Example` component'inin üç nokta menüsündeki **Show**, **Hide** ve **Half Alpha** komutları Edit Mode ve Play Mode'da çalışır.

Örnek UniTask/DOTween gerektirmez ve kendi assembly'sinde bulunur.

## Runtime API

```csharp
using SpriteGroups;
using UnityEngine;

public sealed class CharacterVisibility : MonoBehaviour
{
    [SerializeField] private SpriteRendererGroup group;
    [SerializeField] private SpriteRenderer shadow;

    public void Show() => group.Alpha = 1f;
    public void Hide() => group.Alpha = 0f;

    public void SetVisibility(float alpha)
    {
        group.Alpha = alpha;
    }

    public void SetShadowOpacity(float alpha)
    {
        group.SetBaseAlpha(shadow, alpha);
    }
}
```

`CharacterVisibility` component'ini karaktere ekleyin; Inspector'da `group` alanına Character grubunu, `shadow` alanına Shadow SpriteRenderer'ını atayın. Kendi gameplay kodunuzdan public metotlarını çağırın.

| API | Davranış |
| --- | --- |
| `Alpha` | Yerel alfa; sonlu değerleri 0–1 aralığına sınırlar. |
| `EffectiveAlpha` | Üst gruplarla çarpılmış etkin alfa. |
| `IgnoreParentGroups` | Üst gruplardan alfa mirasını keser. |
| `SetBaseAlpha(renderer, alpha)` | Grubun sahibi olduğu hedef sprite'ın özgün alfasını değiştirir; sahip değilse false döner. |
| `Refresh()` | İlgili aktif kök grubun hiyerarşisini ve sahipliği yeniler. |
| `ApplySettings()` | Harici Editor aracının değiştirdiği serialized ayarları hiyerarşi taramadan uygular. |
| `RendererCount` | Grubun doğrudan sahip olduğu sprite sayısı; alt grupların sprite'larını içermez. |
| `AlphaChanged` | Runtime Alpha setter'ındaki yerel değişimleri bildirir. |

Runtime API'yi Unity ana thread'inde çağırın. `AlphaChanged`, üst grup/Inspector/Animator değişimlerinde tetiklenmez; gerekiyorsa mevcut Event Bus'a adapter ile bağlayın.

## İç içe gruplar

Weapon'a da bir grup eklerseniz sonuç şu şekilde hesaplanır:

```text
sprite özgün alfası × Weapon.Alpha × Character.Alpha
1                  × 0.5          × 0.5 = 0.25
```

Weapon grubunda **Ignore Parent Groups** işaretlendiğinde Character'ın alfası dikkate alınmaz; sonuç `0.5` olur. Aynı nesnedeki ve inactive çocuklardaki sprite'lar da kapsam dahilindedir.

## İsteğe bağlı DOTween / UniTask fade

Fade assembly'si yalnızca **UniTask 2.5.11+** kuruluysa ve **DOTWEEN** scripting symbol'ü tanımlıysa derlenir. Temel grup bu koşullara ihtiyaç duymaz.

1. UniTask'ı Package Manager'dan şu Git URL ile kurun:

   ```text
   https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11
   ```

2. [DOTween'i Demigiant'tan](https://dotween.demigiant.com/download.php) kurun. Bu projede doğrulanan sürüm **1.3.030**.
3. **Tools > Demigiant > DOTween Utility Panel > Setup DOTween...** işlemini tamamlayın. Aktif build target için `DOTWEEN` symbol'ünün eklendiğini kontrol edin. DOTween DLL'inin **Auto Reference** ayarı açık olmalıdır.
4. Grup nesnesine **Rendering > Sprite Renderer Group Fade** ekleyin.

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SpriteGroups.Tweening;
using UnityEngine;

public sealed class CharacterFade : MonoBehaviour
{
    [SerializeField] private SpriteRendererGroupFade fade;

    public void Hide()
    {
        fade.FadeTo(0f, 0.3f, Ease.OutQuad);
    }

    public async UniTask ShowAsync(CancellationToken cancellationToken)
    {
        try
        {
            await fade.FadeToAsync(1f, 0.3f, Ease.OutQuad,
                unscaledTime: true, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Yeni fade, StopFade, component kapatma veya token iptali mevcut fade'i kesebilir.
        }
    }
}
```

Fade yalnızca Play Mode'da çalışır. Yeni fade önceki fade'i keser. `StopFade()`, fade component'ini/GameObject'i kapatma veya token iptali mevcut alfa değerinde durur. Async tamamlanma ve iptal DOTween callback'leriyle bildirilir; polling yoktur. Başka thread'den token iptalinde DOTween temizliği ana thread'e aktarılır.

Kendi asmdef'inizde temel API için `SpriteGroups.Runtime`, fade için ayrıca `SpriteGroups.Tweening` ve `UniTask` referanslarını ekleyin.

## Hiyerarşi değişiklikleri ve sınırlar

- Doğrudan grup çocuklarındaki Transform değişiklikleri ve grup enable/disable olayları otomatik işlenir.
- Derin çocuk ekleme/silme, yeniden parent etme veya SpriteRenderer component ekleme/silme sonrasında `Refresh()` çağırın. Inactive kök için etkinleştirme sırasında sahiplik otomatik kurulur.
- Grup sprite alfasının sahibidir. Başka bir sistemle doğrudan `SpriteRenderer.color.a` yazmayın; `SetBaseAlpha` kullanın. RGB değiştirirken mevcut alfa değerini koruyun.
- Shader'ın SpriteRenderer renginin alfasını kullanması gerekir.
- CanvasGroup'un UI interactable/raycast özellikleri bu paketin kapsamına girmez.

## Performans

Boşta `Update`/`LateUpdate` veya sürekli kontrol yoktur. Alfa değişimi sadece etkilenen dalı günceller; etkin alfa değişmiyorsa dal atlanır. `SetBaseAlpha` sadece hedef sprite'ı günceller. `Refresh` bağımsız kökleri taramaz; value-type sprite kayıtlarını ve ısınmış koleksiyonları yeniden kullanır.

Testlerde 1.000 alfa + temel alfa güncellemesi ve 100 aynı-hiyerarşi yenilemesi ayrı ayrı **0 byte managed allocation** ile doğrulandı. Ölçüm `GC.GetAllocatedBytesForCurrentThread()` ile ısınma sonrası yapılır. Yeni kapasite büyümesi, event aboneleri, native bellek ve tween oluşturma bu ölçüme dahil değildir; FPS benchmark'ı değildir. Async fade başlatma completion/cancellation callback'leri nedeniyle allocation yapabilir.

## Paket testleri ve geliştirme

Runtime ve Editor testleri paket içindedir. Git üzerinden kurulan paketin testlerini açmak için projenizin `Packages/manifest.json` dosyasının üst seviyesine aşağıdaki alanı ekleyin; mevcut `testables` listesi varsa bu kimliği listeye ekleyin:

```json
"testables": ["com.kameryurdakull.sprite-renderer-group"]
```

Unity Test Framework kurulu olmalıdır. **Window > General > Test Runner** üzerinden `SpriteGroups.Tests` Edit Mode testlerini çalıştırın. UniTask ve DOTween etkinse `SpriteGroups.PlayModeTests` testlerini de çalıştırabilirsiniz.

Bu repository bir Unity geliştirme projesidir. UPM, yalnızca `Packages/com.kameryurdakull.sprite-renderer-group` klasörünü kurar. Geliştirme projesinin sahneleri, MCP araçları ve `Assets/Plugins` altındaki DOTween dosyaları tüketici projeye aktarılmaz.

## Referanslar

- [Unity: Git URL ile paket kurma](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-ui-giturl.html)
- [Unity: Assembly Definition koşulları](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/compilation-and-code-reload/script-compilation/assembly-definition-files/includes)
- [UniTask](https://github.com/Cysharp/UniTask)
- [DOTween](https://dotween.demigiant.com/)
