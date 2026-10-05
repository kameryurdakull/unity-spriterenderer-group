using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteGroups
{
    /// <summary>Multiplies the original alpha of owned sprites by this group and its parent groups.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Sprite Renderer Group")]
    public sealed class SpriteRendererGroup : MonoBehaviour
    {
        [Serializable]
        private sealed class SpriteBinding
        {
            public SpriteRenderer Renderer;
            public float BaseAlpha;

            public SpriteBinding(SpriteRenderer renderer)
            {
                Renderer = renderer;
                BaseAlpha = renderer.color.a;
            }
        }

        private static readonly List<SpriteRendererGroup> ActiveGroups = new();
        private static bool hierarchyDirty;

        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField] private bool ignoreParentGroups;
        // Serialized baselines survive assembly reloads and saved Editor previews, including alpha zero.
        [SerializeField, HideInInspector] private List<SpriteBinding> bindings = new();
        private readonly List<SpriteRenderer> spriteBuffer = new();

        public event Action<float> AlphaChanged;

        public float Alpha
        {
            get => alpha;
            set
            {
                var next = ClampAlpha(value);
                if (alpha == next) return;
                alpha = next;
                ApplyAll();
                AlphaChanged?.Invoke(alpha);
            }
        }

        public bool IgnoreParentGroups
        {
            get => ignoreParentGroups;
            set
            {
                if (ignoreParentGroups == value) return;
                ignoreParentGroups = value;
                ApplyAll();
            }
        }

        public float EffectiveAlpha
        {
            get
            {
                if (!isActiveAndEnabled) return 1f;
                var result = alpha;
                if (ignoreParentGroups) return result;
                for (var parent = transform.parent; parent != null; parent = parent.parent)
                {
                    if (!parent.TryGetComponent<SpriteRendererGroup>(out var group) || !group.isActiveAndEnabled)
                        continue;
                    result *= group.alpha;
                    if (group.ignoreParentGroups) break;
                }
                return result;
            }
        }

        public int RendererCount => bindings.Count;

        /// <summary>Rebuilds ownership after adding/removing sprites or changing a deep child hierarchy.</summary>
        public void Refresh()
        {
            hierarchyDirty = true;
            ApplyAll();
        }

        /// <summary>Changes a sprite's unmultiplied alpha. Returns false if another group owns the sprite.</summary>
        public bool SetBaseAlpha(SpriteRenderer renderer, float value)
        {
            var next = ClampAlpha(value);
            if (hierarchyDirty) RebuildAll();
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.Renderer != renderer || renderer == null) continue;
                binding.BaseAlpha = next;
                Apply();
                return true;
            }
            return false;
        }

        private void OnEnable()
        {
            Restore();
            bindings.Clear();
            if (!ActiveGroups.Contains(this)) ActiveGroups.Add(this);
            hierarchyDirty = true;
            ApplyAll();
        }

        private void OnDisable()
        {
            Restore();
            bindings.Clear();
            ActiveGroups.Remove(this);
            hierarchyDirty = true;
            ApplyAll();
        }

        private void OnValidate()
        {
            alpha = float.IsNaN(alpha) ? 1f : Mathf.Clamp01(alpha);
            hierarchyDirty = true;
        }

        private void OnTransformParentChanged() => hierarchyDirty = true;
        private void OnTransformChildrenChanged() => hierarchyDirty = true;
        private void OnDidApplyAnimationProperties() => ApplyAll();

        private void LateUpdate()
        {
            if (hierarchyDirty) RebuildAll();
            Apply();
        }

        private static void ApplyAll()
        {
            if (hierarchyDirty) RebuildAll();
            for (var index = 0; index < ActiveGroups.Count; index++)
                ActiveGroups[index].Apply();
        }

        private static void RebuildAll()
        {
            hierarchyDirty = false;
            // Restore every previous owner before any new owner captures its baseline.
            for (var index = 0; index < ActiveGroups.Count; index++)
            {
                ActiveGroups[index].Restore();
                ActiveGroups[index].bindings.Clear();
            }
            for (var index = 0; index < ActiveGroups.Count; index++)
                ActiveGroups[index].Collect();
        }

        private void Collect()
        {
            spriteBuffer.Clear();
            GetComponentsInChildren(true, spriteBuffer);
            for (var index = 0; index < spriteBuffer.Count; index++)
            {
                var renderer = spriteBuffer[index];
                if (FindOwner(renderer.transform) == this)
                    bindings.Add(new SpriteBinding(renderer));
            }
            spriteBuffer.Clear();
        }

        private static SpriteRendererGroup FindOwner(Transform target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                if (current.TryGetComponent<SpriteRendererGroup>(out var group) && group.isActiveAndEnabled)
                    return group;
            }
            return null;
        }

        private void Apply()
        {
            var multiplier = EffectiveAlpha;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.Renderer == null) continue;
                SetSpriteAlpha(binding.Renderer, binding.BaseAlpha * multiplier);
            }
        }

        private void Restore()
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.Renderer != null) SetSpriteAlpha(binding.Renderer, binding.BaseAlpha);
            }
        }

        private static void SetSpriteAlpha(SpriteRenderer renderer, float value)
        {
            var color = renderer.color;
            if (color.a == value) return;
            color.a = value;
            renderer.color = color;
        }

        private static float ClampAlpha(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Alpha must be finite.");
            return Mathf.Clamp01(value);
        }
    }
}
