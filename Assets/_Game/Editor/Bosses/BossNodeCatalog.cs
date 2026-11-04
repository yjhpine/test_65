using ActionPlatformer.Units.Bosses;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ActionPlatformer.Editor
{
    // Every node type the inspector can add, found through reflection so new node classes appear without editor changes.
    public static class BossNodeCatalog
    {
        public const string FallbackGroup = "기타/";

        public static IReadOnlyList<Type> NodeTypes
        {
            get
            {
                var list = new List<Type>();
                foreach (var t in TypeCache.GetTypesDerivedFrom<BossSelectorNode>())
                    if (!IsTestType(t) && !t.IsAbstract && !t.IsGenericType && t.IsSerializable && t.GetConstructor(Type.EmptyTypes) != null) list.Add(t);
                return list.OrderBy(MenuPath, StringComparer.Ordinal).ThenBy(t => t.FullName, StringComparer.Ordinal).ToList();
            }
        }

        // Test assemblies are compiled into every Editor session; their stub nodes must never reach the add menu.
        private static bool IsTestType(Type t) => t.Assembly.GetReferencedAssemblies().Any(a => a.Name == "nunit.framework");

        public static string MenuPath(Type type)
        {
            var attribute = (BossNodeMenuAttribute)Attribute.GetCustomAttribute(type, typeof(BossNodeMenuAttribute), false);
            return attribute == null || string.IsNullOrEmpty(attribute.Path) ? FallbackGroup + type.Name : attribute.Path;
        }
    }
}
