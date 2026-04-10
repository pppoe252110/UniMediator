#if UNIMEDIATOR_VCONTAINER_INTEGRATION

using System;
using System.Collections.Generic;

namespace UniMediator.Runtime.VContainer
{
    public class MediatorRegistrationOptions
    {
        internal HashSet<Type> IgnoredTypes { get; } = new();

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
    }
}

#endif