using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UniMediator.Runtime;

namespace UniMediator.Tests
{
    // -----------------------------------------------------------------------------
    // Base class providing the mediator setup and registration helpers
    // -----------------------------------------------------------------------------
    public abstract class MediatorTestBase
    {
        protected Dictionary<Type, List<object>> Services { get; private set; }
        protected Mediator Mediator { get; private set; }

        [SetUp]
        public void BaseSetUp()
        {
            Services = new Dictionary<Type, List<object>>();
            Mediator = new Mediator(
                resolver: type => Services.TryGetValue(type, out var list) && list.Count > 0 ? list[0] : null,
                multiResolver: type => Services.TryGetValue(type, out var list) ? list : Enumerable.Empty<object>()
            );
        }

        protected void Register<TService>(object instance)
        {
            var type = typeof(TService);
            if (!Services.ContainsKey(type))
                Services[type] = new List<object>();
            Services[type].Add(instance);
        }
    }
}