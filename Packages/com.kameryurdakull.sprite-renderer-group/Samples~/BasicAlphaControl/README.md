# Basic Alpha Control

Bu örnek UniTask veya DOTween gerektirmez.

1. Hierarchy'de boş bir `SpriteGroup` GameObject oluşturun.
2. Altına **GameObject > 2D Object > Sprites > Square** ile üç sprite ekleyin. Üst üste gelmemeleri için X konumlarını `-1.5`, `0`, `1.5` yapın.
3. İkinci sprite'ın Color alfa değerini `0.5` yapın.
4. Üst nesneye `Group Alpha Example` component'ini ekleyin. `Sprite Renderer Group` otomatik eklenir; sprite'ların ayarladığınız özgün alfaları kaydedilir.
5. `Group Alpha Example` component'inin üç nokta menüsünden **Half Alpha** seçin. Sonuç alfalar `0.5`, `0.25`, `0.5` olur.
6. **Hide** hepsini gizler. **Show** özgün alfaları (`1`, `0.5`, `1`) geri getirir.

Inspector'daki grup Alpha slider'ı da aynı sonucu verir. Bu örnek Edit Mode'da ve Play Mode'da kullanılabilir; sürekli çalışan bir Update veya coroutine içermez.

İç içe grupları denemek için üçüncü sprite'a `Sprite Renderer Group` ekleyin. Alt grubun Alpha değerini `0.5`, üst grubunkini `0.5` yaparsanız üçüncü sprite'ın sonuç alfası `0.25` olur. Alt grupta **Ignore Parent Groups** işaretlendiğinde sonuç `0.5` olur.
