# Unity → s&box Migration Guide

> A practical reference for Unity developers porting C# code and GameObjects to the s&box engine.  
> Based on the [s&box developer documentation](https://sbox.game/dev/doc).

---

## Table of Contents

1. [Mental Model Shift](#mental-model-shift)
2. [Project & File Structure](#project--file-structure)
3. [MonoBehaviour → Component](#monobehaviour--component)
4. [Lifecycle Methods](#lifecycle-methods)
5. [Serialized Fields → `[Property]`](#serialized-fields--property)
6. [GameObjects & Hierarchy](#gameobjects--hierarchy)
7. [Finding Objects & Components](#finding-objects--components)
8. [Instantiate & Destroy](#instantiate--destroy)
9. [Physics & Raycasting](#physics--raycasting)
10. [Input](#input)
11. [UI (uGUI / UI Toolkit → Razor Panels)](#ui-ugui--ui-toolkit--razor-panels)
12. [Coroutines & Async](#coroutines--async)
13. [Networking](#networking)
14. [Global Managers (Singletons)](#global-managers-singletons)
15. [Tags & Layers](#tags--layers)
16. [Asset References](#asset-references)
17. [Common API Cheat Sheet](#common-api-cheat-sheet)
18. [Things That Don't Exist in s&box](#things-that-dont-exist-in-sbox)

---

## Mental Model Shift

The two engines share a lot of surface-level vocabulary — both use **GameObjects**, **Components**, **Scenes**, and **Prefabs** — but there are important differences under the hood.

| Concept | Unity | s&box |
|---|---|---|
| Script base class | `MonoBehaviour` | `Component` |
| Inspector exposure | `[SerializeField]` / `public` | `[Property]` |
| Scene file format | `.unity` (YAML) | `.scene` (JSON) |
| Prefab format | `.prefab` (YAML) | `.prefab` (JSON) |
| UI system | uGUI / UI Toolkit | Razor + SCSS panels |
| Networking | Netcode / Mirror / Fishnet (addon) | Built-in, host-authoritative |
| Coroutines | `IEnumerator` / `StartCoroutine` | `async / await` (plain .NET tasks) |
| Physics engine | PhysX | Source 2 / Havok |
| Shader language | ShaderLab / HLSL | `.vfx` (Source 2 shader) |
| Asset pipeline | Asset database | ResourceLibrary |
| Global managers | Static classes or Singletons | `GameObjectSystem<T>` |

The biggest conceptual shift: in Unity, `MonoBehaviour` **is** the script and it manages its own state heavily. In s&box, `Component` is a lightweight logic block — state management is leaner, and the engine leans heavily on C# attributes (`[Property]`, `[Sync]`, `[Rpc.*]`) to handle serialization, networking, and tooling automatically.

---

## Project & File Structure

### Unity
```
Assets/
  Scripts/
    PlayerController.cs
  Scenes/
    Main.unity
  Prefabs/
    Player.prefab
```

### s&box
```
/code/
  PlayerController.cs
/scenes/
  main.scene
/prefabs/
  player.prefab
/ui/
  HudPanel.razor
  HudPanel.razor.scss
myproject.sbproj
```

The `.sbproj` file is the project root (equivalent to Unity's `.unityproject` folder). All paths in code are relative to the project root.

---

## MonoBehaviour → Component

This is the most direct translation. Strip `MonoBehaviour`, use `Component`. The namespace changes from `UnityEngine` to `Sandbox`.

### Unity
```csharp
using UnityEngine;

public class HealthComponent : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        if ( currentHealth <= 0f )
        {
            Destroy( gameObject );
        }
    }

    public void TakeDamage( float amount )
    {
        currentHealth -= amount;
    }
}
```

### s&box
```csharp
using Sandbox;

public sealed class HealthComponent : Component
{
    [Property] public float MaxHealth { get; set; } = 100f;
    private float currentHealth;

    protected override void OnAwake()
    {
        currentHealth = MaxHealth;
    }

    protected override void OnUpdate()
    {
        if ( currentHealth <= 0f )
        {
            GameObject.Destroy();
        }
    }

    public void TakeDamage( float amount )
    {
        currentHealth -= amount;
    }
}
```

**Key differences:**
- `MonoBehaviour` → `Component` (sealed is recommended)
- `using UnityEngine` → `using Sandbox`
- Lifecycle methods are `protected override` instead of implicitly called
- `Destroy(gameObject)` → `GameObject.Destroy()` or the helper `DestroyGameObject()`
- Public fields for serialization → `[Property]` auto-properties

---

## Lifecycle Methods

The lifecycle is conceptually identical, but the method names differ slightly.

| Unity | s&box | Notes |
|---|---|---|
| `Awake()` | `OnAwake()` | Once, on first enable. Cache references here. |
| `OnEnable()` | `OnEnabled()` | Every time the component becomes active. |
| `Start()` | `OnStart()` | Once, before first Update. All siblings have awoken. |
| `Update()` | `OnUpdate()` | Every frame. `Time.deltaTime` → `Time.Delta` |
| `FixedUpdate()` | `OnFixedUpdate()` | Every physics tick. Use for Rigidbody logic. |
| `LateUpdate()` | `OnPreRender()` | Runs after update, before rendering. |
| `OnDisable()` | `OnDisabled()` | Every time the component becomes inactive. |
| `OnDestroy()` | `OnDestroy()` | Same name. Called once on permanent removal. |
| `OnValidate()` | `OnValidate()` | Same name. Called after inspector changes. |
| `Reset()` | *(automatic)* | `[Property]` defaults are restored automatically. |

### Unity
```csharp
void Awake()        { }
void OnEnable()     { }
void Start()        { }
void Update()       { float dt = Time.deltaTime; }
void FixedUpdate()  { }
void LateUpdate()   { }
void OnDisable()    { }
void OnDestroy()    { }
```

### s&box
```csharp
protected override void OnAwake()       { }
protected override void OnEnabled()     { }
protected override void OnStart()       { }
protected override void OnUpdate()      { float dt = Time.Delta; }
protected override void OnFixedUpdate() { }
protected override void OnPreRender()   { }
protected override void OnDisabled()    { }
protected override void OnDestroy()     { }
```

> **Important:** In s&box, lifecycle methods that run on a dedicated server (`OnFixedUpdate`) behave differently from client-only methods (`OnUpdate`). Components can implement `DontExecuteOnServer` to skip server execution entirely, or `ExecuteInEditor` to run in editor preview scenes.

---

## Serialized Fields → `[Property]`

In Unity you expose fields to the Inspector with `[SerializeField]` on private fields or by making them `public`. In s&box, use `[Property]` on an auto-property.

### Unity
```csharp
[SerializeField] private float speed = 200f;
public GameObject target;
[SerializeField, Range(0, 10)] private int lives = 3;
[Header("Combat")] public float damage = 10f;
[Tooltip("Seconds before respawn")] public float respawnDelay = 5f;
```

### s&box
```csharp
[Property] public float Speed { get; set; } = 200f;
[Property] public GameObject Target { get; set; }
[Property, Range( 0, 10 )] public int Lives { get; set; } = 3;
[Property, Group( "Combat" )] public float Damage { get; set; } = 10f;
[Property, Title( "Respawn Delay" )] public float RespawnDelay { get; set; } = 5f;
```

**Attribute mapping:**

| Unity | s&box |
|---|---|
| `[SerializeField]` | `[Property]` |
| `public` (implicit serialization) | `[Property]` |
| `[HideInInspector]` | *(omit `[Property]`)* |
| `[Header("name")]` | `[Group("name")]` |
| `[Range(min, max)]` | `[Range(min, max)]` (same) |
| `[Tooltip("...")]` | `[Title("...")]` (label) or XML doc comment |
| `[TextArea]` | `[TextArea]` (same) |
| `[Space]` | *(use groups)* |

---

## GameObjects & Hierarchy

The `GameObject` API is very similar. The main differences are method naming and how the scene root is accessed.

### Unity
```csharp
// Create
var go = new GameObject( "Enemy" );

// Parent
go.transform.SetParent( parentTransform );
go.transform.SetParent( parentTransform, worldPositionStays: true );

// Enable / disable
go.SetActive( false );
bool isActive = go.activeInHierarchy;

// Tags
go.tag = "Enemy";
bool isEnemy = go.CompareTag( "Enemy" );

// Don't destroy on load
DontDestroyOnLoad( go );

// Destroy
Destroy( go );
Destroy( go, 2f );  // delayed
```

### s&box
```csharp
// Create
var go = new GameObject( "Enemy" );
// or: var go = Game.ActiveScene.CreateObject();

// Parent
go.SetParent( parentGo, keepWorldPosition: true );
go.Parent = parentGo;

// Enable / disable
go.Enabled = false;
bool isActive = go.Active;  // true only if self AND all ancestors enabled

// Tags (now a set, not a single string)
go.Tags.Add( "enemy" );
bool isEnemy = go.Tags.Has( "enemy" );

// Don't destroy on load
go.Flags = GameObjectFlags.DontDestroyOnLoad;

// Destroy
go.Destroy();
// Delayed: use async/await (see Coroutines section)
```

> **Note:** In s&box `Parent` accepts a `GameObject`, not a `Transform`. The transform and GameObject are not separate objects.

---

## Finding Objects & Components

### Unity
```csharp
// Find by name (slow!)
var go = GameObject.Find( "Player" );

// Find by tag
var go = GameObject.FindWithTag( "Player" );
var all = GameObject.FindGameObjectsWithTag( "Enemy" );

// Find component on self
var rb = GetComponent<Rigidbody>();
var rb = GetComponentInChildren<Rigidbody>();
var rb = GetComponentInParent<Rigidbody>();

// Find all of type in scene (slow!)
var all = FindObjectsOfType<EnemyAI>();
```

### s&box
```csharp
// Find by tag
var go = Scene.FindAllWithTag( "player" ).FirstOrDefault();
var all = Scene.FindAllWithTag( "enemy" );

// Find component on self
var rb = GetComponent<Rigidbody>();          // same method name!
var rb = GetComponentInChildren<Rigidbody>();
var rb = GetComponentInParent<Rigidbody>();

// Include disabled components
var rb = GetComponent<Rigidbody>( includeDisabled: true );

// Find all of type in scene
var all = Scene.GetAllComponents<EnemyAI>();

// Get or add
var health = go.GetOrAddComponent<HealthComponent>();
```

> **No `GameObject.Find( "name" )`** in s&box — use tags, component queries, or `[Property]` references set in the editor. This is actually better practice in Unity too.

---

## Instantiate & Destroy

### Unity
```csharp
// Instantiate from prefab
var instance = Instantiate( prefab );
var instance = Instantiate( prefab, position, rotation );
var instance = Instantiate( prefab, parentTransform );

// Destroy
Destroy( gameObject );
Destroy( gameObject, 2f );   // delayed by 2 seconds
DestroyImmediate( gameObject );
```

### s&box
```csharp
// Instantiate from prefab (preferred)
var instance = SceneUtility.Instantiate(
    ResourceLibrary.Get<PrefabFile>( "prefabs/enemy.prefab" ),
    Transform.World
);

// Or clone from a loaded prefab root
var prefabRoot = GameObject.GetPrefab( "prefabs/enemy.prefab" );
var instance   = prefabRoot.Clone( Transform.World );
instance.Parent = Game.ActiveScene;

// Destroy
gameObject.Destroy();          // queued, end of frame
DestroyGameObject();           // helper from within a Component

// Validity check (equivalent to Unity's null check after Destroy)
if ( go.IsValid() ) { }

// Delayed destroy via async
async void DestroyAfterDelay( GameObject go, float seconds )
{
    await Task.Delay( (int)(seconds * 1000) );
    if ( go.IsValid() ) go.Destroy();
}
```

> In Unity, destroyed objects become a special `null` that passes `== null`. In s&box, use `go.IsValid()` for the same check — destroyed objects are not null, they are invalid.

---

## Physics & Raycasting

### Unity
```csharp
// Raycast
if ( Physics.Raycast( origin, direction, out RaycastHit hit, maxDistance ) )
{
    Debug.Log( hit.point );
    Debug.Log( hit.normal );
    Debug.Log( hit.collider.gameObject.name );
}

// Raycast from camera
Ray ray = Camera.main.ScreenPointToRay( Input.mousePosition );
Physics.Raycast( ray, out RaycastHit hit );

// SphereCast
Physics.SphereCast( origin, radius, direction, out hit, maxDistance );

// All hits
RaycastHit[] hits = Physics.RaycastAll( origin, direction, maxDistance );

// Rigidbody
var rb = GetComponent<Rigidbody>();
rb.velocity = Vector3.up * 5f;
rb.AddForce( Vector3.forward * 100f );
rb.AddForce( Vector3.forward * 100f, ForceMode.Impulse );
rb.mass = 10f;
rb.drag = 0.5f;
rb.angularDrag = 0.5f;
rb.useGravity = false;

// Collision callbacks
void OnCollisionEnter( Collision c ) { }
void OnCollisionStay( Collision c )  { }
void OnCollisionExit( Collision c )  { }
```

### s&box
```csharp
// Raycast
var tr = Scene.PhysicsWorld.Trace
    .Ray( origin, origin + direction * maxDistance )
    .Run();

if ( tr.Hit )
{
    Log.Info( tr.HitPosition );
    Log.Info( tr.Normal );
    Log.Info( tr.Body?.GameObject?.Name );
}

// Raycast from camera center
var ray = Scene.Camera.ScreenNormalToRay( 0.5f, 0.5f );
var tr  = Scene.PhysicsWorld.Trace.Ray( ray, 2000f ).Run();

// Sphere sweep
var tr = Scene.PhysicsWorld.Trace
    .Sphere( radius: 0.5f, from: origin, to: origin + direction * maxDistance )
    .Run();

// All hits
var hits = Scene.PhysicsWorld.Trace.Ray( start, end ).RunAll();

// Trace filters
var tr = Scene.PhysicsWorld.Trace
    .Ray( start, end )
    .IgnoreStatic()
    .HitTriggers()
    .Run();

// Rigidbody
var rb = GetComponent<Rigidbody>();
rb.Velocity        = Vector3.Up * 5f;
rb.ApplyForce( Vector3.Forward * 100f );
rb.ApplyImpulse( Vector3.Forward * 100f );  // ForceMode.Impulse equivalent
rb.MassOverride    = 10f;
rb.LinearDamping   = 0.5f;
rb.AngularDamping  = 0.5f;
rb.Gravity         = false;

// Collision callbacks — implement the interface
public sealed class MyComp : Component, Component.ICollisionListener
{
    void ICollisionListener.OnCollisionStart( Collision c )  { }
    void ICollisionListener.OnCollisionUpdate( Collision c ) { }
    void ICollisionListener.OnCollisionEnd( CollisionStop s ){ }
}
```

**Trace result field mapping:**

| Unity `RaycastHit` | s&box `PhysicsTraceResult` |
|---|---|
| `hit.point` | `tr.HitPosition` |
| `hit.normal` | `tr.Normal` |
| `hit.distance` | `tr.Distance` |
| `hit.fraction` | `tr.Fraction` |
| `hit.collider.gameObject` | `tr.Body?.GameObject` |
| `hit.collider` | `tr.Shape` |

---

## Input

### Unity
```csharp
// Old Input system
if ( Input.GetButton( "Fire1" ) )       { }  // held
if ( Input.GetButtonDown( "Fire1" ) )   { }  // pressed
if ( Input.GetButtonUp( "Fire1" ) )     { }  // released

float h = Input.GetAxis( "Horizontal" );
float v = Input.GetAxis( "Vertical" );
Vector2 mouse = new Vector2( Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y") );

// New Input System (InputAction)
playerInput.actions["Fire"].IsPressed()
```

### s&box
```csharp
// Identical Down / Pressed / Released pattern
if ( Input.Down( "Attack1" ) )    { }  // held
if ( Input.Pressed( "Jump" ) )    { }  // this frame only
if ( Input.Released( "Duck" ) )   { }  // released this frame

// Axes → Analog inputs
Vector3 moveDir = Input.AnalogMove;          // replaces GetAxis("Horizontal/Vertical")
Angles  look    = Input.AnalogLook;          // replaces Mouse X/Y, respects sensitivity
Vector2 mouse   = Input.MouseDelta;          // raw mouse pixels
float   scroll  = Input.MouseWheel.y;

// Cursor visibility
if ( Input.MouseCursorVisible ) return;

// Programmatic
Input.SetAction( "Jump", true );
Input.Clear( "Jump" );
```

**Default action name mapping:**

| Unity default | s&box equivalent |
|---|---|
| `"Fire1"` | `"Attack1"` |
| `"Fire2"` | `"Attack2"` |
| `"Jump"` | `"Jump"` |
| `"Horizontal"` | `Input.AnalogMove.y` (left/right) |
| `"Vertical"` | `Input.AnalogMove.x` (forward/back) |
| `"Mouse X"` | `Input.AnalogLook.yaw` |
| `"Mouse Y"` | `Input.AnalogLook.pitch` |

> s&box actions are defined as strings in **Project Settings → Input**, just like Unity's Input Manager. Players can rebind them; your code never references raw keycodes.

---

## UI (uGUI / UI Toolkit → Razor Panels)

This is the biggest workflow change. s&box uses **Razor** (`.razor`) markup files with **SCSS** stylesheets — similar to Blazor or React, not Unity's Canvas/UXML approach.

### Unity (uGUI)
```
Canvas (GameObject)
  └── Panel (Image)
        ├── HealthBar (Slider)
        └── HealthText (Text)
```
```csharp
public class HealthHUD : MonoBehaviour
{
    public Slider healthBar;
    public Text   healthText;

    void Update()
    {
        healthBar.value = player.health / player.maxHealth;
        healthText.text = $"{player.health} HP";
    }
}
```

### s&box
Three files, same base name:

**`HealthHud.cs`**
```csharp
public sealed class HealthHud : PanelComponent
{
    PlayerController player;

    protected override void OnStart()
    {
        player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
    }

    float Health    => player?.Health    ?? 0;
    float MaxHealth => player?.MaxHealth ?? 100;

    // Re-render whenever these values change
    protected override int BuildHash()
        => HashCode.Combine( Health, MaxHealth );
}
```

**`HealthHud.razor`**
```razor
@inherits PanelComponent

<root>
    <div class="health-bar">
        <div class="fill" style="width: @Pct%"></div>
        <label>@Health.FloorToInt() HP</label>
    </div>
</root>

@code {
    float Pct => (Health / MaxHealth * 100f).Clamp( 0, 100 );
}
```

**`HealthHud.razor.scss`**
```scss
.health-bar {
    width: 240px;
    height: 20px;
    background-color: rgba(0,0,0,0.6);
    border-radius: 4px;
    overflow: hidden;
}
.fill {
    height: 100%;
    background-color: #44cc44;
    transition-duration: 0.2s;
}
```

**Setup in scene:** Add a `ScreenPanel` component to a GameObject, then add your `HealthHud` component to the same or a child GameObject.

**Key differences from Unity UI:**
- No Canvas GameObject required — `ScreenPanel` component handles the root
- No `Update()` polling — re-render is triggered by `BuildHash()` returning a new value
- Layout is CSS flexbox, not Unity's RectTransform anchors
- For 3D world-space UI, use `WorldPanel` instead of `ScreenPanel` (replaces World Space Canvas)

---

## Coroutines & Async

Unity coroutines (`IEnumerator` / `StartCoroutine`) are replaced with standard C# `async/await`.

### Unity
```csharp
void Start()
{
    StartCoroutine( SpawnLoop() );
    StartCoroutine( DelayedAction( 2f ) );
}

IEnumerator SpawnLoop()
{
    while ( true )
    {
        SpawnEnemy();
        yield return new WaitForSeconds( 5f );
    }
}

IEnumerator DelayedAction( float delay )
{
    yield return new WaitForSeconds( delay );
    DoSomething();
}

IEnumerator WaitForCondition()
{
    yield return new WaitUntil( () => isReady );
    DoSomething();
}
```

### s&box
```csharp
protected override void OnStart()
{
    _ = SpawnLoop();
    _ = DelayedAction( 2f );
}

async Task SpawnLoop()
{
    while ( true )
    {
        SpawnEnemy();
        await Task.Delay( 5000 );  // milliseconds
    }
}

async Task DelayedAction( float delay )
{
    await Task.Delay( (int)(delay * 1000) );
    DoSomething();
}

async Task WaitForCondition()
{
    while ( !isReady )
        await Task.Yield();
    DoSomething();
}
```

**Coroutine / yield mapping:**

| Unity | s&box |
|---|---|
| `yield return null` | `await Task.Yield()` |
| `yield return new WaitForSeconds(n)` | `await Task.Delay( (int)(n * 1000) )` |
| `yield return new WaitForFixedUpdate()` | `await Task.Yield()` inside `OnFixedUpdate` |
| `yield return new WaitUntil(() => cond)` | `while (!cond) await Task.Yield()` |
| `StopCoroutine(...)` | Use `CancellationToken` |
| `StartCoroutine(...)` | `_ = MyAsyncMethod()` |

---

## Networking

Unity has no built-in multiplayer — you need Netcode for GameObjects, Mirror, or a similar package. s&box has networking built in with a simple attribute-based API.

### Unity (Netcode for GameObjects)
```csharp
using Unity.Netcode;

public class PlayerHealth : NetworkBehaviour
{
    private NetworkVariable<float> health =
        new NetworkVariable<float>( 100f, NetworkVariableReadPermission.Everyone );

    [ServerRpc]
    void TakeDamageServerRpc( float amount ) { }

    [ClientRpc]
    void PlayHitEffectClientRpc() { }

    public override void OnNetworkSpawn()
    {
        if ( IsOwner ) { }
        if ( IsServer ) { }
    }
}
```

### s&box
```csharp
using Sandbox;

public sealed class PlayerHealth : Component
{
    // Automatically replicated from owner to all clients
    [Sync] public float Health { get; set; } = 100f;

    // Called only on the host
    [Rpc.Host]
    public void TakeDamage( float amount )
    {
        Health -= amount;
    }

    // Called on all clients
    [Rpc.Broadcast]
    public void PlayHitEffect()
    {
        Sound.Play( "player_hit" );
    }

    protected override void OnStart()
    {
        if ( IsProxy ) return;  // equivalent to !IsOwner
        // Owner-only setup
    }
}
```

**Networking concept mapping:**

| Unity (Netcode) | s&box |
|---|---|
| `NetworkBehaviour` | `Component` (networking is built in) |
| `NetworkVariable<T>` | `[Sync] public T Prop { get; set; }` |
| `[ServerRpc]` | `[Rpc.Host]` |
| `[ClientRpc]` | `[Rpc.Broadcast]` |
| `IsOwner` | `!IsProxy` |
| `IsServer` | `Networking.IsHost` |
| `IsClient` | `Networking.IsClient` |
| `NetworkManager.Singleton` | `Networking` (static class) |
| `NetworkObject.Spawn()` | `go.NetworkSpawn( connection )` |
| `NetworkObject.Despawn()` | `go.Destroy()` |

> In s&box, set `GameObject.NetworkMode = NetworkMode.Object` in the Inspector (or in code) before calling `NetworkSpawn`. Only the host can spawn networked objects — clients must request the host to do it via `[Rpc.Host]`.

---

## Global Managers (Singletons)

### Unity
```csharp
// Common singleton pattern
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if ( Instance != null ) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad( gameObject );
    }
}

// Usage
GameManager.Instance.StartGame();
```

### s&box
```csharp
// GameObjectSystem — created automatically per scene, no boilerplate
public class GameManager : GameObjectSystem<GameManager>
{
    public GameManager( Scene scene ) : base( scene )
    {
        Listen( Stage.StartUpdate, 0, Tick, "GameManager" );
    }

    void Tick() { }

    public void StartGame() { }
}

// Usage — from anywhere
GameManager.Current.StartGame();
// or
Scene.GetSystem<GameManager>().StartGame();
```

`GameObjectSystem<T>` replaces the Unity singleton pattern entirely. It is:
- Automatically instantiated when the scene starts
- Automatically disposed when the scene ends
- Accessible via `T.Current` or `Scene.GetSystem<T>()`
- No `DontDestroyOnLoad` hack required

---

## Tags & Layers

### Unity
```csharp
// Single tag (string)
go.tag = "Enemy";
if ( go.CompareTag( "Enemy" ) ) { }

// Layer (int)
go.layer = LayerMask.NameToLayer( "Enemies" );
Physics.Raycast( ray, out hit, 100f, LayerMask.GetMask( "Enemies" ) );
```

### s&box
```csharp
// Tags are a set — multiple tags per object
go.Tags.Add( "enemy" );
go.Tags.Add( "boss" );
bool isEnemy = go.Tags.Has( "enemy" );
bool isBoss  = go.Tags.HasAll( new[] { "enemy", "boss" } );

// Tag-based scene queries
var allEnemies = Scene.FindAllWithTag( "enemy" );

// Physics trace filtering — use tag filters on shapes rather than layers
var tr = Scene.PhysicsWorld.Trace
    .Ray( start, end )
    .IgnoreStatic()
    .Run();
// Check tr.Tags on the result for hit-shape tags
```

> s&box does not have a direct equivalent to Unity's physics **layers**. Filtering in traces is done with `.IgnoreStatic()`, `.IgnoreDynamic()`, `.HitTriggers()`, and similar builder methods rather than integer layer masks.

---

## Asset References

### Unity
```csharp
// Assign in Inspector (drag-and-drop)
public GameObject enemyPrefab;
public AudioClip  fireSound;
public Material   material;

// Load from Resources folder (avoid in modern Unity)
var prefab = Resources.Load<GameObject>( "Prefabs/Enemy" );
```

### s&box
```csharp
// Direct property reference — assign in editor
[Property] public PrefabFile EnemyPrefab  { get; set; }
[Property] public SoundFile  FireSound    { get; set; }
[Property] public Material   Mat          { get; set; }
[Property] public Model      WeaponModel  { get; set; }

// Deferred reference (load on demand)
[Property] public FileReference<PrefabFile> EnemyPrefabRef { get; set; }
var prefab = EnemyPrefabRef.GetAsset();

// Load by path at runtime
var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/enemy.prefab" );
var model  = Model.Load( "models/enemy/enemy.vmdl" );
var sound  = SoundFile.Load( "sounds/weapons/fire.vsnd" );
```

**Asset type mapping:**

| Unity | s&box | Extension |
|---|---|---|
| `GameObject` (prefab) | `PrefabFile` | `.prefab` |
| `AudioClip` | `SoundFile` | `.vsnd` |
| `Material` | `Material` | `.vmat` |
| `Mesh` / `Model` | `Model` | `.vmdl` |
| `Texture2D` | `Texture` | `.vtex` |
| `AnimatorController` | `AnimationGraph` | `.vanmgrph` |
| `ScriptableObject` | `GameResource` | custom extension |
| `Scene` (reference) | `SceneFile` | `.scene` |

---

## Common API Cheat Sheet

| Unity | s&box |
|---|---|
| `Debug.Log( msg )` | `Log.Info( msg )` |
| `Debug.LogWarning( msg )` | `Log.Warning( msg )` |
| `Debug.LogError( msg )` | `Log.Error( msg )` |
| `Time.deltaTime` | `Time.Delta` |
| `Time.fixedDeltaTime` | `Time.Delta` (inside `OnFixedUpdate`) |
| `Time.time` | `Time.Now` |
| `Time.timeScale` | `Scene.TimeScale` |
| `transform.position` | `WorldPosition` (on Component) or `Transform.Position` |
| `transform.rotation` | `WorldRotation` |
| `transform.localPosition` | `Transform.LocalPosition` |
| `transform.localScale` | `Transform.LocalScale` |
| `transform.forward` | `WorldRotation.Forward` |
| `transform.right` | `WorldRotation.Right` |
| `transform.up` | `WorldRotation.Up` |
| `Quaternion.Euler(x,y,z)` | `Rotation.From( pitch, yaw, roll )` |
| `Vector3.Distance(a, b)` | `a.Distance( b )` |
| `Vector3.Lerp(a,b,t)` | `Vector3.Lerp( a, b, t )` (same) |
| `Mathf.Clamp(v,min,max)` | `v.Clamp( min, max )` (extension method) |
| `Mathf.Lerp(a,b,t)` | `MathX.Lerp( a, b, t )` |
| `Mathf.Approximately(a,b)` | `a.AlmostEqual( b )` |
| `Screen.width / height` | `Screen.Width / Screen.Height` |
| `Camera.main` | `Scene.Camera` |
| `Application.isPlaying` | `Game.IsPlaying` |
| `Application.Quit()` | `Game.Close()` |
| `PlayerPrefs.SetFloat(k,v)` | `[ConVar( Saved = true )]` or `FileSystem` |

---

## Things That Don't Exist in s&box

These Unity features have no direct equivalent and need a different approach:

| Unity Feature | s&box Alternative |
|---|---|
| `GameObject.Find("name")` | Use `[Property]` references or tag queries |
| Physics layers / layer masks | Trace filter methods (`.IgnoreStatic()`, etc.) |
| `Resources.Load<T>(path)` | `ResourceLibrary.Get<T>(path)` |
| `ScriptableObject` | `GameResource` (inherit from it) |
| `IEnumerator` coroutines | `async/await` with `Task` |
| `MonoBehaviour.Invoke(method, delay)` | `async Task` with `Task.Delay` |
| Unity Events (`UnityEvent`) | C# `Action` delegates or the `Event` system |
| `Canvas` / `RectTransform` UI | Razor panels + SCSS |
| `Animator` / `AnimatorController` | `AnimationGraph` (`.vanmgrph`) |
| `NavMeshAgent` | No built-in equivalent yet — use physics/custom pathfinding |
| `AddComponent<T>()` at edit time | Done via Inspector |
| `[RequireComponent(typeof(T))]` | `go.GetOrAddComponent<T>()` in `OnAwake` |
| `OnTriggerEnter` / `OnTriggerExit` | `.HitTriggers()` on traces; or `ICollisionListener` |
| `Physics.OverlapSphere` | `.Sphere( r, from, to ).RunAll()` with zero-length sweep |
| `AssetDatabase` (editor scripting) | s&box editor extensions (separate topic) |

---

*Based on [s&box Developer Documentation](https://sbox.game/dev/doc) · [github.com/Facepunch/sbox-public](https://github.com/Facepunch/sbox-public)*