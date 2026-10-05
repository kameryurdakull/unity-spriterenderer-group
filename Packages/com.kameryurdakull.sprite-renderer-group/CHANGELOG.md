# Changelog

## 1.1.0 — 2026-10-05

- SpriteRenderer ile birlikte child TextMeshPro ve TextMeshProUGUI alfa desteği.
- TMP hedefleri için SetBaseAlpha, TextCount ve TargetCount API'leri.
- Sprite/TMP hedeflerinde aynı event-driven sahiplik ve alfa mirası davranışı.
- Önceki Renderer alanıyla kaydedilen sprite alfaları için serialized veri migrasyonu.
- Unity 6 TMP desteği için com.unity.ugui 2.0.0 paket bağımlılığı.
- TMP, karma hedefler ve serialized migrasyon testleri.

## 1.0.0 — 2026-10-05

- Git URL ile kurulabilen Unity Package Manager paketi.
- Event-driven ortak alfa, iç içe grup mirası ve Ignore Parent Groups.
- Özgün sprite alfalarının korunması ve renderer sahipliği transferi.
- Canlı Inspector önizlemesi ve hedef sprite için SetBaseAlpha API'si.
- İsteğe bağlı DOTween/UniTask fade desteği ve callback tabanlı iptal.
- Package Manager'dan import edilebilen Basic Alpha Control örneği.
- Davranış ve ısınmış koleksiyonlarda managed allocation testleri.
