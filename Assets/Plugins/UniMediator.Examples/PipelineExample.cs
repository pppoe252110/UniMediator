#if UNIMEDIATOR_UNITASK_INTEGRATION
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniMediator.Runtime;
using System.Collections.Generic;

namespace UniMediator.Examples
{
    // Request
    public class DeleteSaveRequest : IAsyncRequest<bool>
    {
        public string SaveSlot;
        public string UserToken;
    }

    // Handler (business logic only)
    public class DeleteSaveHandler : IAsyncRequestHandler<DeleteSaveRequest, bool>
    {
        public async UniTask<bool> Handle(DeleteSaveRequest request, CancellationToken token)
        {
            await UniTask.Delay(500); // simulate I/O
            return true; // success
        }
    }

    // Pipeline Behavior 1: Logging
    public class LoggingBehavior : IAsyncPipelineBehavior<DeleteSaveRequest, bool>
    {
        public async UniTask<bool> Handle(DeleteSaveRequest request, RequestHandlerDelegate<bool> next, CancellationToken token)
        {
            Debug.Log($"[LOG] Deleting save slot: {request.SaveSlot}");
            bool result = await next();
            Debug.Log($"[LOG] Deletion result: {result}");
            return result;
        }
    }

    // Pipeline Behavior 2: Permission Check
    public class PermissionBehavior : IAsyncPipelineBehavior<DeleteSaveRequest, bool>
    {
        public async UniTask<bool> Handle(DeleteSaveRequest request, RequestHandlerDelegate<bool> next, CancellationToken token)
        {
            if (request.UserToken != "AdminSecret")
            {
                Debug.LogError("Permission denied!");
                return false; // short‑circuit the pipeline
            }
            Debug.Log("Permission granted.");
            return await next();
        }
    }

    // Usage Example
    public class PipelineExample : MonoBehaviour
    {
        private IMediator _mediator;

        private void Awake()
        {
            _mediator = new Mediator(
                resolver: type =>
                {
                    if (type == typeof(IAsyncRequestHandler<DeleteSaveRequest, bool>))
                        return new DeleteSaveHandler();
                    return null;
                },
                multiResolver: type =>
                {
                    var list = new List<object>();
                    if (type == typeof(IAsyncPipelineBehavior<DeleteSaveRequest, bool>))
                    {
                        list.Add(new LoggingBehavior());
                        list.Add(new PermissionBehavior());
                    }
                    return list;
                }
            );
        }

        private async void Start()
        {
            var request = new DeleteSaveRequest { SaveSlot = "Slot1", UserToken = "WrongToken" };
            bool success = await _mediator.SendAsync(request);
            Debug.Log($"Final result: {success}"); // false (permission denied)

            request.UserToken = "AdminSecret";
            success = await _mediator.SendAsync(request);
            Debug.Log($"Final result: {success}"); // true, with logging
        }
    }
}
#endif
