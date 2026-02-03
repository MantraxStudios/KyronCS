using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using KrayonCore;

namespace KrayonCore
{
    public static class ComponentRegistry
    {
        public static readonly List<Type> Components;

        static ComponentRegistry()
        {
            Components = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    t.IsClass &&
                    !t.IsAbstract &&
                    t.IsSubclassOf(typeof(Component)))
                .OrderBy(t => t.Name)
                .ToList();
        }
    }
}