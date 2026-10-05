using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteGroups
{
    /// <summary>Event-driven alpha inheritance. The group exclusively owns its sprites' alpha.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Sprite Renderer Group")]
    public sealed class SpriteRendererGroup : MonoBehaviour
    {
        [Serializable]
        private struct SpriteBinding
        {
            public SpriteRenderer Renderer;
            public float BaseAlpha;
            [NonSerialized] public uint Revision;
        }

        private readonly struct Ownership
        {
            public readonly SpriteRendererGroup Group;
            public readonly int Index;

            public Ownership(SpriteRendererGroup group, int index)
            {
                Group = group;
                Index = index;
            }
        }

        // Structural callbacks may reach the new owner first. Transfer the original baseline
        // through this lookup instead of capturing another group's multiplied color.
        private static readonly Dictionary<SpriteRenderer, Ownership> Owners = new();
        private static readonly List<SpriteRendererGroup> RefreshBuffer = new();
        private static readonly List<SpriteRenderer> ComponentBuffer = new();
        private static uint refreshRevision;
        private static bool refreshing;

        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField] private bool ignoreParentGroups;
        // Serialized baselines preserve Editor previews across saving, duplication and domain reloads.
        [SerializeField, HideInInspector] private List<SpriteBinding> bindings = new();
        private readonly List<SpriteRendererGroup> childGroups = new();
        private SpriteRendererGroup parentGroup;
        private float effectiveAlpha = 1f;
        private uint visitedRevision;
        private uint bufferedRevision;
        private bool initialized;

        public event Action<float> AlphaChanged;

        public float Alpha
        {
            get => alpha;
            set
            {
                var next = ClampAlpha(value);
                if (alpha == next) return;
                alpha = next;
                ApplySettings();
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
                ApplySettings();
            }
        }

        public float EffectiveAlpha => isActiveAndEnabled ? effectiveAlpha : 1f;
        public int RendererCount => bindings.Count;

        /// <summary>Applies serialized settings without scanning the Transform hierarchy.</summary>
        public void ApplySettings()
        {
            if (initialized && isActiveAndEnabled)
                ApplyBranch(parentGroup != null ? parentGroup.effectiveAlpha : 1f, false);
        }

        /// <summary>Refreshes this hierarchy root after structural changes, reusing warmed buffers.</summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled || refreshing) return;
            var root = this;
            for (var current = transform.parent; current != null; current = current.parent)
            {
                if (current.TryGetComponent<SpriteRendererGroup>(out var ancestor) && ancestor.isActiveAndEnabled)
                    root = ancestor;
            }
            root.RefreshRoot();
        }

        /// <summary>Updates only the specified sprite. Returns false when it belongs to another group.</summary>
        public bool SetBaseAlpha(SpriteRenderer renderer, float value)
        {
            var next = ClampAlpha(value);
            if (renderer == null || !Owners.TryGetValue(renderer, out var ownership) || ownership.Group != this)
                return false;
            var binding = bindings[ownership.Index];
            if (binding.BaseAlpha == next) return true;
            binding.BaseAlpha = next;
            bindings[ownership.Index] = binding;
            SetSpriteAlpha(renderer, next * effectiveAlpha);
            return true;
        }

        private void OnEnable()
        {
            Initialize();
            Refresh();
        }

        private void OnDisable()
        {
            initialized = false;
            var previousParent = parentGroup;
            SetParent(null);
            ReleaseBindings();
            if (previousParent != null && previousParent.isActiveAndEnabled)
            {
                previousParent.Refresh();
                return;
            }
            for (var index = childGroups.Count - 1; index >= 0; index--)
            {
                var child = childGroups[index];
                child.SetParent(null);
                if (child.isActiveAndEnabled) child.Refresh();
            }
        }

        private void OnValidate() => alpha = float.IsNaN(alpha) ? 1f : Mathf.Clamp01(alpha);

        private void OnTransformParentChanged()
        {
            if (!initialized || !isActiveAndEnabled) return;
            var previousParent = parentGroup;
            if (previousParent != null && previousParent.isActiveAndEnabled) previousParent.Refresh();
            Refresh();
        }

        private void OnTransformChildrenChanged()
        {
            if (initialized && isActiveAndEnabled) Refresh();
        }

        private void OnDidApplyAnimationProperties() => ApplySettings();

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            for (var index = bindings.Count - 1; index >= 0; index--)
            {
                var binding = bindings[index];
                if (binding.Renderer == null)
                {
                    RemoveBinding(index, false);
                    continue;
                }
                if (Owners.TryGetValue(binding.Renderer, out var previous) && previous.Group != this)
                {
                    binding.BaseAlpha = previous.Group.bindings[previous.Index].BaseAlpha;
                    previous.Group.RemoveBinding(previous.Index, false);
                    bindings[index] = binding;
                }
                Owners[binding.Renderer] = new Ownership(this, index);
            }
        }

        private void RefreshRoot()
        {
            refreshing = true;
            unchecked { refreshRevision++; }
            try
            {
                GatherPreviousGroups(this);
                Traverse(transform, null);
                for (var index = 0; index < RefreshBuffer.Count; index++)
                {
                    var group = RefreshBuffer[index];
                    if (group.visitedRevision != refreshRevision)
                    {
                        group.ReleaseBindings();
                        group.SetParent(null);
                        continue;
                    }
                    for (var bindingIndex = group.bindings.Count - 1; bindingIndex >= 0; bindingIndex--)
                    {
                        if (group.bindings[bindingIndex].Revision != refreshRevision)
                            group.RemoveBinding(bindingIndex, true);
                    }
                }
                ApplyBranch(1f, true);
            }
            finally
            {
                RefreshBuffer.Clear();
                ComponentBuffer.Clear();
                refreshing = false;
            }
        }

        private static void GatherPreviousGroups(SpriteRendererGroup group)
        {
            group.bufferedRevision = refreshRevision;
            RefreshBuffer.Add(group);
            for (var index = 0; index < group.childGroups.Count; index++)
                GatherPreviousGroups(group.childGroups[index]);
        }

        private static void Traverse(Transform node, SpriteRendererGroup owner)
        {
            if (node.TryGetComponent<SpriteRendererGroup>(out var group) && group.isActiveAndEnabled)
            {
                group.Initialize();
                group.SetParent(owner);
                group.visitedRevision = refreshRevision;
                if (group.bufferedRevision != refreshRevision)
                {
                    group.bufferedRevision = refreshRevision;
                    RefreshBuffer.Add(group);
                }
                owner = group;
            }
            node.GetComponents(ComponentBuffer);
            for (var index = 0; index < ComponentBuffer.Count; index++)
                owner.Collect(ComponentBuffer[index]);
            ComponentBuffer.Clear();
            for (var index = 0; index < node.childCount; index++)
                Traverse(node.GetChild(index), owner);
        }

        private void Collect(SpriteRenderer renderer)
        {
            var binding = default(SpriteBinding);
            if (Owners.TryGetValue(renderer, out var previous))
            {
                binding = previous.Group.bindings[previous.Index];
                binding.Revision = refreshRevision;
                if (previous.Group == this)
                {
                    bindings[previous.Index] = binding;
                    return;
                }
                previous.Group.RemoveBinding(previous.Index, false);
            }
            else
            {
                binding = new SpriteBinding { Renderer = renderer, BaseAlpha = renderer.color.a, Revision = refreshRevision };
            }
            Owners[renderer] = new Ownership(this, bindings.Count);
            bindings.Add(binding);
        }

        private void SetParent(SpriteRendererGroup next)
        {
            if (parentGroup == next) return;
            if (parentGroup != null) parentGroup.childGroups.Remove(this);
            parentGroup = next;
            if (next != null) next.childGroups.Add(this);
        }

        private void ApplyBranch(float inheritedAlpha, bool force)
        {
            var next = ignoreParentGroups ? alpha : alpha * inheritedAlpha;
            if (!force && effectiveAlpha == next) return;
            effectiveAlpha = next;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.Renderer != null) SetSpriteAlpha(binding.Renderer, binding.BaseAlpha * next);
            }
            for (var index = 0; index < childGroups.Count; index++)
            {
                var child = childGroups[index];
                if (force || !child.ignoreParentGroups) child.ApplyBranch(next, force);
            }
        }

        private void ReleaseBindings()
        {
            for (var index = bindings.Count - 1; index >= 0; index--)
                RemoveBinding(index, true);
        }

        private void RemoveBinding(int index, bool restore)
        {
            var binding = bindings[index];
            if (restore && binding.Renderer != null) SetSpriteAlpha(binding.Renderer, binding.BaseAlpha);
            if (!ReferenceEquals(binding.Renderer, null)) Owners.Remove(binding.Renderer);
            var lastIndex = bindings.Count - 1;
            if (index != lastIndex)
            {
                var moved = bindings[lastIndex];
                bindings[index] = moved;
                if (!ReferenceEquals(moved.Renderer, null)) Owners[moved.Renderer] = new Ownership(this, index);
            }
            bindings.RemoveAt(lastIndex);
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
