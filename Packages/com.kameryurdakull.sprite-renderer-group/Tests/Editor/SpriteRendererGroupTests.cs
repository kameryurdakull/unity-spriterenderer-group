using NUnit.Framework;
using UnityEngine;

namespace SpriteGroups.Tests
{
    public sealed class SpriteRendererGroupTests
    {
        private const float Tolerance = 0.00001f;
        private GameObject root;
        private SpriteRendererGroup group;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(SpriteRendererGroupTests));
            group = root.AddComponent<SpriteRendererGroup>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void AlphaPreservesOriginalAlphaAndRgbThroughZeroAndRefresh()
        {
            var sprite = CreateSprite(root.transform, 0.6f);
            group.Refresh();
            group.Alpha = 0.5f;
            AssertAlpha(sprite, 0.3f);
            group.Alpha = 0f;
            group.Refresh();
            group.Alpha = 1f;
            AssertAlpha(sprite, 0.6f);
            Assert.That(sprite.color.r, Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void NestedGroupsMultiplyAndIgnoreParentsStopsInheritance()
        {
            var child = new GameObject("Child Group");
            child.transform.SetParent(root.transform);
            var nested = child.AddComponent<SpriteRendererGroup>();
            var sprite = CreateSprite(child.transform, 0.8f);
            group.Refresh();
            group.Alpha = 0.5f;
            nested.Alpha = 0.25f;
            AssertAlpha(sprite, 0.1f);
            nested.IgnoreParentGroups = true;
            AssertAlpha(sprite, 0.2f);
            Assert.That(group.RendererCount, Is.Zero);
            Assert.That(nested.RendererCount, Is.EqualTo(1));
        }

        [Test]
        public void DisablingAndRemovingOwnersRestoresOriginalAlpha()
        {
            var sprite = CreateSprite(root.transform, 0.7f);
            group.Refresh();
            group.Alpha = 0.2f;
            group.enabled = false;
            AssertAlpha(sprite, 0.7f);
            group.enabled = true;
            group.Refresh();
            AssertAlpha(sprite, 0.14f);
            Object.DestroyImmediate(group);
            AssertAlpha(sprite, 0.7f);
        }

        [Test]
        public void RefreshTransfersOwnershipWithoutCapturingMultipliedAlpha()
        {
            var first = new GameObject("First");
            first.transform.SetParent(root.transform);
            var nested = first.AddComponent<SpriteRendererGroup>();
            nested.Alpha = 0.25f;
            var sprite = CreateSprite(first.transform, 0.8f);
            group.Alpha = 0.5f;
            group.Refresh();
            AssertAlpha(sprite, 0.1f);
            sprite.transform.SetParent(root.transform);
            group.Refresh();
            AssertAlpha(sprite, 0.4f);
            Assert.That(nested.RendererCount, Is.Zero);
        }

        [Test]
        public void InactiveSpritesAndBaseAlphaUpdatesAreSupported()
        {
            var sprite = CreateSprite(root.transform, 0.8f);
            sprite.gameObject.SetActive(false);
            group.Refresh();
            group.Alpha = 0.5f;
            AssertAlpha(sprite, 0.4f);
            Assert.That(group.SetBaseAlpha(sprite, 0.2f), Is.True);
            AssertAlpha(sprite, 0.1f);
            group.Refresh();
            group.Alpha = 1f;
            AssertAlpha(sprite, 0.2f);
        }

        [Test]
        public void DisablingNestedGroupTransfersOwnershipToParentImmediately()
        {
            var child = new GameObject("Nested");
            child.transform.SetParent(root.transform);
            var nested = child.AddComponent<SpriteRendererGroup>();
            var sprite = CreateSprite(child.transform, 0.8f);
            group.Refresh();
            group.Alpha = 0.5f;
            nested.Alpha = 0.25f;
            nested.enabled = false;
            AssertAlpha(sprite, 0.4f);
            nested.enabled = true;
            AssertAlpha(sprite, 0.1f);
        }

        [Test]
        public void DuplicatingAnInvisibleGroupPreservesSerializedBaseline()
        {
            CreateSprite(root.transform, 0.6f);
            group.Refresh();
            group.Alpha = 0f;
            var clone = Object.Instantiate(root);
            try
            {
                var clonedGroup = clone.GetComponent<SpriteRendererGroup>();
                clonedGroup.Alpha = 1f;
                AssertAlpha(clone.GetComponentInChildren<SpriteRenderer>(), 0.6f);
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void InvalidAlphaIsRejectedAndFiniteValuesAreClamped()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => group.Alpha = float.NaN);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => group.Alpha = float.PositiveInfinity);
            group.Alpha = -1f;
            Assert.That(group.Alpha, Is.Zero);
            group.Alpha = 2f;
            Assert.That(group.Alpha, Is.EqualTo(1f));
        }

        [Test]
        public void AlphaChangesDoNotTouchUnrelatedRootsOrIndependentChildren()
        {
            var independent = new GameObject("Independent");
            independent.transform.SetParent(root.transform);
            var independentGroup = independent.AddComponent<SpriteRendererGroup>();
            independentGroup.IgnoreParentGroups = true;
            var independentSprite = CreateSprite(independent.transform, 0.8f);
            var unrelated = new GameObject("Unrelated");
            try
            {
                var unrelatedGroup = unrelated.AddComponent<SpriteRendererGroup>();
                var unrelatedSprite = CreateSprite(unrelated.transform, 0.6f);
                unrelatedGroup.Refresh();
                group.Refresh();
                // Sentinel alpha writes detect any unnecessary renderer updates on these branches.
                independentSprite.color = new Color(1f, 1f, 1f, 0.37f);
                unrelatedSprite.color = new Color(1f, 1f, 1f, 0.43f);
                group.Alpha = 0.5f;
                AssertAlpha(independentSprite, 0.37f);
                AssertAlpha(unrelatedSprite, 0.43f);
            }
            finally
            {
                Object.DestroyImmediate(unrelated);
            }
        }

        [Test]
        public void SetBaseAlphaOnlyWritesTheRequestedRenderer()
        {
            var first = CreateSprite(root.transform, 0.6f);
            var second = CreateSprite(root.transform, 0.8f);
            group.Refresh();
            second.color = new Color(1f, 1f, 1f, 0.37f);
            group.SetBaseAlpha(first, 0.4f);
            AssertAlpha(first, 0.4f);
            AssertAlpha(second, 0.37f);
        }

        [Test]
        public void NewOwnerCanRefreshFirstWithoutLosingInvisibleSpriteBaseline()
        {
            var oldBranch = new GameObject("Old Branch");
            oldBranch.transform.SetParent(root.transform);
            var sprite = CreateSprite(oldBranch.transform, 0.8f);
            group.Refresh();
            group.Alpha = 0f;
            var otherRoot = new GameObject("Other Root");
            try
            {
                var newGroup = otherRoot.AddComponent<SpriteRendererGroup>();
                var newBranch = new GameObject("New Branch");
                newBranch.transform.SetParent(otherRoot.transform);
                sprite.transform.SetParent(newBranch.transform);
                newGroup.Refresh();
                group.Refresh();
                newGroup.Alpha = 0.5f;
                AssertAlpha(sprite, 0.4f);
                Assert.That(group.RendererCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        public void ReparentingNestedGroupUpdatesOldAndNewInheritance()
        {
            var child = new GameObject("Nested");
            child.transform.SetParent(root.transform);
            var nested = child.AddComponent<SpriteRendererGroup>();
            nested.Alpha = 0.25f;
            var sprite = CreateSprite(child.transform, 0.8f);
            group.Refresh();
            group.Alpha = 0.5f;
            var otherRoot = new GameObject("Other Root");
            try
            {
                var newGroup = otherRoot.AddComponent<SpriteRendererGroup>();
                newGroup.Alpha = 0.8f;
                child.transform.SetParent(otherRoot.transform);
                AssertAlpha(sprite, 0.16f);
                group.Alpha = 0f;
                AssertAlpha(sprite, 0.16f);
                newGroup.Alpha = 0.4f;
                AssertAlpha(sprite, 0.08f);
            }
            finally
            {
                Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        public void WarmAlphaAndBaseAlphaUpdatesAllocateNoManagedMemory()
        {
            var sprite = CreateSprite(root.transform, 0.8f);
            group.Refresh();
            for (var index = 0; index < 32; index++)
            {
                group.Alpha = (index & 1) == 0 ? 0.5f : 1f;
                group.SetBaseAlpha(sprite, (index & 1) == 0 ? 0.4f : 0.8f);
            }
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 1000; index++)
            {
                group.Alpha = (index & 1) == 0 ? 0.5f : 1f;
                group.SetBaseAlpha(sprite, (index & 1) == 0 ? 0.4f : 0.8f);
            }
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void WarmRefreshReusesStorageWithoutManagedAllocations()
        {
            for (var index = 0; index < 16; index++) CreateSprite(root.transform, 0.8f);
            for (var index = 0; index < 32; index++) group.Refresh();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 100; index++) group.Refresh();
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static SpriteRenderer CreateSprite(Transform parent, float alpha)
        {
            var child = new GameObject("Sprite");
            child.transform.SetParent(parent);
            var sprite = child.AddComponent<SpriteRenderer>();
            sprite.color = new Color(0.2f, 0.4f, 0.6f, alpha);
            return sprite;
        }

        private static void AssertAlpha(SpriteRenderer sprite, float expected)
            => Assert.That(sprite.color.a, Is.EqualTo(expected).Within(Tolerance));
    }
}
