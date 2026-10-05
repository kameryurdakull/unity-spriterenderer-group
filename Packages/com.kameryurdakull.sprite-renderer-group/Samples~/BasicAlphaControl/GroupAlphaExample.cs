using UnityEngine;

namespace SpriteGroups.Samples
{
    /// <summary>Use the component's context menu to control all sprites below this GameObject.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRendererGroup))]
    [AddComponentMenu("Rendering/Samples/Group Alpha Example")]
    public sealed class GroupAlphaExample : MonoBehaviour
    {
        private const string ShowCommand = "Show";
        private const string HideCommand = "Hide";
        private const string HalfAlphaCommand = "Half Alpha";

        [ContextMenu(ShowCommand)]
        public void Show() => GetComponent<SpriteRendererGroup>().Alpha = 1f;

        [ContextMenu(HideCommand)]
        public void Hide() => GetComponent<SpriteRendererGroup>().Alpha = 0f;

        [ContextMenu(HalfAlphaCommand)]
        public void HalfAlpha() => GetComponent<SpriteRendererGroup>().Alpha = 0.5f;
    }
}
