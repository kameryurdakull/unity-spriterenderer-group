using System;
using System.Threading;
using DG.Tweening;
using NUnit.Framework;
using SpriteGroups.Tweening;
using UnityEngine;

namespace SpriteGroups.Tests
{
    public sealed class SpriteRendererGroupFadeTests
    {
        private GameObject root;
        private SpriteRendererGroup group;
        private SpriteRendererGroupFade fade;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(SpriteRendererGroupFadeTests));
            group = root.AddComponent<SpriteRendererGroup>();
            fade = root.AddComponent<SpriteRendererGroupFade>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        [Test]
        public void FadeUpdatesGroupAlphaAndCompletesAtTarget()
        {
            var tween = fade.FadeTo(0f, 1f).SetUpdate(UpdateType.Manual);
            tween.Goto(0.5f);
            Assert.That(group.Alpha, Is.EqualTo(0.5f).Within(0.00001f));
            tween.Complete();
            Assert.That(group.Alpha, Is.Zero);
        }

        [Test]
        public void ReplacementAndDisablingKillOnlyOwnedFade()
        {
            var first = fade.FadeTo(0f, 1f).SetUpdate(UpdateType.Manual);
            first.Goto(0.25f);
            var second = fade.FadeTo(1f, 1f).SetUpdate(UpdateType.Manual);
            Assert.That(first.IsActive(), Is.False);
            fade.enabled = false;
            Assert.That(second.IsActive(), Is.False);
            Assert.That(group.Alpha, Is.EqualTo(0.75f).Within(0.00001f));
        }

        [Test]
        public void PreCancelledAsyncFadeDoesNotReplaceExistingTween()
        {
            var existing = fade.FadeTo(0f, 1f).SetUpdate(UpdateType.Manual);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var task = fade.FadeToAsync(1f, 1f, cancellationToken: cancellation.Token);
                Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                Assert.That(existing.IsActive(), Is.True);
            }
        }

        [Test]
        public void InvalidFadeArgumentsLeaveExistingTweenAlive()
        {
            var existing = fade.FadeTo(0f, 1f).SetUpdate(UpdateType.Manual);
            Assert.Throws<ArgumentOutOfRangeException>(() => fade.FadeTo(float.NaN, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => fade.FadeTo(0f, -1f));
            Assert.That(existing.IsActive(), Is.True);
        }

        [Test]
        public void AsyncCancellationStopsAtCurrentAlphaWithoutPolling()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var task = fade.FadeToAsync(0f, 1f, cancellationToken: cancellation.Token);
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
                Assert.That(group.Alpha, Is.EqualTo(1f));
            }
        }

        [Test]
        public void AsyncReplacementCancelsOldAwaitWithoutKillingNewFade()
        {
            var task = fade.FadeToAsync(0f, 1f);
            var replacement = fade.FadeTo(0.5f, 1f).SetUpdate(UpdateType.Manual);
            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            Assert.That(replacement.IsActive(), Is.True);
        }

        [Test]
        public void ZeroDurationAsyncFadeCompletesThroughTweenCallback()
        {
            var task = fade.FadeToAsync(0.25f, 0f);
            // Drive the regular tween update synchronously; no coroutine or polling is required.
            DOTween.CompleteAll();
            task.GetAwaiter().GetResult();
            Assert.That(group.Alpha, Is.EqualTo(0.25f));
        }
    }
}
