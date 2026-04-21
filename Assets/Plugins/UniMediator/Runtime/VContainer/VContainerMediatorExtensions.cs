#if UNIMEDIATOR_VCONTAINER_INTEGRATION

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VContainer;

namespace UniMediator.Runtime.VContainer
{
    public static class UniMediatorVContainerExtensions
    {
        /// <summary>
        /// Registers Mediator and scans the specified assemblies for handlers.
        /// </summary>
        /// <param name="builder">The container builder.</param>
        /// <param name="configure">Optional configuration action.</param>
        public static void RegisterMediator(this IContainerBuilder builder, Action<MediatorRegistrationOptions> configure = null)
        {
            var options = new MediatorRegistrationOptions();
            configure?.Invoke(options);

            // Determine which assemblies to scan
            var assemblies = options.AssembliesToScan.Count > 0
                ? options.AssembliesToScan
                : new HashSet<Assembly> { Assembly.GetCallingAssembly() };

            var ignoreSet = options.IgnoredTypes;
            var defaultLifetime = options.DefaultLifetime;

            // 1. Register the Mediator itself
            builder.Register<IMediator>(resolver => new Mediator(
                type => resolver.Resolve(type),
                type => (IEnumerable<object>)resolver.Resolve(typeof(IEnumerable<>).MakeGenericType(type))
            ), Lifetime.Singleton);

            // 2. Target open generic interfaces
            var targetOpenGenerics = new HashSet<Type>
            {
                typeof(IRequestHandler<,>),
                typeof(IRequestHandler<>),
                typeof(IPipelineBehavior<,>),
                typeof(INotificationHandler<>),
#if UNIMEDIATOR_UNITASK_INTEGRATION
                typeof(IStreamHandler<,>),
                typeof(IAsyncRequestHandler<,>),
                typeof(IAsyncRequestHandler<>),
                typeof(IAsyncNotificationHandler<>),
                typeof(IAsyncPipelineBehavior<,>)
#endif
            };

            // 3. Scan and register across all specified assemblies, skipping ignored types
            var implementations = assemblies
                .SelectMany(asm => asm.GetTypes())
                .Where(t => t is { IsAbstract: false, IsInterface: false })
                .Where(t => !typeof(MonoBehaviour).IsAssignableFrom(t))
                .Where(t => !ignoreSet.Contains(t));

            foreach (var type in implementations)
            {
                foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
                {
                    if (targetOpenGenerics.Contains(iface.GetGenericTypeDefinition()))
                    {
                        builder.Register(type, defaultLifetime).As(iface);
                    }
                }
            }
        }
    }
}
#endif