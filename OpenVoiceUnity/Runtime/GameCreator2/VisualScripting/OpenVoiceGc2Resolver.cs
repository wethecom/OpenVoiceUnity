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
            GameObject sourceFallback,
            Args args,
            PlayerVoice fallback,
            bool requireOwnership
        )
        {
            if (source != null)
            {
                GameObject fromProperty = source.Get(args);
                if (fromProperty != null && fromProperty.TryGetComponent(out PlayerVoice fromSourceProperty))
                    return IsAllowed(fromSourceProperty, requireOwnership) ? fromSourceProperty : null;
            }

            if (sourceFallback != null && sourceFallback.TryGetComponent(out PlayerVoice fromSource))
                return IsAllowed(fromSource, requireOwnership) ? fromSource : null;

            if (fallback != null)
                return IsAllowed(fallback, requireOwnership) ? fallback : null;

            if (args != null && args.Self != null && args.Self.TryGetComponent(out PlayerVoice fromSelf))
                return IsAllowed(fromSelf, requireOwnership) ? fromSelf : null;

            if (args != null && args.Target != null && args.Target.TryGetComponent(out PlayerVoice fromTarget))
                return IsAllowed(fromTarget, requireOwnership) ? fromTarget : null;

            return null;
        }

        public static OpenVoiceVoicePreferences ResolvePreferences(
            PropertyGetGameObject source,
            GameObject sourceFallback,
            Args args,
            OpenVoiceVoicePreferences fallback
        )
        {
            if (source != null)
            {
                GameObject fromProperty = source.Get(args);
                if (fromProperty != null && fromProperty.TryGetComponent(out OpenVoiceVoicePreferences fromSourceProperty))
                    return fromSourceProperty;
            }

            if (sourceFallback != null && sourceFallback.TryGetComponent(out OpenVoiceVoicePreferences fromSource))
                return fromSource;

            if (fallback != null)
                return fallback;

            if (args != null && args.Self != null && args.Self.TryGetComponent(out OpenVoiceVoicePreferences fromSelf))
                return fromSelf;

            if (args != null && args.Target != null && args.Target.TryGetComponent(out OpenVoiceVoicePreferences fromTarget))
                return fromTarget;

            return null;
        }

        public static GameObject ResolveGameObject(
            PropertyGetGameObject source,
            GameObject sourceFallback,
            Args args,
            GameObject fallback
        )
        {
            if (source != null)
            {
                GameObject fromProperty = source.Get(args);
                if (fromProperty != null)
                    return fromProperty;
            }

            if (sourceFallback != null)
                return sourceFallback;

            if (fallback != null)
                return fallback;

            if (args != null && args.Self != null)
                return args.Self;

            if (args != null && args.Target != null)
                return args.Target;

            return null;
        }

        public static bool IsAllowed(PlayerVoice voice, bool requireOwnership)
        {
            if (!requireOwnership) return true;
            return voice is NetworkBehaviour networkBehaviour && networkBehaviour.IsOwner;
        }
    }
}
