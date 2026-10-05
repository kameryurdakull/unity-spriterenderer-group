using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpriteGroups
{
    /// <summary>Event-driven alpha inheritance for SpriteRenderer and TMP_Text targets.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Sprite Renderer Group")]
    public sealed class SpriteRendererGroup : MonoBehaviour
    {
        [Serializable]
        private struct AlphaBinding
        {
            [FormerlySerializedAs("Renderer")] public Component Target;
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
        private static readonly Dictionary<Component, Ownership> Owners = new();
        private static readonly List<SpriteRendererGroup> RefreshBuffer = new();
        private static readonly List<Component> ComponentBuffer = new();
        private static uint refreshRevision;
        private static bool refreshing;

        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField] private bool ignoreParentGroups;
        // Serialized baselines preserve Editor previews across saving, duplication and domain reloads.
        [SerializeField, HideInInspector] private List<AlphaBinding> bindings = new();
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
        public int RendererCount => CountTargets<SpriteRenderer>();
        public int TextCount => CountTargets<TMP_Text>();
        public int TargetCount => bindings.Count;

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
            => SetBaseAlpha((Component)renderer, value);

        /// <summary>Updates a supported sprite or TMP target without visiting any other target.</summary>
        public bool SetBaseAlpha(Component target, float value)
        {
            var next = ClampAlpha(value);
            if (target == null || !Owners.TryGetValue(target, out var ownership) || ownership.Group != this)
                return false;
            var binding = bindings[ownership.Index];
            if (binding.BaseAlpha == next) return true;
            binding.BaseAlpha = next;
            bindings[ownership.Index] = binding;
            SetTargetAlpha(target, next * effectiveAlpha);
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
                if (binding.Target == null)
                {
                    RemoveBinding(index, false);
                    continue;
                }
                if (Owners.TryGetValue(binding.Target, out var previous) && previous.Group != this)
                {
                    binding.BaseAlpha = previous.Group.bindings[previous.Index].BaseAlpha;
                    previous.Group.RemoveBinding(previous.Index, false);
                    bindings[index] = binding;
                }
                Owners[binding.Target] = new Ownership(this, index);
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
            {
                var target = ComponentBuffer[index];
                if (target is SpriteRenderer || target is TMP_Text) owner.Collect(target);
            }
            ComponentBuffer.Clear();
            for (var index = 0; index < node.childCount; index++)
                Traverse(node.GetChild(index), owner);
        }

        private void Collect(Component target)
        {
            var binding = default(AlphaBinding);
            if (Owners.TryGetValue(target, out var previous))
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
                binding = new AlphaBinding { Target = target, BaseAlpha = ReadTargetAlpha(target), Revision = refreshRevision };
            }
            Owners[target] = new Ownership(this, bindings.Count);
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
                if (binding.Target != null) SetTargetAlpha(binding.Target, binding.BaseAlpha * next);
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
            if (restore && binding.Target != null) SetTargetAlpha(binding.Target, binding.BaseAlpha);
            if (!ReferenceEquals(binding.Target, null)) Owners.Remove(binding.Target);
            var lastIndex = bindings.Count - 1;
            if (index != lastIndex)
            {
                var moved = bindings[lastIndex];
                bindings[index] = moved;
                if (!ReferenceEquals(moved.Target, null)) Owners[moved.Target] = new Ownership(this, index);
            }
            bindings.RemoveAt(lastIndex);
        }

        private int CountTargets<T>() where T : Component
        {
            var count = 0;
            for (var index = 0; index < bindings.Count; index++)
            {
                if (bindings[index].Target is T) count++;
            }
            return count;
        }

        private static float ReadTargetAlpha(Component target)
        {
            if (target is SpriteRenderer renderer) return renderer.color.a;
            return ((TMP_Text)target).alpha;
        }

        private static void SetTargetAlpha(Component target, float value)
        {
            if (target is SpriteRenderer renderer)
            {
                var color = renderer.color;
                if (color.a == value) return;
                color.a = value;
                renderer.color = color;
            }
            else if (target is TMP_Text text && text.alpha != value)
            {
                text.alpha = value;
            }
        }

        private static float ClampAlpha(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Alpha must be finite.");
            return Mathf.Clamp01(value);
        }
    }
}
