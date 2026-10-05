using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
            try
            {
                await UniTask.WaitUntil(() => !tween.IsActive() || tween.IsComplete(),
                    cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!tween.IsActive()) throw new OperationCanceledException();
            }
            finally
            {
                tween.Kill();
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
            activeTween = DOTween.To(() => group.Alpha, value => group.Alpha = value,
                    Mathf.Clamp01(targetAlpha), duration)
                .SetEase(ease)
                .SetUpdate(unscaledTime)
                .SetAutoKill(autoKill)
                .SetRecyclable(false);
            return activeTween;
        }
    }
}
