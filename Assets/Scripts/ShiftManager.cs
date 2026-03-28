using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative shift manager that drives the day/night cycle.
///
/// Phase flow:
///   Dawn → DayShift → (all NPCs finished) → Transition → NightShift → Dawn → ...
///   If no doppelgangers escaped during the day, skips Transition/NightShift and goes straight to Dawn.
///
/// Multiplayer:
///   All state is in NetworkVariables — clients read them for UI, lighting, audio.
///   Phase transitions are server-initiated. Clients react via OnValueChanged callbacks.
///
/// Debug:
///   Set <see cref="enableDebugTools"/> to true (Inspector checkbox or runtime toggle)
///   to expose an OnGUI overlay with buttons for forcing phase transitions, adjusting
///   escaped doppelganger count, and skipping timers.
/// </summary>
public class ShiftManager : NetworkBehaviour
{
    // ──────────────────────────────────────────────
    // Phase enum
    // ──────────────────────────────────────────────

    public enum ShiftPhase
    {
        /// <summary>Brief calm period between shifts. Cleanup, score screen, prep.</summary>
        Dawn = 0,
        /// <summary>Pharmacy is open. NPCs arrive, player verifies prescriptions.</summary>
        DayShift = 1,
        /// <summary>Short cinematic/atmospheric beat before night begins.</summary>
        Transition = 2,
        /// <summary>Monster is active. Player crafts weapon, survives until dawn.</summary>
        NightShift = 3
    }

    // ──────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The NPCSpawnManager in the scene.")]
    [SerializeField] private NPCSpawnManager spawnManager;

    [Tooltip("The RoundConfig to use for the first day shift. Future nights may generate configs dynamically.")]
    [SerializeField] private RoundConfig defaultRoundConfig;

    [Tooltip("Score manager for tracking shift outcomes. Optional — leave null if not using scoring.")]
    [SerializeField] private ShiftScoreManager scoreManager;

    [Header("Timing")]
    [Tooltip("Seconds to wait in Dawn phase before starting the next day shift.")]
    [SerializeField] private float dawnDuration = 5f;

    [Tooltip("Seconds the Transition phase lasts (lights flicker, atmosphere change) before night begins.")]
    [SerializeField] private float transitionDuration = 4f;

    [Tooltip("Seconds the night shift lasts before dawn arrives (0 = infinite, ended only by killing all monsters).")]
    [SerializeField] private float nightDuration = 120f;

    [Header("Debug")]
    [Tooltip("Enable the runtime debug overlay (OnGUI buttons) for testing shift transitions.")]
    [SerializeField] private bool enableDebugTools = false;

    // ──────────────────────────────────────────────
    // Networked state
    // ──────────────────────────────────────────────

