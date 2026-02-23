using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace KrayonCore
{
    public static class ComponentRegistry
    {
        public static readonly List<Type> Components;

        static ComponentRegistry()
        {
            Components = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                IEnumerable<Type> types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null)!;
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    try
                    {
                        if (t != null && t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Component)))
                            Components.Add(t);
                    }
                    catch { }
                }
            }

            Components = Components.OrderBy(t => t.Name).ToList();
        }
    }
}