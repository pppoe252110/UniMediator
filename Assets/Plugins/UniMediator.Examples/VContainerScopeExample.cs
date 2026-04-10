#if UNIMEDIATOR_VCONTAINER_INTEGRATION

using VContainer;
using VContainer.Unity;
using UniMediator.Runtime.VContainer;

namespace UniMediator.Examples
{
    public class VContainerScopeExample : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMediator();
        }
    }
}
#endif