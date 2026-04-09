#if UNIMEDIATOR_VCONTAINER_INTEGRATION

using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;
using UniMediator.Runtime;

namespace UniMediator.Examples
{
    public static class UniMediatorVContainerExtensions
    {
        public static void RegisterMediator(this IContainerBuilder builder)
        {
            // Use the assembly where the LifetimeScope is defined
            var assembly = System.Reflection.Assembly.GetCallingAssembly();

            // 1. Register the Mediator
            builder.Register<IMediator>(resolver => new Mediator(
                type => resolver.Resolve(type),
                type => (IEnumerable<object>)resolver.Resolve(typeof(IEnumerable<>).MakeGenericType(type))
            ), Lifetime.Singleton);

            // 2. Define the target interfaces
            var targetOpenGenerics = new HashSet<Type>
            {
                typeof(IRequestHandler<,>), typeof(IAsyncRequestHandler<,>),
                typeof(IRequestHandler<>), typeof(IAsyncRequestHandler<>),
                typeof(INotificationHandler<>), typeof(ISyncNotificationHandler<>),
                typeof(IStreamHandler<,>), typeof(IPipelineBehavior<,>),
                typeof(IAsyncPipelineBehavior<,>)
            };

            // 3. Scan and Register
            var implementations = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false });

            foreach (var type in implementations)
            {
                foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
                {
                    if (targetOpenGenerics.Contains(iface.GetGenericTypeDefinition()))
                    {
                        builder.Register(type, Lifetime.Transient).As(iface);
                    }
                }
            }
        }
    }
}
#endif