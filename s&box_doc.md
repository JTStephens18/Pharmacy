# s&box Developer Reference

> Generated from official s&box documentation at [sbox.game/dev/doc](https://sbox.game/dev/doc)  
> Source: [Facepunch/sbox-public](https://github.com/Facepunch/sbox-public) — last updated **March 29, 2026**

---

## Table of Contents

1. [Overview](#overview)
2. [Installation](#installation)
3. [Scenes & GameObjects](#scenes--gameobjects)
4. [Components](#components)
5. [Component Lifecycle](#component-lifecycle)
6. [Prefabs & Resources](#prefabs--resources)
7. [Networking](#networking)
8. [Physics](#physics)
9. [Input](#input)
10. [Rendering & UI](#rendering--ui)
11. [Scripting — Attributes & Code Generation](#scripting--attributes--code-generation)
12. [Events & GameObjectSystem](#events--gameobjectsystem)
13. [Hot Reload](#hot-reload)

---

## Overview

s&box is a modern game engine built on Valve's **Source 2** renderer and **.NET** (currently targeting .NET 10). It provides:

- **Component-based scene system** — GameObjects + Components, no deep inheritance required
- **Built-in multiplayer** — host-authoritative networking via Steam's networking stack
- **Physics simulation** — full rigidbody physics and spatial query traces
- **Razor-powered UI** — HTML/CSS-style panels, both 2D screen overlays and 3D world panels
- **Live hot-reload** — edit C# while the game is running; changes apply without restarting
- **Action graphs** — visual scripting for logic without code

All gameplay logic is written in modern C# and every interactive system lives inside **Components** attached to **GameObjects**.

---

## Installation

### Via Steam (Recommended)

1. Visit [sbox.game/give-me-that](https://sbox.game/give-me-that) and request beta access.
2. Once approved, install **s&box** from your Steam library.
3. Launch via Steam — on first run, choose a working directory for your projects.

You can launch directly into a specific project with: `sbox.exe +game facepunch.sandbox`

### Build from Source (Contributors)

**Prerequisites:** Git, Visual Studio 2022 (.NET desktop workload), .NET 10 SDK

```bash
git clone https://github.com/Facepunch/sbox-public.git
# From repo root:
Bootstrap.bat   # runs build, build-shaders, build-content
```

After completion, binaries are in the `game/` folder.

---

## Scenes & GameObjects

### The Scene

A `Scene` is the root container for everything in your game. It inherits from `GameObject` and owns the physics world, render world, and object directory.

```csharp
// Access the active scene from anywhere
Scene active = Game.ActiveScene;

// Create a new blank scene
var scene = new Scene();

// Destroy when done
scene.Destroy();
```

**Key Scene properties:**

| Property | Type | Description |
|---|---|---|
| `Game.ActiveScene` | `Scene` | The scene currently being ticked |
| `Scene.IsEditor` | `bool` | True in editor preview scenes |
| `Scene.PhysicsWorld` | `PhysicsWorld` | Physics simulation |
| `Scene.SceneWorld` | `SceneWorld` | Render world |
| `Scene.Directory` | `GameObjectDirectory` | Fast lookup index |
| `Scene.TimeScale` | `float` | Slow-motion multiplier (0–1) |

**Creating objects:**

```csharp
var go = Game.ActiveScene.CreateObject();
go.Name = "My Object";
```

**Finding objects by tag:**

```csharp
foreach ( var enemy in Scene.FindAllWithTag( "enemy" ) ) { }
var tagged = Scene.FindAllWithTags( new[] { "pickup", "active" } );
```

---

### GameObjects

A `GameObject` is a node in the hierarchy. It has a name, transform, enabled flag, and tag set. All behavior comes from Components.

```csharp
var go    = new GameObject( "Player" );
var go2   = new GameObject( false, "Disabled Prop" );
var child = new GameObject( parentGo, true, "Child" );
```

#### Hierarchy

```csharp
child.SetParent( newParent, keepWorldPosition: true );
child.Parent = anotherParent;

foreach ( var c in go.Children ) { }
foreach ( var obj in go.GetAllObjects( enabled: true ) ) { }
```

`go.IsRoot` — true when the parent is the Scene itself.  
`go.Root` — climbs to the top-most non-scene ancestor.

#### Enabled vs. Active

- `go.Enabled` — what *you* set
- `go.Active` — true only if the object **and all ancestors** are enabled

```csharp
go.Enabled = false;
bool alive = go.Active;
```

#### Tags

```csharp
go.Tags.Add( "enemy" );
bool isEnemy = go.Tags.Has( "enemy" );
bool isBoss  = go.Tags.HasAll( new[] { "enemy", "boss" } );
```

#### GameObjectFlags

```csharp
go.Flags = GameObjectFlags.DontDestroyOnLoad; // persists across scene loads
go.Flags |= GameObjectFlags.Hidden;           // hide from editor
go.Flags |= GameObjectFlags.NotNetworked;     // exclude from networking
go.Flags |= GameObjectFlags.EditorOnly;       // editor only, no in-game spawn
go.Flags |= GameObjectFlags.Absolute;         // ignore parent transform
```

#### Destroying GameObjects

```csharp
go.Destroy();

if ( go.IsValid() )
    go.Destroy();
```

---

## Components

Every piece of behavior is a `Component` — a C# class inheriting from `Component`.

### Creating a Component

```csharp
using Sandbox;

public sealed class HealthComponent : Component
{
    [Property] public float MaxHealth { get; set; } = 100f;
    [Property] public float CurrentHealth { get; private set; }

    protected override void OnAwake()
    {
        CurrentHealth = MaxHealth;
    }

    protected override void OnUpdate()
    {
        if ( CurrentHealth <= 0f )
        {
            Log.Info( $"{GameObject.Name} died." );
            GameObject.Destroy();
        }
    }

    public void TakeDamage( float amount )
    {
        CurrentHealth = Math.Max( 0f, CurrentHealth - amount );
    }
}
```

### The `[Property]` Attribute

Exposes fields/properties to the Inspector, serializer, and Reset():

```csharp
[Property, Range( 0f, 600f )]
public float Speed { get; set; } = 200f;

[Property]
public Model CharacterModel { get; set; }

[Property]
public GameObject SpawnPoint { get; set; }

[Property, Group( "Combat" )]
public float Damage { get; set; } = 10f;
```

Additional `[Property]` display attributes:

| Attribute | Purpose |
|---|---|
| `[KeyProperty]` | Represents the whole object in collapsed rows |
| `[InlineEditor]` | Expand inline instead of popup |
| `[Advanced]` | Hidden unless Advanced mode is enabled |
| `[Range(min, max)]` | Show as a slider |
| `[Group("name")]` | Organize into collapsible groups |
| `[ToggleGroup("bool")]` | Group controlled by a bool toggle |
| `[TextArea]` | Multi-line text field |
| `[Title("...")]` | Custom display name |
| `[Category("...")]` | Category in Add Component menu |
| `[Icon("...")]` | Material Design icon name |

### Built-in Component Properties

| Property | Type | Description |
|---|---|---|
| `GameObject` | `GameObject` | The owning object |
| `Scene` | `Scene` | Shortcut for `GameObject.Scene` |
| `Transform` | `GameTransform` | Shortcut for `GameObject.Transform` |
| `Components` | `ComponentList` | All components on same GameObject |
| `Enabled` | `bool` | Whether this component is enabled |
| `Active` | `bool` | Enabled AND hierarchy is active |
| `Tags` | `ITagSet` | Shortcut for `GameObject.Tags` |

### Adding & Querying Components

```csharp
// Add
var health = go.AddComponent<HealthComponent>();
var health2 = go.GetOrAddComponent<HealthComponent>();

// Query on same object
var h = go.GetComponent<HealthComponent>();
var h2 = go.GetComponent<HealthComponent>( includeDisabled: true );
IEnumerable<HealthComponent> all = go.GetComponents<HealthComponent>();

// In hierarchy
var h = go.GetComponentInChildren<HealthComponent>();
var h = go.GetComponentInParent<GameManager>();

// Scene-wide
foreach ( var e in Scene.GetAllComponents<EnemyComponent>() )
    e.Alert();
```

### Destroying & Toggling Components

```csharp
myComponent.Destroy();      // removes from GameObject
DestroyGameObject();        // destroys the entire GameObject

myComponent.Enabled = false; // OnDisabled fires
myComponent.Enabled = true;  // OnEnabled fires
```

---

## Component Lifecycle

### Lifecycle Order

```
Component created / enabled
         │
         ▼
     OnAwake()          ← once, ever
         │
         ▼
     OnEnabled()        ← each time false → true
         │
         ▼  (first tick)
     OnStart()          ← once, before first Update
         │
    ┌────┴─────────────────────────────────┐
    │  per-physics-tick   │  per-frame     │
    ▼                     ▼                │
OnFixedUpdate()       OnUpdate()          │
                          │               │
                      OnPreRender()       │
    └─────────────────────────────────────┘
         │
         ▼  (on disable)
     OnDisabled()      ← each time true → false
         │
         ▼  (on destroy)
     OnDestroy()       ← once, ever
```

### Callback Reference

| Method | When |
|---|---|
| `OnAwake()` | Once, first time component becomes active. Cache references here. |
| `OnEnabled()` | Every time component becomes active (after OnAwake and on re-enable). |
| `OnStart()` | Once, before first OnUpdate. All siblings have run OnAwake. |
| `OnUpdate()` | Every frame while active. Uses `Time.Delta`. |
| `OnFixedUpdate()` | Every physics tick. Use for Rigidbody logic. |
| `OnPreRender()` | Before rendering. Sync visual state here. |
| `OnDisabled()` | Every time component becomes inactive. Unsubscribe events. |
| `OnDestroy()` | Once, on permanent removal. Clean up native resources. |
| `OnValidate()` | After JSON deserialization or inspector property change. Clamp values. |
| `OnRefresh()` | After network snapshot update. |
| `OnParentChanged(old, new)` | When the GameObject is re-parented. |
| `OnTagsChanged()` | When the tag set changes. |

### ShouldExecute Rules

| Condition | Result |
|---|---|
| Scene is a `PrefabCacheScene` | Never executes |
| Scene is null | Never executes |
| Editor preview scene and no `ExecuteInEditor` | Never executes |
| Dedicated server and `DontExecuteOnServer` | Never executes |

```csharp
// Run in editor preview scenes
public sealed class MyEditorTool : Component, ExecuteInEditor { }

// Skip on dedicated servers
public sealed class ParticleSpawner : Component, DontExecuteOnServer { }
```

---

## Prefabs & Resources

### Prefabs

A `.prefab` file stores a complete serialized GameObject hierarchy.

```csharp
// Load (read-only template)
var prefabGo = GameObject.GetPrefab( "prefabs/enemies/grunt.prefab" );

// Instantiate (preferred method)
var enemy = SceneUtility.Instantiate(
    ResourceLibrary.Get<PrefabFile>( "prefabs/enemies/grunt.prefab" ),
    spawnPoint
);

// Clone from template root
var instance = prefabRoot.Clone( Transform.World );
instance.Parent = Game.ActiveScene;

// Prefab state queries
bool isInstance   = go.IsPrefabInstance;
bool isRoot       = go.IsPrefabInstanceRoot;
string sourceFile = go.PrefabInstanceSource;

// Break / update link
go.BreakFromPrefab();    // irreversible
go.UpdateFromPrefab();   // pull latest values
```

### Resources

| Class | Purpose |
|---|---|
| `Resource` | Base for native engine assets (Model, Material, Texture, …) |
| `GameResource` | Base for your own JSON-serialized C# asset types |

**Native resource loading:**

```csharp
Model    model = Model.Load( "models/citizen/citizen.vmdl" );
Material mat   = Material.Load( "materials/custom/hero.vmat" );
SoundFile sfx  = SoundFile.Load( "sounds/weapons/shotgun_fire.vsnd" );
```

**Custom GameResource:**

```csharp
[GameResource( "Item Definition", "item", "Defines a collectible item", Icon = "backpack" )]
public sealed class ItemDefinition : GameResource
{
    [Property] public string DisplayName { get; set; }
    [Property] public Model  PickupModel { get; set; }
    [Property] public float  HealAmount  { get; set; }

    protected override void PostLoad()   { }
    protected override void PostReload() { }
}

// Loading
var item = ResourceLibrary.Get<ItemDefinition>( "data/items/medkit.item" );
```

**Built-in resource types:**

| Type | Extension | Description |
|---|---|---|
| `Model` | `.vmdl` | 3D mesh + skeleton |
| `Material` | `.vmat` | Surface shading |
| `Texture` | `.vtex` | 2D image |
| `Shader` | `.vfx` | GPU shader |
| `SoundFile` | `.vsnd` | Sound asset |
| `AnimationGraph` | `.vanmgrph` | Character animation state machine |
| `SceneFile` | `.scene` | Full scene |
| `PrefabFile` | `.prefab` | Serialized GameObject hierarchy |

**FileReference<T>** — defer loading until needed:

```csharp
[Property] public FileReference<PrefabFile> SpawnablePrefab { get; set; }

// Load on demand
var prefab = SpawnablePrefab.GetAsset();
```

---

## Networking

s&box uses a **host-authoritative** model over Steam's networking stack.

### NetworkMode

```csharp
// Default: never networked
GameObject.NetworkMode = NetworkMode.Never;

// Networked as a full object (has owner, Sync properties)
GameObject.NetworkMode = NetworkMode.Object;

// Networked as snapshot (position/rotation only)
GameObject.NetworkMode = NetworkMode.Snapshot;
```

### Spawning Networked Objects

Only the **host** can spawn networked objects:

```csharp
var go = SceneUtility.GetPrefabScene( prefab ).Clone();
go.NetworkSpawn( connection );  // assign to a specific connection
```

### `[Sync]` Properties

Automatically replicates from the **owner** to all other clients:

```csharp
public sealed class PlayerComponent : Component
{
    [Sync] public float Health { get; set; } = 100f;
    [Sync] public bool IsAlive { get; set; } = true;

    // Host writes, all clients receive read-only
    [HostSync] public int Score { get; set; }

    // Custom sync direction / polling
    [Sync( SyncFlags.FromHost )]  public float GameTime { get; set; }
    [Sync( SyncFlags.Query )]     public Vector3 ExternalPosition { get; set; }
}
```

> **Note:** `[HostSync]` is obsolete since December 2024. Use `[Sync( SyncFlags.FromHost )]` instead.

### RPCs

```csharp
// Broadcast: called on ALL clients including host
[Rpc.Broadcast]
public void OnPlayerDied( string killerName ) { }

// Owner: called only on the owning client
[Rpc.Owner]
public void ReceiveAmmo( int amount ) { }

// Host: called only on the host
[Rpc.Host]
public void RequestSpawnItem( string itemId ) { }
```

**Checking the caller inside an RPC:**

```csharp
[Rpc.Host]
public void TakeDamage( float amount )
{
    Log.Info( $"Damage from {Rpc.Caller.DisplayName}" );
}
```

### Filtering RPC Recipients

```csharp
// Send to specific connections
using ( Rpc.FilterInclude( targetConnection ) )
    OnPlayerDied( "Alice" );

using ( Rpc.FilterInclude( c => c.Ping < 200 ) )
    SendHighPriorityEvent();

// Exclude connections
using ( Rpc.FilterExclude( localConnection ) )
    BroadcastEvent( "something happened" );
```

> You cannot nest filter scopes — an `InvalidOperationException` is thrown.

### Ownership

```csharp
if ( IsProxy ) return; // We don't own this object

// Transfer ownership
GameObject.Network.AssignOwnership( newOwnerConnection );
```

**NetworkOrphaned policies** (when owner disconnects):

| Policy | Behavior |
|---|---|
| `Destroy` | Object is destroyed |
| `ClearOwner` | Ownership cleared (host takes control) |
| `Host` | Transferred to host |
| `Random` | Transferred to random remaining connection |

### Networking State Helpers

```csharp
bool amHost        = Networking.IsHost;
bool amClient      = Networking.IsClient;
bool sessionActive = Networking.IsActive;
```

---

## Physics

Physics is accessed via `Scene.PhysicsWorld`.

### Traces (Raycasts)

```csharp
// Ray
var tr = Scene.PhysicsWorld.Trace
    .Ray( WorldPosition, WorldPosition + WorldRotation.Forward * 1000f )
    .Run();

if ( tr.Hit )
{
    Log.Info( $"Hit {tr.Body?.GameObject?.Name} at {tr.HitPosition}" );
    Log.Info( $"Normal: {tr.Normal}, Surface: {tr.Surface?.ResourceName}" );
}

// Sphere sweep
var tr = Scene.PhysicsWorld.Trace.Sphere( radius: 16f, from: start, to: end ).Run();

// Box sweep
var tr = Scene.PhysicsWorld.Trace
    .Box( new Vector3( 32, 32, 64 ), from: start, to: end ).Run();

// Capsule sweep
var capsule = new Capsule( Vector3.Zero, Vector3.Up * 72f, 16f );
var tr = Scene.PhysicsWorld.Trace.Capsule( capsule, from: start, to: end ).Run();

// Multiple hits
var hits = Scene.PhysicsWorld.Trace.Ray( start, end ).RunAll();
```

**Trace filters:**

| Method | Effect |
|---|---|
| `.IgnoreStatic()` | Skip static world geometry |
| `.IgnoreDynamic()` | Skip dynamic bodies |
| `.IgnoreKeyframed()` | Skip keyframed bodies |
| `.HitTriggers()` | Include trigger volumes |
| `.HitTriggersOnly()` | Only hit triggers |

**PhysicsTraceResult fields:**

| Field | Type | Description |
|---|---|---|
| `Hit` | `bool` | Whether anything was hit |
| `StartedSolid` | `bool` | Trace started inside a solid |
| `HitPosition` | `Vector3` | World position of hit |
| `Normal` | `Vector3` | Surface normal |
| `Fraction` | `float` | How far [0..1] trace traveled |
| `Body` | `PhysicsBody` | Hit physics body |
| `Surface` | `Surface` | Surface material |

### Rigidbody Component

Requires at least one collider (`BoxCollider`, `SphereCollider`, `CapsuleCollider`) on the same or child object.

```csharp
var rb = Components.Get<Rigidbody>();

// Velocity
rb.Velocity        = Vector3.Up * 500f;
rb.AngularVelocity = new Vector3( 0, 0, 90f );

// Forces
rb.ApplyForce( Vector3.Up * 9800f );
rb.ApplyForceAt( worldPoint, Vector3.Right * 500f );
rb.ApplyTorque( new Vector3( 0, 0, 1000f ) );

// Impulses (not scaled by delta time)
rb.ApplyImpulse( Vector3.Up * 300f );
rb.ApplyImpulseAt( worldPoint, Vector3.Forward * 200f );

// Mass & damping
rb.MassOverride   = 50f;
rb.LinearDamping  = 0.1f;
rb.AngularDamping = 0.5f;
rb.GravityScale   = 2.0f;

// CCD for fast-moving objects
rb.PhysicsBody.EnhancedCcd = true;

// Sleeping
rb.Sleeping = false; // force wake
```

### Collision Detection

```csharp
public sealed class DamageOnCollision : Component, Component.ICollisionListener
{
    void ICollisionListener.OnCollisionStart( Collision collision )
    {
        var speed  = collision.Contact.NormalSpeed;
        var other  = collision.Other.GameObject;
        var point  = collision.Contact.Point;
        var normal = collision.Contact.Normal;
    }

    void ICollisionListener.OnCollisionUpdate( Collision c ) { }
    void ICollisionListener.OnCollisionEnd( CollisionStop s ) { }
}
```

---

## Input

### Querying Actions

```csharp
if ( Input.Down( "Attack1" ) )   { }  // held
if ( Input.Pressed( "Jump" ) )   { }  // this frame only
if ( Input.Released( "Duck" ) )  { }  // released this frame
```

### Analog Inputs

```csharp
// Movement direction (from WASD / gamepad stick)
var wishDir = Input.AnalogMove;  // Vector3, local space

// Mouse look (pre-scaled by sensitivity)
EyeAngles += Input.AnalogLook;   // Angles

// Raw mouse
Vector2 delta  = Input.MouseDelta;
float   scroll = Input.MouseWheel.y;

// Cursor visible?
if ( Input.MouseCursorVisible ) return;
```

### Default Actions

**Movement:** `Forward`(W), `Backward`(S), `Left`(A), `Right`(D), `Jump`(Space), `Run`(Shift), `Duck`(Ctrl)  
**Actions:** `Attack1`(M1), `Attack2`(M2), `Reload`(R), `Use`(E), `Drop`(G), `Flashlight`(F)  
**Inventory:** `Slot1`–`Slot9`, `SlotPrev`(M4), `SlotNext`(M5)  
**Misc:** `View`(C), `Voice`(V), `Score`(Tab), `Menu`(Q), `Chat`(Enter)

### Programmatic Input

```csharp
Input.SetAction( "Jump", true );
Input.Clear( "Jump" );
Input.ReleaseActions();
```

### Querying Bound Keys

```csharp
string key = Input.GetButtonOrigin( "Jump" );
// "Space" on keyboard, "A Button" on gamepad
```

### Full Movement Example

```csharp
public sealed class PlayerController : Component
{
    [Property] public float MoveSpeed    { get; set; } = 200f;
    [Property] public float JumpStrength { get; set; } = 350f;

    Angles eyeAngles;

    protected override void OnUpdate()
    {
        eyeAngles += Input.AnalogLook;
        eyeAngles = eyeAngles.WithPitch( eyeAngles.pitch.Clamp( -89f, 89f ) );
        Scene.Camera.WorldRotation = eyeAngles.ToRotation();
    }

    protected override void OnFixedUpdate()
    {
        var wishDir  = Input.AnalogMove.Normal;
        var rotation = Rotation.FromYaw( eyeAngles.yaw );
        var velocity = rotation * wishDir * MoveSpeed;

        if ( Input.Pressed( "Jump" ) )
            velocity += Vector3.Up * JumpStrength;

        WorldPosition += velocity * Time.Delta;
    }
}
```

---

## Rendering & UI

### UI System Overview

s&box UI uses an HTML/CSS-style panel model with Razor (`.razor`) markup and SCSS styling — GPU-accelerated, no browser required. Layout follows CSS flexbox.

| Component | Description |
|---|---|
| `ScreenPanel` | 2D overlay (HUDs, menus) |
| `WorldPanel` | 3D world-space panel (name tags, terminals) |

### ScreenPanel

```csharp
var sp = Components.Get<ScreenPanel>();
sp.Opacity = 0.9f;
sp.ZIndex  = 200;
// ScaleStrategy: ConsistentHeight (1080p assumed) or FollowDesktopScaling
```

### WorldPanel

```csharp
var wp = Components.Get<WorldPanel>();
wp.PanelSize        = new Vector2( 800, 400 );
wp.LookAtCamera     = true;
wp.InteractionRange = 250f;
```

### PanelComponent (Custom UI)

Create three files with the same name:

**`HealthHud.cs`:**
```csharp
public sealed class HealthHud : PanelComponent
{
    [Property] public float Health    { get; set; } = 100f;
    [Property] public float MaxHealth { get; set; } = 100f;

    protected override int BuildHash()
    {
        // Re-renders when these values change
        return HashCode.Combine( Health, MaxHealth );
    }
}
```

**`HealthHud.razor`:**
```razor
@using Sandbox;
@inherits PanelComponent

<root>
    <div class="health-bar">
        <div class="health-fill" style="width: @FillPercent%"></div>
        <label class="health-text">@Health / @MaxHealth</label>
    </div>
</root>

@code {
    float FillPercent => (Health / MaxHealth * 100f).Clamp( 0, 100 );
}
```

**`HealthHud.razor.scss`:**
```scss
.health-bar {
    width: 300px;
    height: 24px;
    background-color: rgba(0, 0, 0, 0.6);
    border-radius: 4px;
    overflow: hidden;
}
.health-fill {
    height: 100%;
    background-color: #e84040;
    transition-duration: 0.3s;
}
```

### BuildHash & Reactivity

The UI re-renders when `BuildHash()` returns a different value. Always include every template dependency:

```csharp
protected override int BuildHash()
    => HashCode.Combine( Health, IsAlive, StatusEffect );

// Force immediate re-render
Health -= damage;
StateHasChanged();
```

### Razor Markup Reference

```razor
@* Conditional *@
@if ( IsVisible ) { <div>@Message</div> }

@* Loop *@
@foreach ( var item in Items ) { <div>@item.Name</div> }

@* Style binding *@
<div style="opacity: @Opacity; color: @TintColor.Hex;">…</div>

@* Class conditional *@
<div class="slot @(IsSelected ? "selected" : "")">…</div>

@* Event handler *@
<button onclick=@OnButtonClicked>Click me</button>
```

### Mouse Events

```csharp
protected override void OnMouseDown( MousePanelEvent e ) { }
protected override void OnMouseMove( MousePanelEvent e ) { }
protected override void OnMouseUp( MousePanelEvent e )   { }
protected override void OnMouseOver( MousePanelEvent e ) { }
protected override void OnMouseOut( MousePanelEvent e )  { }
protected override void OnMouseWheel( Vector2 delta )    { }
```

---

## Scripting — Attributes & Code Generation

### `[Property]`

Marks a property for the Inspector, serializer, and `Reset()`.

```csharp
[Property] public float Speed { get; set; } = 500f;
[Property( "display_name" )] public string DisplayName { get; set; }
```

### `[Sync]` / `[HostSync]`

Replicates properties over the network (see **Networking** section).

### RPC Attributes

```csharp
[Rpc.Broadcast] public void PlayFireEffect() { }  // all clients
[Rpc.Host]      public void RequestRefill()  { }  // host only
[Rpc.Owner]     public void ReceiveHit()     { }  // owner only
```

### `[ConVar]`

Exposes a static property as a console variable:

```csharp
public static class GameSettings
{
    [ConVar( "sv_gravity", Help = "World gravity scale" )]
    public static float Gravity { get; set; } = 1.0f;

    [ConVar( Saved = true )]
    public static float MusicVolume { get; set; } = 0.8f;

    [ConVar( flags: ConVarFlags.Replicated )]
    public static int MaxPlayers { get; set; } = 16;
}
```

`ConVarFlags`: `Saved`, `Replicated`, `Cheat`, `UserInfo`, `Hidden`, `ChangeNotice`, `Protected`, `Server`, `Admin`, `GameSetting`.

### `[ConCmd]`

Marks a static method as a console command:

```csharp
[ConCmd( "kill_all", flags: ConVarFlags.Server | ConVarFlags.Admin )]
public static void KillAll()
{
    foreach ( var player in Game.ActiveScene.GetAllComponents<Player>() )
        player.Kill();
}
```

### `[CodeGenerator]`

Meta-attribute to define custom compile-time code transformations:

```csharp
// 1. Define the attribute
[AttributeUsage( AttributeTargets.Method )]
[CodeGenerator( CodeGeneratorFlags.Instance | CodeGeneratorFlags.WrapMethod, "MyMod.Profiler.OnEnter" )]
public class ProfiledAttribute : Attribute { }

// 2. Provide the callback
public static class Profiler
{
    public static void OnEnter( object target, string methodName, Action inner )
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        inner();
        Log.Info( $"{methodName} took {sw.ElapsedMilliseconds}ms" );
    }
}

// 3. Use it
public class ExpensiveComponent : Component
{
    [Profiled]
    public void DoExpensiveWork() { ... }
}
```

**CodeGeneratorFlags:**

| Flag | Effect |
|---|---|
| `WrapPropertyGet` | Intercept property getter |
| `WrapPropertySet` | Intercept property setter |
| `WrapMethod` | Intercept method calls |
| `Static` | Apply to static targets |
| `Instance` | Apply to instance targets |

---

## Events & GameObjectSystem

### Attribute-Based Events

```csharp
public class MySystem : GameObjectSystem
{
    public MySystem( Scene scene ) : base( scene ) { }

    [EditorEvent.Frame]
    void OnFrame() { /* every editor frame */ }
}
```

Control dispatch order with `Priority` (lower = runs first, default = 0):

```csharp
[Event( "my.game.tick", Priority = -1 )]
void RunFirst() { }
```

**Built-in event attributes:**

| Attribute | When |
|---|---|
| `[EditorEvent.Frame]` | Every editor frame |
| `[EditorEvent.Hotload]` | After hot reload |
| `[Event.Streamer.JoinChat]` | Viewer joins stream chat |
| `[Event.Streamer.ChatMessage]` | Chat message received |

### Interface-Based Scene Events

**Scene loading:**

```csharp
public class GameManager : GameObjectSystem, ISceneLoadingEvents
{
    public GameManager( Scene scene ) : base( scene ) { }

    void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options ) { }
    async Task ISceneLoadingEvents.OnLoad( Scene scene, SceneLoadOptions options ) { await LoadAssetsAsync(); }
    void ISceneLoadingEvents.AfterLoad( Scene scene ) { }
}
```

**Scene startup:**

```csharp
public class Bootstrap : GameObjectSystem, ISceneStartup
{
    public Bootstrap( Scene scene ) : base( scene ) { }

    void ISceneStartup.OnHostPreInitialize( SceneFile scene ) { }  // host only, before load
    void ISceneStartup.OnHostInitialize() { }                      // host only, after load
    void ISceneStartup.OnClientInitialize() { }                    // clients after receiving scene
}
```

**Physics events:**

```csharp
public class PhysicsListener : GameObjectSystem, IScenePhysicsEvents
{
    public PhysicsListener( Scene scene ) : base( scene ) { }

    void IScenePhysicsEvents.PrePhysicsStep()  { }
    void IScenePhysicsEvents.PostPhysicsStep() { }
    void IScenePhysicsEvents.OnOutOfBounds( Rigidbody body ) { body.GameObject.Destroy(); }
    void IScenePhysicsEvents.OnFellAsleep( Rigidbody body )  { }
}
```

### Custom Events

```csharp
// 1. Define event attribute
public static class GameEvent
{
    public class PlayerScoredAttribute : EventAttribute
    {
        public PlayerScoredAttribute() : base( "mygame.player.scored" ) { }
    }
}

// 2. Subscribe
[GameEvent.PlayerScored]
void OnPlayerScored( string playerName, int newScore ) { }

// 3. Fire
Event.Run( "mygame.player.scored", playerName, newScore );
```

### GameObjectSystem

Per-scene singleton manager, automatically created when the scene starts:

```csharp
public class ScoreManager : GameObjectSystem<ScoreManager>
{
    public int Score { get; private set; }

    public ScoreManager( Scene scene ) : base( scene )
    {
        Listen( Stage.StartUpdate, 0, Tick, "ScoreManager Tick" );
    }

    void Tick() { /* every frame */ }

    public void AddScore( int amount ) { Score += amount; }
}

// Access from anywhere:
ScoreManager.Current.AddScore( 10 );
var mgr = Scene.GetSystem<ScoreManager>();
```

**Tick stages:**

| Stage | When |
|---|---|
| `Stage.StartUpdate` | Start of each frame |
| `Stage.FinishUpdate` | End of each frame |
| `Stage.StartFixedUpdate` | Start of each physics tick |
| `Stage.PhysicsStep` | During physics step |
| `Stage.FinishFixedUpdate` | End of physics tick |
| `Stage.UpdateBones` | After bone transforms |
| `Stage.Interpolation` | Transform interpolation |
| `Stage.SceneLoaded` | Once, after scene loads |

---

## Hot Reload

Edit C# source files and see changes applied to the running game without restarting.

### How It Works

**IL hot reload (fast path):** Only method bodies changed — the engine patches method implementations in-place. No instances are disrupted.

**Full assembly reload:** Triggered by structural changes (new fields, type changes, etc.):
1. Modified assembly is recompiled
2. Old assembly is unregistered from EventSystem
3. `Hotload` class orchestrates the swap
4. `UpdateReferences` walks watched fields and upgrades all references to the new type

### Writing Hot-Reload-Friendly Code

```csharp
public sealed class ScoreDisplay : Component
{
    // [Property] fields are serialized and restored across full reloads
    [Property] public int Score { get; set; }
    [Property] public string PlayerName { get; set; } = "Player";

    protected override void OnUpdate()
    {
        // Method body changes use IL fast path — no disruption
        Log.Info( $"{PlayerName}: {Score}" );
    }
}
```

### Changes That Require Full Reload

| Change | Reason |
|---|---|
| Adding/removing a field | Changes memory layout |
| Changing field type | Requires value migration |
| Adding/removing a method signature | Changes assembly surface |
| Modifying a `struct` | Value types unsafe to patch in-place |
| Adding a `using` directive | Treated as declaration-level change |
| Changing preprocessor symbols | Changes compiled code |

> **Warning:** Static fields are **not** automatically reset during full reloads. Prefer `[Property]` instance fields for state that should reset correctly.

---

## Quick Reference

### Common Patterns

```csharp
// Get or create a component
var health = go.GetOrAddComponent<HealthComponent>();

// Safe null check after potential destroy
if ( go.IsValid() ) go.Destroy();

// Scene-wide singleton access
var mgr = Scene.GetSystem<GameManager>();

// Spawn prefab at location
SceneUtility.Instantiate(
    ResourceLibrary.Get<PrefabFile>( "prefabs/player.prefab" ),
    spawnTransform
);

// Physics raycast from camera center
var ray = Scene.Camera.ScreenNormalToRay( 0.5f, 0.5f );
var tr  = Scene.PhysicsWorld.Trace.Ray( ray, 2000f ).Run();

// Network-safe write guard
if ( IsProxy ) return;

// Broadcast an event
Event.Run( "mygame.player.scored", playerName, score );
```

### File Extensions

| Extension | Type |
|---|---|
| `.scene` | Scene file |
| `.prefab` | Prefab file |
| `.vmdl` | 3D model |
| `.vmat` | Material |
| `.vtex` | Texture |
| `.vsnd` | Sound |
| `.vanmgrph` | Animation graph |
| `.vfx` | Shader |
| `.razor` | UI panel markup |
| `.sbproj` | s&box project file |

---

*Documentation source: [sbox.game/dev/doc](https://sbox.game/dev/doc) · [github.com/Facepunch/sbox-public](https://github.com/Facepunch/sbox-public)*