# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication Style (最高优先级)

**用户是非技术背景的产品/策划人员**，不熟悉编程术语。沟通时务必遵守以下规则：

1. **先理解，后动手** — 用户的需求描述可能不精确、不成体系、夹杂情绪化表达。不要字面理解，要以产品经理/主程的思维去**揣摩他真正想达成的体验或结果**，把模糊的日常语言翻译成具体的技术方案。
2. **说人话** — 回复用中文，避免技术黑话（如"用事件系统解耦"、"走 Mirror RPC"），改用结果导向的描述（如"让登录按钮点完后弹出房间列表"）。如果必须提到技术细节，先用人话解释它会影响什么。
3. **主动确认，减少返工** — 如果需求有多种理解方式，用 2-3 个简洁选项让他选（描述效果差异，不列技术方案），而不是默认为其中一种直接开工。
4. **改动前先简要说明思路** — 不要直接贴代码。用一两句人话讲清楚：改哪里、影响什么、为什么这样做。
5. **容忍情绪，保持耐心** — 用户可能因为不理解技术限制而急躁。永远不要以"技术上做不到"直接拒绝，而是解释限制背后的原因，并主动给出替代方案。

> 一句话：把用户当成只会描述"想要什么效果"的产品经理，你来当那个把需求翻译成代码的主程。

## Project Overview

《Keep Run》— a personal 2D platformer multiplayer game. Unity client + C# .NET Framework 4.7.2 socket server, using MySQL for persistence.

## Directory Layout

```
Client/                  # Unity project (open with Unity Editor)
  Assets/Scripts/
    GameLaunch.cs        # Entry point: initializes singletons, canvas, Mirror network
    GameManager.cs       # Global game state (mode, room, login, scoring)
    Event/               # Event system (observer pattern, decouples UI/game logic)
    Game/AI/             # FSM-based Boss and Enemy AI
    Game/Player/         # Player controller, animation, local/remote drivers
    Game/PlatformGenerator/  # Infinite scrolling platform system
    Game/Mechanism/      # Game mechanics (teleporter, trap, win door, etc.)
    Game/Camera/         # Camera controller and effects
    Game/Damage/         # Damage projectiles (fireball, bomb, slash)
    Game/Input/          # Virtual input (joystick, buttons, axis)
    Manager/             # UIManager, UpdateManager, TimerMgr
    Network/Mirror/      # Mirror networking (see below)
    UIPanel/             # All UI panels (Login, Room, Game, Pause, etc.)
    Resource/            # ResourceManager (async asset loading)
    Utilities/           # CommonUtility, AdvancedTmpSizer

Server/
  SocketMultiplayerGameServer/   # C# .NET Framework 4.7.2 console app
    Program.cs           # Entry point, starts TCP Server on port 6666
    Servers/Server.cs    # TCP server: accept, heartbeat, reconnect, room CRUD
    Servers/UDPServer.cs # UDP server on port 6667
    Servers/Client.cs    # Per-connection client state and message I/O
    Servers/Room.cs      # Room state (players, master, game)
    Controller/          # RequestCode → Controller dispatch via reflection
    Database/            # MySQL connection pool (MySqlConnector)
    DAO/                 # Data access objects (UserData)
    CONFIG.cs            # Server constants (heartbeat, reconnect window, DB config)
  Protocol/
    SocketGameProtocol.proto  # protobuf message definitions (MainPack, RoomPack, etc.)
```

## Build & Run

### Unity Client
- Open `Client/` in Unity (version 2022.3 LTS based on package cache).
- **Play in Editor**: Open scene `Assets/Scenes/Game.unity`, press Play. `GameLaunch.cs` auto-creates the network manager and UI.
- **Headless build** (dedicated server): Use Unity menu `Mirror > Build Headless Server` (see `Client/Assets/Scripts/Network/Mirror/Server/Editor/HeadlessBuildTool.cs`). Output goes to `Client/Builds/Headless-Win/`.
- **Android build**: Gradle project at `Client/Android/`. Build from Unity with Android as target platform.

### C# Server (独立 TCP/UDP Server)
- Open `Server/SocketMultiplayerGameServer/SocketMultiplayerGameServer.sln` in Visual Studio.
- Build: `msbuild Server/SocketMultiplayerGameServer/SocketMultiplayerGameServer.csproj /p:Configuration=Debug`
- Run: `Server/SocketMultiplayerGameServer/bin/Debug/SocketMultiplayerGameServer.exe`
- **Requires MySQL** on `localhost:3306`, database `test`, user `root`/`314159` (see `CONFIG.cs`).

