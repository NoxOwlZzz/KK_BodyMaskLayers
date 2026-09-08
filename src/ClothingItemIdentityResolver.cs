using System;
using System.Reflection;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    public static class ClothingItemIdentityResolver
    {
        private static readonly Type ResolverType = Type.GetType(
            GameTarget.SideloaderResolverType, false);
        private static readonly MethodInfo TryGetResolutionInfo = FindResolutionMethod();
        private static readonly MethodInfo TryGetExternalResolutionInfo = FindExternalResolutionMethod();

        public static ClothingItemIdentity Resolve(ChaControl character, ClothingSlot slot)
        {
            ClothingItemIdentity identity = new ClothingItemIdentity
            {
                Slot = slot,
                Category = (int)ClothingSlotRegistry.GetCategory(slot),
                LocalItemId = 0,
                OriginalItemId = 0
            };

            int index = (int)slot;
            if (character == null || character.nowCoordinate == null ||
                character.nowCoordinate.clothes == null ||
                character.nowCoordinate.clothes.parts == null ||
                index < 0 || index >= character.nowCoordinate.clothes.parts.Length)
            {
                return identity;
            }

            identity.LocalItemId = character.nowCoordinate.clothes.parts[index].id;
            identity.OriginalItemId = identity.LocalItemId;
            if (character.infoClothes != null && index < character.infoClothes.Length &&
                character.infoClothes[index] != null)
            {
                identity.Category = character.infoClothes[index].Category;
                identity.DisplayName = character.infoClothes[index].Name;
            }

            TryResolveSideloader(identity);
            return identity;
        }

        private static void TryResolveSideloader(ClothingItemIdentity identity)
        {
            if (TryGetResolutionInfo == null || identity.LocalItemId <= 0)
            {
                return;
            }

            try
            {
                object resolved = TryGetResolutionInfo.Invoke(null, new object[]
                {
                    (ChaListDefine.CategoryNo)identity.Category,
                    identity.LocalItemId
                });
                if (resolved == null)
                {
                    return;
                }

                Type type = resolved.GetType();
                identity.SideloaderGuid = ReadProperty<string>(type, resolved, "GUID");
                identity.OriginalItemId = ReadProperty<int>(type, resolved, "Slot");
            }
            catch (Exception exception)
            {
                BodyMaskLayersPlugin.LogDebug("Sideloader identity lookup failed: " + exception.Message);
            }
        }

        public static int ResolveExternalLocalItemId(int originalItemId, int category, string modGuid)
        {
            if (TryGetExternalResolutionInfo == null || originalItemId <= 0 ||
                string.IsNullOrEmpty(modGuid))
            {
                return originalItemId;
            }

            try
            {
                object resolved = TryGetExternalResolutionInfo.Invoke(null, new object[]
                {
                    originalItemId,
                    (ChaListDefine.CategoryNo)category,
                    modGuid
                });
                if (resolved == null)
                {
                    return originalItemId;
                }

                int localSlot = ReadProperty<int>(resolved.GetType(), resolved, "LocalSlot");
                return localSlot > 0 ? localSlot : originalItemId;
            }
            catch (Exception exception)
            {
                BodyMaskLayersPlugin.LogDebug(
                    "Sideloader external item lookup failed: " + exception.Message);
                return originalItemId;
            }
        }

        private static MethodInfo FindResolutionMethod()
        {
            if (ResolverType == null)
            {
                return null;
            }

            MethodInfo[] methods = ResolverType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "TryGetResolutionInfo")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(ChaListDefine.CategoryNo) &&
                    parameters[1].ParameterType == typeof(int))
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindExternalResolutionMethod()
        {
            if (ResolverType == null)
            {
                return null;
            }

            MethodInfo[] methods = ResolverType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "TryGetResolutionInfo")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(ChaListDefine.CategoryNo) &&
                    parameters[2].ParameterType == typeof(string))
                {
                    return method;
                }
            }

            return null;
        }

        private static T ReadProperty<T>(Type type, object instance, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return default(T);
            }

            object value = property.GetValue(instance, null);
            return value is T ? (T)value : default(T);
        }
    }
}
