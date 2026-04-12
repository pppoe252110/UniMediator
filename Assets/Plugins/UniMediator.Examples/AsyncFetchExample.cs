#if UNIMEDIATOR_UNITASK_INTEGRATION
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniMediator.Runtime;

namespace UniMediator.Examples
{
    // Async Request
    public class FetchPlayerDataRequest : IAsyncRequest<PlayerData>
    {
        public string PlayerId { get; set; }
    }

    public class PlayerData
    {
        public string Name;
        public int Level;
    }

    // Async Handler (simulates network delay)
    public class FetchPlayerDataHandler : IAsyncRequestHandler<FetchPlayerDataRequest, PlayerData>
    {
        public async UniTask<PlayerData> Handle(FetchPlayerDataRequest request, CancellationToken token)
        {
            // Simulate 2-second network call
            await UniTask.Delay(2000, cancellationToken: token);

            // Cancellation will throw if token was cancelled
            return new PlayerData { Name = "Hero", Level = 42 };
        }
    }

    // Usage Example
    public class AsyncFetchExample : MonoBehaviour
    {
        private IMediator _mediator;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            _mediator = new Mediator(
                type => type == typeof(IAsyncRequestHandler<FetchPlayerDataRequest, PlayerData>)
                    ? new FetchPlayerDataHandler() : null
            );
        }

        private async void Start()
        {
            _cts = new CancellationTokenSource();

            // Cancel after 1 second (handler takes 2 seconds → will cancel)
            _cts.CancelAfter(1000);

            try
            {
                var request = new FetchPlayerDataRequest { PlayerId = "abc123" };
                PlayerData data = await _mediator.SendAsync(request, _cts.Token);
                Debug.Log($"Loaded: {data.Name} Lv.{data.Level}");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("Request was cancelled (timeout)");
            }
        }

        private void OnDestroy() => _cts?.Dispose();
    }
}
#endif