    /// <summary>Current phase, readable by all clients.</summary>
    public NetworkVariable<int> CurrentPhase { get; } =
        new NetworkVariable<int>((int)ShiftPhase.Dawn, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>How many doppelgangers escaped (were approved) this day shift.</summary>
    public NetworkVariable<int> EscapedDoppelgangers { get; } =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Current night number (1-based). Increments each time a full day/night cycle completes.</summary>
    public NetworkVariable<int> CurrentNight { get; } =
        new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ──────────────────────────────────────────────
    // Events (server-side, for other systems to hook into)
    // ──────────────────────────────────────────────

    /// <summary>Fired on the server when the phase changes. Arg = new phase.</summary>
    public event Action<ShiftPhase> OnPhaseChanged;

    /// <summary>Fired on the server when a day shift starts cleanly.</summary>
    public event Action OnDayShiftStarted;

    /// <summary>Fired on the server when night shift starts.</summary>
    public event Action OnNightShiftStarted;

    /// <summary>Fired on the server when a shift cycle ends cleanly (dawn reached).</summary>
    public event Action OnShiftCycleCompleted;

    // ──────────────────────────────────────────────
    // Private state
    // ──────────────────────────────────────────────

    private Coroutine _phaseCoroutine;

    /// <summary>Convenience accessor for the current phase as the enum type.</summary>
    public ShiftPhase Phase => (ShiftPhase)CurrentPhase.Value;

    // ──────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Listen for NPC queue completion.
            if (spawnManager != null)
                spawnManager.OnAllNPCsFinished += OnAllNPCsFinished;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (spawnManager != null)
                spawnManager.OnAllNPCsFinished -= OnAllNPCsFinished;

            StopPhaseCoroutine();
        }

        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Non-networked fallback: if NGO is not running (editor solo play), wire up events in Awake.
    /// </summary>
    private void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (spawnManager != null)
                spawnManager.OnAllNPCsFinished += OnAllNPCsFinished;
        }
    }

    private void OnDestroy()
    {
        if (spawnManager != null)
            spawnManager.OnAllNPCsFinished -= OnAllNPCsFinished;
    }

    // ──────────────────────────────────────────────
    // Public API (server-only unless noted)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Kick off the first day shift. Called by GameStarter (or debug tools).
    /// Server-only. Safe to call multiple times — cancels any in-progress phase.
    /// </summary>
    public void StartDayShift()
    {
        if (!IsServerOrLocal()) return;

        StopPhaseCoroutine();

        EscapedDoppelgangers.Value = 0;
        if (scoreManager != null)
            scoreManager.ResetForNewShift();
        SetPhase(ShiftPhase.DayShift);

        // Start NPC spawning with the current config.
        RoundConfig config = GetRoundConfigForNight(CurrentNight.Value);
        if (spawnManager != null && config != null)
        {
            spawnManager.StartNPCSpawning(config);
        }
        else
        {
            Debug.LogWarning("[ShiftManager] No spawn manager or round config — day shift started with no NPCs.");
        }

        OnDayShiftStarted?.Invoke();
        Debug.Log($"[ShiftManager] Day shift started (Night {CurrentNight.Value}).");
    }

    /// <summary>
    /// Called by other systems (e.g. CashRegister) when a doppelganger is approved and escapes.
    /// Server-only.
    /// </summary>
    public void ReportEscape()
    {
        if (!IsServerOrLocal()) return;

        EscapedDoppelgangers.Value++;
        Debug.Log($"[ShiftManager] Doppelganger escaped! Total: {EscapedDoppelgangers.Value}");
    }

    /// <summary>
    /// Force the shift to a specific phase. Intended for debug/testing only.
    /// Cancels any in-progress phase coroutine.
    /// </summary>
    public void ForcePhase(ShiftPhase phase)
    {
        if (!IsServerOrLocal()) return;

        StopPhaseCoroutine();
        Debug.Log($"[ShiftManager] DEBUG: Forcing phase to {phase}");

        switch (phase)
        {
            case ShiftPhase.Dawn:
                BeginDawn();
                break;
            case ShiftPhase.DayShift:
                StartDayShift();
                break;
            case ShiftPhase.Transition:
                StartTransition();
                break;
            case ShiftPhase.NightShift:
                StartNightShift();
                break;
        }
    }

    // ──────────────────────────────────────────────
    // Phase transitions (server-only)
    // ──────────────────────────────────────────────

    /// <summary>Called when all NPCs in the queue have exited.</summary>
    private void OnAllNPCsFinished()
    {
        if (!IsServerOrLocal()) return;

        // Only relevant during the day shift.
        if (Phase != ShiftPhase.DayShift)
        {
            Debug.Log($"[ShiftManager] OnAllNPCsFinished called during {Phase} — ignoring.");
            return;
        }

        Debug.Log($"[ShiftManager] All NPCs finished. Escaped doppelgangers: {EscapedDoppelgangers.Value}");

        if (EscapedDoppelgangers.Value == 0)
        {
            // Clean end — no monster tonight.
            Debug.Log("[ShiftManager] Clean shift — skipping to dawn.");
            BeginDawn();
        }
        else
        {
            // Doppelgangers escaped — transition to night.
            StartTransition();
        }
    }

    private void StartTransition()
    {
        SetPhase(ShiftPhase.Transition);
        Debug.Log($"[ShiftManager] Transition started ({transitionDuration}s).");

        // Fire a ClientRpc for one-shot effects (lights flicker, audio sting, etc.)
        if (IsSpawned)
            TriggerTransitionEffectsClientRpc();

        _phaseCoroutine = StartCoroutine(TransitionCoroutine());
    }

    private IEnumerator TransitionCoroutine()
    {
        yield return new WaitForSeconds(transitionDuration);
        StartNightShift();
    }

    private void StartNightShift()
    {
        StopPhaseCoroutine();
        SetPhase(ShiftPhase.NightShift);
        Debug.Log($"[ShiftManager] Night shift started (Night {CurrentNight.Value}, {EscapedDoppelgangers.Value} monster(s)).");

        OnNightShiftStarted?.Invoke();

        // TODO: Spawn monster(s) based on EscapedDoppelgangers.Value
        // TODO: Dim lights via ClientRpc

        if (nightDuration > 0f)
        {
            _phaseCoroutine = StartCoroutine(NightTimerCoroutine());
        }
        // If nightDuration == 0, night only ends when all monsters are killed (OnMonsterKilled).
    }

    private IEnumerator NightTimerCoroutine()
    {
        yield return new WaitForSeconds(nightDuration);
        OnDawnReached();
    }

    /// <summary>Called when dawn timer expires or all monsters are killed.</summary>
    public void OnDawnReached()
    {
        if (!IsServerOrLocal()) return;
        if (Phase != ShiftPhase.NightShift) return;

        Debug.Log("[ShiftManager] Dawn reached — ending night shift.");
        // TODO: Despawn remaining monsters (retreat behavior).
        BeginDawn();
    }

    /// <summary>Called by MonsterController when a monster dies. If all dead, end night early.</summary>
    public void OnMonsterKilled()
    {
        if (!IsServerOrLocal()) return;
        if (Phase != ShiftPhase.NightShift) return;

        // TODO: Check if all monsters are dead. If so:
        // Debug.Log("[ShiftManager] All monsters killed — ending night early.");
        // BeginDawn();
    }

    private void BeginDawn()
    {
        StopPhaseCoroutine();
        SetPhase(ShiftPhase.Dawn);

        CurrentNight.Value++;
        Debug.Log($"[ShiftManager] Dawn (preparing Night {CurrentNight.Value}). Waiting {dawnDuration}s.");

        OnShiftCycleCompleted?.Invoke();

        _phaseCoroutine = StartCoroutine(DawnCoroutine());
    }

    private IEnumerator DawnCoroutine()
    {
        yield return new WaitForSeconds(dawnDuration);
        StartDayShift();
    }

    // ──────────────────────────────────────────────
    // ClientRpcs
    // ──────────────────────────────────────────────

    [ClientRpc]
    private void TriggerTransitionEffectsClientRpc()
    {
        // Clients can subscribe to OnPhaseChanged or check Phase for their own effects.
        // This RPC is for one-shot effects that shouldn't rely on polling.
        Debug.Log("[ShiftManager] Transition effects triggered (client-side).");
        // TODO: Lights flicker, audio sting, etc.
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private void SetPhase(ShiftPhase phase)
    {
        if (IsSpawned)
            CurrentPhase.Value = (int)phase;

        OnPhaseChanged?.Invoke(phase);
    }

    /// <summary>
    /// Returns the RoundConfig for the given night number.
    /// Currently always returns the default config. Future: generate configs dynamically
    /// based on night number (more NPCs, more doppelgangers, harder recipes).
    /// </summary>
    private RoundConfig GetRoundConfigForNight(int nightNumber)
    {
        // TODO: Dynamic config generation based on nightNumber.
        return defaultRoundConfig;
    }

    /// <summary>
    /// Returns true if this is the server, or if NGO is not running (local/editor fallback).
    /// </summary>
    private bool IsServerOrLocal()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true; // Non-networked, allow everything.
        return IsServer;
    }

    private void StopPhaseCoroutine()
    {
        if (_phaseCoroutine != null)
        {
            StopCoroutine(_phaseCoroutine);
            _phaseCoroutine = null;
        }
    }

    // ──────────────────────────────────────────────
    // Debug Tools
    // ──────────────────────────────────────────────

    /// <summary>
    /// Toggle debug tools at runtime from any script:
    /// <code>shiftManager.SetDebugTools(true);</code>
    /// </summary>
    public void SetDebugTools(bool enabled)
    {
        enableDebugTools = enabled;
    }

    public bool DebugToolsEnabled => enableDebugTools;

    /// <summary>
    /// True while the debug panel is in interactive mode (F1 toggle).
    /// Other scripts (MouseLook, ObjectPickup, PlayerMovement) should check this
    /// and skip their input processing when true.
    /// </summary>
    public static bool IsDebugUIActive { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool _debugFoldout = true;

    [Header("Debug Key")]
    [Tooltip("Key to toggle debug panel interaction mode (unlocks cursor so you can click buttons).")]
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F1;

    private void Update()
    {
        if (!enableDebugTools) return;

        // F1 toggles interactive mode on/off.
        if (Input.GetKeyDown(debugToggleKey))
        {
            Debug.Log($"[ShiftManager] DEBUG: F1 pressed — toggling debug UI {(IsDebugUIActive ? "OFF" : "ON")}");
            SetDebugUIActive(!IsDebugUIActive);
        }
    }

    private void OnGUI()
    {
        if (!enableDebugTools)
        {
            if (IsDebugUIActive) SetDebugUIActive(false);
            return;
        }

        // Only show on server (or in non-networked mode).
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
        {
            if (IsDebugUIActive) SetDebugUIActive(false);
            return;
        }

        // Scale everything relative to screen height so the panel is readable at any resolution.
        float scale = Screen.height / 720f;
        float panelWidth = 400f * scale;
        float panelX = Screen.width - panelWidth - 14f * scale;
        float panelY = 14f * scale;
        float lineHeight = 36f * scale;
        float padding = 10f * scale;
        float y = panelY;
        int fontSize = Mathf.RoundToInt(16f * scale);

        // Scaled GUIStyle helpers.
        GUIStyle headerStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(18f * scale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = true
        };
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = fontSize
        };
        GUIStyle sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };

        // ── Hint label (always visible when debug tools enabled) ──
        string modeLabel = IsDebugUIActive
            ? "<b>[F1] Debug Panel — INTERACTIVE  (press F1 to return to game)</b>"
            : "<b>[F1] Debug Panel — view only  (press F1 to interact)</b>";
        GUI.Label(new Rect(panelX, y, panelWidth, lineHeight), modeLabel, labelStyle);
        y += lineHeight + padding * 0.5f;

        // Background box.
        float contentStartY = y;
        float collapsedHeight = lineHeight + padding * 2f;
        float expandedHeight = lineHeight * 12.5f + padding * 2f;
        float panelHeight = _debugFoldout ? expandedHeight : collapsedHeight;
        GUI.Box(new Rect(panelX - padding, contentStartY - padding, panelWidth + padding * 2f, panelHeight), "");

        // Header / foldout toggle — only responds to clicks when interactive.
        bool headerClicked;
        if (IsDebugUIActive)
        {
            headerClicked = GUI.Button(new Rect(panelX, y, panelWidth, lineHeight),
                _debugFoldout ? "▼ Shift Manager Debug" : "► Shift Manager Debug", headerStyle);
        }
        else
        {
            GUI.Label(new Rect(panelX, y, panelWidth, lineHeight),
                _debugFoldout ? "▼ Shift Manager Debug" : "► Shift Manager Debug", headerStyle);
            headerClicked = false;
        }
        if (headerClicked)
        {
            _debugFoldout = !_debugFoldout;
            Debug.Log($"[ShiftManager] DEBUG: Foldout toggled → {(_debugFoldout ? "open" : "closed")}");
        }
        y += lineHeight + padding * 0.5f;

        if (!_debugFoldout) return;

        // ── Status display (always visible) ──
        GUI.Label(new Rect(panelX, y, panelWidth, lineHeight),
            $"Phase: <b>{Phase}</b>   |   Night: <b>{CurrentNight.Value}</b>", labelStyle);
        y += lineHeight;

        GUI.Label(new Rect(panelX, y, panelWidth, lineHeight),
            $"Escaped Doppelgangers: <b>{EscapedDoppelgangers.Value}</b>", labelStyle);
        y += lineHeight;

        bool spawning = spawnManager != null && spawnManager.IsSpawning;
        GUI.Label(new Rect(panelX, y, panelWidth, lineHeight),
            $"NPC Spawning: <b>{(spawning ? "Active" : "Idle")}</b>", labelStyle);
        y += lineHeight + padding;

        // ── All buttons below only work in interactive mode ──
        if (!IsDebugUIActive)
        {
            GUI.Label(new Rect(panelX, y, panelWidth, lineHeight),
                "Press F1 to enable buttons", sectionStyle);
            return;
        }

        // ── Phase buttons ──
        GUI.Label(new Rect(panelX, y, panelWidth, lineHeight), "Force Phase:", sectionStyle);
        y += lineHeight * 0.7f;

        float halfWidth = (panelWidth - padding * 0.5f) / 2f;

        if (GUI.Button(new Rect(panelX, y, halfWidth, lineHeight), "Dawn", buttonStyle))
        {
            Debug.Log("[ShiftManager] DEBUG: Button pressed → Force Dawn");
            ForcePhase(ShiftPhase.Dawn);
        }
        if (GUI.Button(new Rect(panelX + halfWidth + padding * 0.5f, y, halfWidth, lineHeight), "Day Shift", buttonStyle))
        {
            Debug.Log("[ShiftManager] DEBUG: Button pressed → Force Day Shift");
            ForcePhase(ShiftPhase.DayShift);
        }
        y += lineHeight + 2f * scale;

        if (GUI.Button(new Rect(panelX, y, halfWidth, lineHeight), "Transition", buttonStyle))
        {
            Debug.Log("[ShiftManager] DEBUG: Button pressed → Force Transition");
            ForcePhase(ShiftPhase.Transition);
        }
        if (GUI.Button(new Rect(panelX + halfWidth + padding * 0.5f, y, halfWidth, lineHeight), "Night Shift", buttonStyle))
        {
            Debug.Log("[ShiftManager] DEBUG: Button pressed → Force Night Shift");
            ForcePhase(ShiftPhase.NightShift);
        }
        y += lineHeight + padding;

        // ── Doppelganger controls ──
        GUI.Label(new Rect(panelX, y, panelWidth, lineHeight), "Escaped Doppelgangers:", sectionStyle);
        y += lineHeight * 0.7f;

        float btnW = 90f * scale;
        float btnGap = 8f * scale;

        if (GUI.Button(new Rect(panelX, y, btnW, lineHeight), "- 1", buttonStyle))
        {
            if (EscapedDoppelgangers.Value > 0)
                EscapedDoppelgangers.Value--;
            Debug.Log($"[ShiftManager] DEBUG: EscapedDoppelgangers → {EscapedDoppelgangers.Value}");
        }
        if (GUI.Button(new Rect(panelX + btnW + btnGap, y, btnW, lineHeight), "+ 1", buttonStyle))
        {
            EscapedDoppelgangers.Value++;
            Debug.Log($"[ShiftManager] DEBUG: EscapedDoppelgangers → {EscapedDoppelgangers.Value}");
        }
        if (GUI.Button(new Rect(panelX + (btnW + btnGap) * 2f, y, btnW, lineHeight), "Reset", buttonStyle))
        {
            EscapedDoppelgangers.Value = 0;
            Debug.Log("[ShiftManager] DEBUG: EscapedDoppelgangers → 0");
        }
        y += lineHeight + padding;

        // ── Night counter ──
        GUI.Label(new Rect(panelX, y, panelWidth, lineHeight), $"Night Counter: {CurrentNight.Value}", sectionStyle);
        y += lineHeight * 0.7f;

        if (GUI.Button(new Rect(panelX, y, btnW, lineHeight), "- 1", buttonStyle))
        {
            if (CurrentNight.Value > 1)
                CurrentNight.Value--;
            Debug.Log($"[ShiftManager] DEBUG: CurrentNight → {CurrentNight.Value}");
        }
        if (GUI.Button(new Rect(panelX + btnW + btnGap, y, btnW, lineHeight), "+ 1", buttonStyle))
        {
            CurrentNight.Value++;
            Debug.Log($"[ShiftManager] DEBUG: CurrentNight → {CurrentNight.Value}");
        }
        if (GUI.Button(new Rect(panelX + (btnW + btnGap) * 2f, y, btnW + 20f * scale, lineHeight), "Reset to 1", buttonStyle))
        {
            CurrentNight.Value = 1;
            Debug.Log("[ShiftManager] DEBUG: CurrentNight → 1");
        }
        y += lineHeight + padding;

        // ── Quick actions ──
        if (GUI.Button(new Rect(panelX, y, panelWidth, lineHeight), "Skip Current Timer (Advance Phase)", buttonStyle))
        {
            Debug.Log("[ShiftManager] DEBUG: Button pressed → Skip Current Timer");
            DebugSkipCurrentTimer();
        }
    }

    /// <summary>
    /// Immediately completes whatever timer/coroutine is running and advances to the next phase.
    /// </summary>
    private void DebugSkipCurrentTimer()
    {
        if (!IsServerOrLocal()) return;

        StopPhaseCoroutine();

        switch (Phase)
        {
            case ShiftPhase.Dawn:
                Debug.Log("[ShiftManager] DEBUG: Skipping dawn timer → Day Shift.");
                StartDayShift();
                break;

            case ShiftPhase.DayShift:
                Debug.Log("[ShiftManager] DEBUG: Forcing all NPCs finished.");
                if (spawnManager != null)
                    spawnManager.StopNPCSpawning();
                OnAllNPCsFinished();
                break;

            case ShiftPhase.Transition:
                Debug.Log("[ShiftManager] DEBUG: Skipping transition → Night Shift.");
                StartNightShift();
                break;

            case ShiftPhase.NightShift:
                Debug.Log("[ShiftManager] DEBUG: Skipping night timer → Dawn.");
                OnDawnReached();
                break;
        }
    }

    /// <summary>
    /// Toggles cursor visibility and the static input-block flag.
    /// </summary>
    private void SetDebugUIActive(bool active)
    {
        if (active == IsDebugUIActive) return;

        IsDebugUIActive = active;
        Debug.Log($"[ShiftManager] DEBUG: IsDebugUIActive → {active} | Cursor → {(active ? "Unlocked" : "Locked")}");

        if (active)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        if (IsDebugUIActive) SetDebugUIActive(false);
    }
#endif
}
