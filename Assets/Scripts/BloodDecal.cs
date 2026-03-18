using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A flat mesh decal placed on surfaces when an NPC is shot.
/// Randomizes its own scale and rotation on spawn.
/// Mop.cs cleans these by calling Clean(), which fades them out and destroys them.
///
/// Prefab setup: a Quad with a transparent blood-texture material.
///   - Material: Standard shader, Rendering Mode = Transparent
///   - Attach this component and assign the Renderer field
/// </summary>
public class BloodDecal : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;
    [SerializeField] private float _minScale = 0.08f;
    [SerializeField] private float _maxScale = 0.3f;
    [SerializeField] private float _fadeOutDuration = 0.6f;

    // ── Static registry ───────────────────────────────────────────────────────
    // Mop iterates this to find nearby decals. Capped to avoid unbounded growth.
    private const int MaxDecals = 80;
    public static readonly List<BloodDecal> Active = new List<BloodDecal>();

    private bool _isFading;

    void Awake()
    {
        // Enforce cap: remove and destroy oldest decal when limit is reached
        if (Active.Count >= MaxDecals)
        {
            BloodDecal oldest = Active[0];
            Active.RemoveAt(0);
            Destroy(oldest.gameObject);
        }

        Active.Add(this);

        // Non-uniform scale: random width and height independently so decals look
        // like elongated splatters and tear-drops rather than uniform squares.
        float w = Random.Range(_minScale, _maxScale);
        float h = Random.Range(_minScale * 0.35f, _maxScale * 0.65f);
        // Randomly swap axes so elongation can go either direction
        if (Random.value > 0.5f) { float t = w; w = h; h = t; }
        transform.localScale = new Vector3(w, h, 1f);
    }

    void OnDestroy()
    {
        Active.Remove(this);
    }

    /// <summary>
    /// Called by Mop when it passes over this decal. Fades out and destroys.
    /// </summary>
    public void Clean()
    {
        if (_isFading) return;
        _isFading = true;
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        if (_renderer == null) { Destroy(gameObject); yield break; }

        // Create a material instance so we don't modify the shared asset
        Material mat = _renderer.material;
        Color c = mat.color;
        float elapsed = 0f;

        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
            mat.color = c;
            yield return null;
        }

        Destroy(gameObject);
    }
}
