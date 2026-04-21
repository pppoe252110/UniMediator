#if UNIMEDIATOR_VCONTAINER_INTEGRATION

using System;
using System.Collections.Generic;
using System.Reflection;
using VContainer;

namespace UniMediator.Runtime.VContainer
{
    public class MediatorRegistrationOptions
    {
        internal HashSet<Type> IgnoredTypes { get; } = new();
        internal HashSet<Assembly> AssembliesToScan { get; } = new();
        internal Lifetime DefaultLifetime { get; private set; } = Lifetime.Transient;

        /// <summary>
        /// Ignores the types of the provided objects during Mediator handler scanning.
        /// </summary>
        public MediatorRegistrationOptions Ignore(params object[] objects)
        {
            if (objects != null)
            {
                foreach (var obj in objects)
                    IgnoredTypes.Add(obj.GetType());
            }
            return this;
        }

        /// <summary>
        /// Ignores the specified types during Mediator handler scanning.
        /// </summary>
        public MediatorRegistrationOptions IgnoreTypes(params Type[] types)
        {
            if (types != null)
            {
                foreach (var t in types)
                    IgnoredTypes.Add(t);
            }
            return this;
        }

        /// <summary>
        /// Specifies which assemblies to scan for handlers. 
        /// If not called, defaults to the calling assembly.
        /// </summary>
        public MediatorRegistrationOptions WithAssemblies(params Assembly[] assemblies)
        {
            if (assemblies != null)
            {
                foreach (var asm in assemblies)
                    AssembliesToScan.Add(asm);
            }
            return this;
        }

        /// <summary>
        /// Sets the default VContainer lifetime for registered handlers and behaviors.
        /// Defaults to Transient.
        /// </summary>
        public MediatorRegistrationOptions WithDefaultLifetime(Lifetime lifetime)
        {
            DefaultLifetime = lifetime;
            return this;
        }
    }
}

#endif