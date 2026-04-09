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
1.  Download the source or clone the repo.
2.  Place the `UniMediator` folder into your Unity `Assets` or `Packages` directory.
3.  **If using VContainer:** Add `UNIMEDIATOR_VCONTAINER_INTEGRATION` to your **Project Settings > Player > Scripting Define Symbols**.

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
string data = await mediator.Send(new LoadDataRequest());
```

---

## 💉 VContainer Integration
UniMediator includes an extension to automate the registration of all handlers within your assembly.
### Step 1: Create the Extension
Add this utility to your project to enable the `RegisterMediator` syntax:
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VContainer;
using VContainer.Unity;
using UniMediator.Runtime;

public static class UniMediatorVContainerExtensions
{
    public static void RegisterMediator(this IContainerBuilder builder, Assembly assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        // Register Mediator Singleton
        builder.Register<IMediator>(resolver => new Mediator(
            type => resolver.Resolve(type),
            type => (System.Collections.IEnumerable)resolver.Resolve(typeof(IEnumerable<>).MakeGenericType(type))
        ), Lifetime.Singleton);

        // Scan for Handlers
        var handlerTypes = new[] { 
            typeof(IRequestHandler<,>), typeof(IAsyncRequestHandler<,>),
            typeof(INotificationHandler<>), typeof(IPipelineBehavior<,>) 
            /* Add others as needed */ 
        };

        var implementations = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface);

        foreach (var type in implementations)
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                if (handlerTypes.Contains(iface.GetGenericTypeDefinition()))
                {
                    builder.Register(type, Lifetime.Transient).As(iface);
                }
            }
        }
    }
}
```

### Step 2: Use in LifetimeScope
```csharp
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Automatically finds all handlers in this assembly
        builder.RegisterMediator();
    }
}
```