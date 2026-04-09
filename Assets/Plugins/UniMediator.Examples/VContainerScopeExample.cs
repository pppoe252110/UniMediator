#if UNIMEDIATOR_VCONTAINER_INTEGRATION

using VContainer;
using VContainer.Unity;

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