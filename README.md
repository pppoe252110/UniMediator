![Unity Tests](https://github.com/pppoe252110/UniMediator/actions/workflows/tests.yml/badge.svg)
# UniMediator

**UniMediator** is a lightweight, high-performance Mediator pattern implementation designed specifically for **Unity**. It simplifies communication between different parts of your game by decoupling the sender of a request from its handler.

It supports synchronous and asynchronous workflows, notification broadcasting, data streaming, and powerful middleware pipelines—all with a focus on zero-allocation performance and `UniTask` integration.

---

## 🚀 Features

* **Decoupled Architecture:** Send requests or publish notifications without tight coupling.
* **UniTask Support:** Built from the ground up for `UniTask` to ensure allocation-free async operations.
* **Request/Response:** Synchronous and Asynchronous handlers for getting results.
* **Notifications:** Publish events to multiple handlers with `Sequential` or `Parallel` strategies.
* **Request Streams:** Handle data streams via `IUniTaskAsyncEnumerable`.
* **Pipeline Behaviors:** Middleware support for logging, validation, and profiling.
* **VContainer Integration:** Automatic handler scanning and registration.

---

## 📦 Installation

### Dependencies
* [UniTask](https://github.com/Cysharp/UniTask) (Required)
* [VContainer](https://github.com/hadashiA/VContainer) (Optional, for DI)

### Setup
Via git URL:
```url
https://github.com/pppoe252110/UniMediator.git?path=Assets/Plugins/UniMediator
```
Manual:
1.  Download the source or clone the repo.
2.  Place the `UniMediator` folder into your Unity `Assets` or `Packages` directory.

---

## 🛠 Usage

### 1. Basic Request/Handler
```csharp
public class HealRequest : IRequest<int> 
{
    public int Amount;
}

public class HealHandler : IRequestHandler<HealRequest, int> 
{
    public int Handle(HealRequest request) 
    {
        Debug.Log($"Healed for {request.Amount}");
        return 100; // Return new health
    }
}
```
### 2. Async Request
```csharp
public class LoadDataRequest : IAsyncRequest<string> { }

public class LoadDataHandler : IAsyncRequestHandler<LoadDataRequest, string> 
{
    public async UniTask<string> Handle(LoadDataRequest request, CancellationToken ct) 
    {
        await UniTask.Delay(1000, cancellationToken: ct);
        return "Data Loaded";
    }
}
```
### 3. Sending via Mediator
```csharp
// Send a sync request
int health = mediator.Send(new HealRequest { Amount = 20 });

// Send an async request
string data = await mediator.SendAsync(new LoadDataRequest());
```

---

## 💉 VContainer Integration

UniMediator includes an extension to automate the registration of all standard class handlers (POCOs) in your assembly. For **MonoBehaviours** already existing in your scene, you should register them manually using `AsImplementedInterfaces()`.

### 1. Example Handler (MonoBehaviour)
This component lives on a VFX Manager object in your scene. It listens for a "Player Hit" notification to trigger visual effects.

```csharp
public class PlayerHitNotification : INotification 
{
    public Vector3 HitPosition;
}

public class PlayerVFXManager : MonoBehaviour, INotificationHandler<PlayerHitNotification>
{
    [SerializeField] private ParticleSystem _bloodSplatPrefab;

    public void Handle(PlayerHitNotification notification)
    {
        Instantiate(_bloodSplatPrefab, notification.HitPosition, Quaternion.identity);
    }
}
```

### 2. Registration in LifetimeScope
Call RegisterMediator() to scan the assembly for standard handlers, then register your scene components specifically.

```csharp
using VContainer;
using VContainer.Unity;
using UniMediator.Runtime.VContainer;

public class GameAppScope : LifetimeScope
{
    [SerializeField] private PlayerVFXManager _vfxManager;

    protected override void Configure(IContainerBuilder builder)
    {
        // 1. Register the Mediator & scan assembly for POCO handlers
        builder.RegisterMediator();

        // 2. Register your game services
        builder.Register<CombatService>(Lifetime.Singleton);

        // 3. Register scene-based handlers
        // Use AsImplementedInterfaces() so the Mediator finds the INotificationHandler
        builder.RegisterComponent(_vfxManager)
               .AsImplementedInterfaces();
    }
}
```

### 3. Usage

```csharp
public class CombatService
{
    private readonly IMediator _mediator;

    // Injected by VContainer
    public CombatService(IMediator mediator) => _mediator = mediator;

    public void DealDamage(Vector3 position)
    {
        // ... damage logic ...

        // Trigger VFX via Mediator
        _mediator.Publish(new PlayerHitNotification { HitPosition = position });
    }
}
```
