using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;

namespace SpriteGroups.Tweening
{
    /// <summary>Optional DOTween/UniTask adapter. Each instance owns at most one fade.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRendererGroup))]
    [AddComponentMenu("Rendering/Sprite Renderer Group Fade")]
    public sealed class SpriteRendererGroupFade : MonoBehaviour
    {
        private SpriteRendererGroup group;
        private Tween activeTween;
        private DOGetter<float> alphaGetter;
        private DOSetter<float> alphaSetter;

        public Tween FadeTo(float targetAlpha, float duration, Ease ease = Ease.Linear, bool unscaledTime = false)
        {
            return StartFade(targetAlpha, duration, ease, unscaledTime, true);
        }

        /// <summary>Cancellation, replacement or disabling cancels the await and preserves the current alpha.</summary>
        public async UniTask FadeToAsync(float targetAlpha, float duration, Ease ease = Ease.Linear,
            bool unscaledTime = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tween = StartFade(targetAlpha, duration, ease, unscaledTime, false);
            var completion = new UniTaskCompletionSource();
            var killed = false;
            tween.OnComplete(() => completion.TrySetResult());
            tween.OnKill(() =>
            {
                killed = true;
                completion.TrySetCanceled();
            });
            var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            try
            {
                await completion.Task;
            }
            finally
            {
                registration.Dispose();
                // Cancellation may originate on a worker thread; DOTween cleanup belongs on the main thread.
                await UniTask.SwitchToMainThread();
                tween.OnComplete(null).OnKill(null);
                if (!killed) tween.Kill();
                if (ReferenceEquals(activeTween, tween)) activeTween = null;
            }
        }

        public void StopFade()
        {
            activeTween?.Kill();
            activeTween = null;
        }

        private void Awake() => group = GetComponent<SpriteRendererGroup>();
        private void OnDisable() => StopFade();

        private Tween StartFade(float targetAlpha, float duration, Ease ease, bool unscaledTime, bool autoKill)
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                throw new InvalidOperationException("Fades require an enabled component in Play Mode.");
            if (float.IsNaN(targetAlpha) || float.IsInfinity(targetAlpha))
                throw new ArgumentOutOfRangeException(nameof(targetAlpha));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));

            if (group == null) group = GetComponent<SpriteRendererGroup>();
            if (!group.isActiveAndEnabled)
                throw new InvalidOperationException("The sprite group must be enabled.");
            StopFade();
            alphaGetter ??= ReadAlpha;
            alphaSetter ??= WriteAlpha;
            activeTween = DOTween.To(alphaGetter, alphaSetter, Mathf.Clamp01(targetAlpha), duration)
                .SetEase(ease)
                .SetUpdate(unscaledTime)
                .SetAutoKill(autoKill)
                .SetRecyclable(false);
            return activeTween;
        }

        private float ReadAlpha() => group.Alpha;
        private void WriteAlpha(float value) => group.Alpha = value;
    }
}
