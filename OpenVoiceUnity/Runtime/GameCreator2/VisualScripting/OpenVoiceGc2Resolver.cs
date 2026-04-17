using FishNet.Object;
using GameCreator.Runtime.Common;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    internal static class OpenVoiceGc2Resolver
    {
        public static PlayerVoice ResolveVoice(
            PropertyGetGameObject source,
            Args args,
            PlayerVoice fallback,
            bool requireOwnership
        )
        {
            if (source != null)
            {
                GameObject target = source.Get(args);
                if (target != null && target.TryGetComponent(out PlayerVoice fromProperty))
                    return IsAllowed(fromProperty, requireOwnership) ? fromProperty : null;
            }

            if (fallback != null)
                return IsAllowed(fallback, requireOwnership) ? fallback : null;

            if (args.Self != null && args.Self.TryGetComponent(out PlayerVoice fromSelf))
                return IsAllowed(fromSelf, requireOwnership) ? fromSelf : null;

            if (args.Target != null && args.Target.TryGetComponent(out PlayerVoice fromTarget))
                return IsAllowed(fromTarget, requireOwnership) ? fromTarget : null;

            return null;
        }

        public static bool IsAllowed(PlayerVoice voice, bool requireOwnership)
        {
            if (!requireOwnership) return true;
            return voice is NetworkBehaviour networkBehaviour && networkBehaviour.IsOwner;
        }
    }
}
