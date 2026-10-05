using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SpriteGroups.Tests
{
    public sealed class SpriteRendererGroupTextTests
    {
        private const float Tolerance = 0.00001f;
        private GameObject root;
        private SpriteRendererGroup group;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(nameof(SpriteRendererGroupTextTests));
            group = root.AddComponent<SpriteRendererGroup>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(false)]
        [TestCase(true)]
        public void TextAlphaPreservesBaselineAndRgbThroughZeroRefreshAndDisable(bool ui)
        {
            var text = CreateText(root.transform, ui, 0.6f);
            group.Refresh();
            group.Alpha = 0.5f;
            AssertAlpha(text, 0.3f);
            group.Alpha = 0f;
            group.Refresh();
            group.Alpha = 1f;
            AssertAlpha(text, 0.6f);
            Assert.That(text.color.r, Is.EqualTo(0.2f).Within(Tolerance));
            group.Alpha = 0.5f;
            group.enabled = false;
            AssertAlpha(text, 0.6f);
            Assert.That(group.TextCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedTextsMultiplyIgnoreParentsAndTransferOnDisable(bool ui)
        {
            var nestedRoot = new GameObject("Nested");
            nestedRoot.transform.SetParent(root.transform);
            var nested = nestedRoot.AddComponent<SpriteRendererGroup>();
            var text = CreateText(nestedRoot.transform, ui, 0.8f);
            group.Refresh();
            group.Alpha = 0.5f;
            nested.Alpha = 0.25f;
            AssertAlpha(text, 0.1f);
            nested.IgnoreParentGroups = true;
            AssertAlpha(text, 0.2f);
            nested.enabled = false;
            AssertAlpha(text, 0.4f);
            Assert.That(group.TextCount, Is.EqualTo(1));
        }

        [Test]
        public void MixedTargetsUseIndependentBaseAlphaAndCounts()
        {
            var sprite = root.AddComponent<SpriteRenderer>();
            var text = CreateText(root.transform, false, 0.8f);
            var uiText = CreateText(root.transform, true, 0.6f);
            group.Refresh();
            group.Alpha = 0.5f;
            Assert.That(group.SetBaseAlpha(text, 0.4f), Is.True);
            AssertAlpha(text, 0.2f);
            AssertAlpha(uiText, 0.3f);
            Assert.That(sprite.color.a, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(group.RendererCount, Is.EqualTo(1));
            Assert.That(group.TextCount, Is.EqualTo(2));
            Assert.That(group.TargetCount, Is.EqualTo(3));
        }

        [Test]
        public void TextOwnershipTransfersBeforeOldOwnerRefreshAtZeroAlpha()
        {
            var oldBranch = new GameObject("Old Branch");
            oldBranch.transform.SetParent(root.transform);
            var text = CreateText(oldBranch.transform, true, 0.8f);
            group.Refresh();
            group.Alpha = 0f;
            var otherRoot = new GameObject("Other Root");
            try
            {
                var other = otherRoot.AddComponent<SpriteRendererGroup>();
                var newBranch = new GameObject("New Branch");
                newBranch.transform.SetParent(otherRoot.transform);
                text.transform.SetParent(newBranch.transform);
                other.Refresh();
                group.Refresh();
                other.Alpha = 0.5f;
                AssertAlpha(text, 0.4f);
                Assert.That(group.TextCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(otherRoot);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvisibleTextDuplicationPreservesBaseline(bool ui)
        {
            CreateText(root.transform, ui, 0.6f);
            group.Refresh();
            group.Alpha = 0f;
            var clone = Object.Instantiate(root);
            try
            {
                clone.GetComponent<SpriteRendererGroup>().Alpha = 1f;
                AssertAlpha(clone.GetComponentInChildren<TMP_Text>(true), 0.6f);
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void LegacySerializedRendererFieldPreservesInvisibleSpriteBaseline()
        {
            var sprite = root.AddComponent<SpriteRenderer>();
            sprite.color = new Color(1f, 1f, 1f, 0.8f);
            group.Refresh();
            group.Alpha = 0f;
            const string CurrentField = "\"Target\":";
            const string LegacyField = "\"Renderer\":";
            var legacyJson = EditorJsonUtility.ToJson(group).Replace(CurrentField, LegacyField);
            EditorJsonUtility.FromJsonOverwrite(legacyJson, group);
            group.Refresh();
            group.Alpha = 1f;
            Assert.That(sprite.color.a, Is.EqualTo(0.8f).Within(Tolerance));
        }

        [Test]
        public void WarmInactiveMixedTargetsAllocateNoManagedMemory()
        {
            root.AddComponent<SpriteRenderer>();
            var text = CreateText(root.transform, false, 0.8f);
            var uiText = CreateText(root.transform, true, 0.6f);
            group.Refresh();
            for (var index = 0; index < 32; index++)
            {
                group.Alpha = (index & 1) == 0 ? 0.5f : 1f;
                group.SetBaseAlpha(text, (index & 1) == 0 ? 0.4f : 0.8f);
                group.SetBaseAlpha(uiText, (index & 1) == 0 ? 0.3f : 0.6f);
                group.Refresh();
            }
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 100; index++)
            {
                group.Alpha = (index & 1) == 0 ? 0.5f : 1f;
                group.SetBaseAlpha(text, (index & 1) == 0 ? 0.4f : 0.8f);
                group.SetBaseAlpha(uiText, (index & 1) == 0 ? 0.3f : 0.6f);
                group.Refresh();
            }
            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static TMP_Text CreateText(Transform parent, bool ui, float alpha)
        {
            var child = new GameObject(ui ? nameof(TextMeshProUGUI) : nameof(TextMeshPro), typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(parent);
            var text = ui ? (TMP_Text)child.AddComponent<TextMeshProUGUI>() : child.AddComponent<TextMeshPro>();
            text.color = new Color(0.2f, 0.4f, 0.6f, alpha);
            return text;
        }

        private static void AssertAlpha(TMP_Text text, float expected)
            => Assert.That(text.alpha, Is.EqualTo(expected).Within(Tolerance));
    }
}
