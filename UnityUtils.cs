using System.Linq;
using UnityEngine;

namespace DvMod.SteamCutoff
{
    public static class UnityUtils
    {
        public static string GetPath(this Component c)
        {
            return string.Join("/", c.GetComponentsInParent<Transform>(true).Reverse().Select(c => c.name));
        }

        public static Vector3 CurrentDisplacement(this Joint j)
        {
            return j.transform.InverseTransformPoint(j.connectedBody.transform.TransformPoint(j.connectedAnchor)) - j.anchor;
        }
    }
}