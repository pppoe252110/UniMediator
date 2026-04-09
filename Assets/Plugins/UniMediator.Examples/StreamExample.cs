using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniMediator.Runtime;
using Cysharp.Threading.Tasks.Linq;

namespace UniMediator.Examples
{
    // Stream Request
    public class ChatStreamRequest : IStreamRequest<ChatMessage>
    {
        public string Channel;
        public int MaxMessages = 5;
    }

    public class ChatMessage
    {
        public string Sender;
        public string Text;
    }

    // Stream Handler
    public class ChatStreamHandler : IStreamHandler<ChatStreamRequest, ChatMessage>
    {
        public IUniTaskAsyncEnumerable<ChatMessage> Handle(ChatStreamRequest request, CancellationToken token)
        {
            return UniTaskAsyncEnumerable.Create<ChatMessage>(async (writer, ct) =>
            {
                for (int i = 0; i < request.MaxMessages; i++)
                {
                    // Simulate receiving a message every 500ms
                    await UniTask.Delay(500, cancellationToken: ct);

                    var msg = new ChatMessage
                    {
                        Sender = $"User{i}",
                        Text = $"Hello from {request.Channel} #{i}"
                    };

                    await writer.YieldAsync(msg);
                }
            });
        }
    }

    // Usage Example
    public class StreamExample : MonoBehaviour
    {
        private IMediator _mediator;
        private CancellationTokenSource _cts;
        private readonly List<ChatMessage> _received = new List<ChatMessage>();

        private void Awake()
        {
            _mediator = new Mediator(
                type => type == typeof(IStreamHandler<ChatStreamRequest, ChatMessage>)
                    ? new ChatStreamHandler() : null
            );
        }

        private async void Start()
        {
            _cts = new CancellationTokenSource();

            // Optional: cancel after 2 seconds
            _cts.CancelAfter(2000);

            var request = new ChatStreamRequest { Channel = "General", MaxMessages = 10 };
            var stream = _mediator.CreateStream(request, _cts.Token);

            try
            {
                await foreach (var msg in stream.WithCancellation(_cts.Token))
                {
                    _received.Add(msg);
                    Debug.Log($"[{msg.Sender}]: {msg.Text}");
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("Stream was cancelled.");
            }

            Debug.Log($"Total messages received: {_received.Count}");
        }

        private void OnDestroy() => _cts?.Dispose();
    }
}