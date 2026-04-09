using UnityEngine;
using UniMediator.Runtime;

namespace UniMediator.Examples
{
    // Request & Response
    public class ScoreRequest : IRequest<string>
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
    }

    // Handler
    public class ScoreHandler : IRequestHandler<ScoreRequest, string>
    {
        public string Handle(ScoreRequest request)
        {
            float kd = request.Deaths == 0 ? request.Kills : (float)request.Kills / request.Deaths;
            return $"K/D: {kd:F2}";
        }
    }

    // Usage Example (attach to a GameObject)
    public class ScoreExample : MonoBehaviour
    {
        private IMediator _mediator;

        private void Awake()
        {
            // Manual registration (in a real project you'd use dependency injection)
            _mediator = new Mediator(
                type => type == typeof(IRequestHandler<ScoreRequest, string>) ? new ScoreHandler() : null
            );
        }

        private void Start()
        {
            var request = new ScoreRequest { Kills = 15, Deaths = 3 };
            string result = _mediator.Send(request);
            Debug.Log(result); // Output: "K/D: 5.00"
        }
    }
}