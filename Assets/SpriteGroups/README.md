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

Grup sprite alfasının sahibidir. Başka bir sistemin `SpriteRenderer.color.a` yazması yerine `SetBaseAlpha` kullanın; RGB değişiklikleri serbesttir. Doğrudan grup çocuklarının hiyerarşi değişimleri otomatik izlenir. Derin çocuklara runtime'da sprite ekleme/silme, renderer component ekleme/silme veya yeniden parent etme sonrasında `Refresh()` çağırın. Yenileme özgün alfaları korur; alfa sıfırken de güvenlidir. Runtime API Unity ana thread'inde çağrılmalıdır.

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

Fade Play Mode'da çalışır. Yeni fade mevcut fade'i keser. `StopFade`, fade component'ini veya GameObject'i kapatma ve cancellation token iptali mevcut alfa değerinde durur. Kesilen async fade `OperationCanceledException` üretir. Token iptali sonraki UniTask Update adımında işlenir. Await tamamlanınca tween temizlenir; while/coroutine kullanılmaz.

`AlphaChanged` yalnızca runtime `Alpha` setter'ındaki yerel değişimleri bildirir; üst grup değişimleri ve Inspector/Animator değişimleri bu event'i üretmez. Event'i mevcut Event Bus'a adapter üzerinden aktarabilirsiniz; bu component'in DI container'a ihtiyacı yoktur.

Paketler projenin Package Structure kurulum servisiyle eklendi: UniTask 2.5.11 ve DOTween 1.3.030.