## Architecture

### Event System
The **EventDispatcher** (`Client/Assets/Scripts/Event/EventDispater.cs`) is the central communication bus. All UI and game modules communicate through string-named events defined as `MessageEvent` static fields. Usage pattern:
```csharp
// Subscribe
EventDispatcher.AddObserver(this, MessageEvent.OnLoginOK, LoginOK, null);
// Publish
EventDispatcher.PostEvent(MessageEvent.OnLoginOK, this, username, sessionId, playerId);
// Callback returns true to auto-unsubscribe
private bool LoginOK(params object[] args) { ... return false; } // false = keep listening
```

### Two Networking Systems

1. **Original TCP+UDP Server** (`Server/`): Custom socket server with protobuf serialization. RequestCode → Controller → ActionCode method dispatch via reflection (`ControllerManager.HandleRequest`). Uses a custom binary header framing protocol (4-byte length prefix + protobuf body). Supports heartbeat, session-based reconnect, room management.

2. **Mirror Networking** (`Client/Assets/Scripts/Network/Mirror/`): Unity's Mirror (KCP transport) replacing the raw socket approach. Key components:
   - `CustomNetworkManager` — extends Mirror's `NetworkManager`, manages connection lifecycle, registers all message handlers, bridges to EventDispatcher for backward compatibility
   - `MirrorRoomManager` / `MirrorAuthManager` / `MirrorServerGameManager` — server-side managers handling room/auth/game messages
   - `MirrorGameManager` — in-game networked state (spawn, timer, game flow)
   - `MirrorPlayer` — networked player entity with SyncVars (hp, isDead, starCount)
   - Custom `NetworkMessage` structs in `Messages/` directory (AuthMessages, GameMessages, RoomMessages)

Both systems coexist — the Mirror layer forwards its events through the EventDispatcher to compatible UI code.

### Controller Pattern (Server)
`ControllerManager` maps `RequestCode` enums to `BaseController` subclasses (`UserController`, `RoomController`, `GameController`). Each ActionCode enum value on the incoming packet maps to a method on the controller by name (case-sensitive reflection). Controllers return `MainPack` protobuf responses or null.

### Finite State Machine (Client AI)
Both Boss and Enemy AI use the same FSM pattern:
- `IState` interface: `OnEnter()`, `OnUpdate()`, `OnExit()`
- `FSM`/`BossStateMachine`: manages state transitions
- States extend `BossState` (abstract) or implement `IState` directly
- Boss path: `Client/Assets/Scripts/Game/AI/Boss/` (Idle, Patrol, Chase, Attack, Hurt, Death, etc.)
- Enemy path: `Client/Assets/Scripts/Game/AI/Enermy/` (Idle, Patrol, Chase, Attack, Hit, Death, React)

### Platform Generation
`InfinitePlatformGenerator` creates/destroys `DynamicPlatform` prefabs in a scrolling pattern. Platforms are pooled via `ObjectPool`. `PlatformData` ScriptableObjects define platform configurations.

### UI System
- `UIManager` manages panel lifecycle, caches `BasePanel` instances by `UIPanelType`
- `BaseUI` is the base for individual UI elements
- All panels are in `Client/Assets/Scripts/UIPanel/`

### Dependencies
- **Client**: Mirror, kcp2k (KCP transport), DOTween, TextMesh Pro, Google Protobuf, System.Buffers/Memory/Numerics
- **Server**: Google Protobuf 3.32.0, MySqlConnector 2.5.0, BouncyCastle, K4os (LZ4 compression), ZstdSharp

## Key Architectural Rules

- The **EventDispatcher** is the only communication channel between network code and UI. Never call UI methods directly from network handlers.
- `GameManager` (Unity singleton) holds all global game state. Access via `GameManager.Instance`.
- `GameLaunch.Initialize()` runs exactly once per process (guarded by `IsInit` static flag). It creates the persistent Canvas, EventSystem, and VirtualInputSystem GameObjects.
- Network message structs in `Client/Assets/Scripts/Network/Mirror/Messages/` must implement `NetworkMessage` interface and be structs (Mirror requirement).
- Server controllers handle one `RequestCode` each. Method names on controllers must match `ActionCode` enum values exactly.
