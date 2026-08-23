using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightOwlZzz.Koikatsu.BodyMaskLayers
{
    internal static class BodyMaskControllerRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, BodyMaskCharacterController> MaterialTargets =
            new Dictionary<int, BodyMaskCharacterController>();
        private static readonly List<BodyMaskCharacterController> Controllers =
            new List<BodyMaskCharacterController>();

        public static void RegisterController(BodyMaskCharacterController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException("controller");
            }

            lock (SyncRoot)
            {
                if (!Controllers.Contains(controller))
                {
                    Controllers.Add(controller);
                }
            }
        }

        public static void UnregisterController(BodyMaskCharacterController controller)
        {
            if (controller == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                Controllers.Remove(controller);
            }
        }

        public static int BindMaterial(Material material, BodyMaskCharacterController controller)
        {
            if (material == null)
            {
                throw new ArgumentNullException("material");
            }

            if (controller == null)
            {
                throw new ArgumentNullException("controller");
            }

            int materialId = material.GetInstanceID();
            lock (SyncRoot)
            {
                MaterialTargets[materialId] = controller;
            }

            return materialId;
        }

        public static void UnbindMaterial(int materialId, BodyMaskCharacterController controller)
        {
            if (materialId == 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                BodyMaskCharacterController existing;
                if (MaterialTargets.TryGetValue(materialId, out existing) && existing == controller)
                {
                    MaterialTargets.Remove(materialId);
                }
            }
        }

        public static bool TryGetByMaterial(
            Material material,
            out BodyMaskCharacterController controller)
        {
            controller = null;
            if (material == null)
            {
                return false;
            }

            lock (SyncRoot)
            {
                return MaterialTargets.TryGetValue(material.GetInstanceID(), out controller) &&
                       controller != null;
            }
        }

        public static BodyMaskCharacterController[] SnapshotControllers()
        {
            lock (SyncRoot)
            {
                return Controllers.ToArray();
            }
        }

        public static void ForEachController(Action<BodyMaskCharacterController> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            lock (SyncRoot)
            {
                for (int i = 0; i < Controllers.Count; i++)
                {
                    BodyMaskCharacterController controller = Controllers[i];
                    if (controller != null)
                    {
                        action(controller);
                    }
                }
            }
        }
    }
}
