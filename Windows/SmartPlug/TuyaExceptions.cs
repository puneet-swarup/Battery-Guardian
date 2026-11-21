using System;

namespace BatteryGuardian.SmartPlug
{
    /// <summary>
    /// Raised when the plug answers in a way the client cannot interpret: a
    /// rejected control frame, a failed handshake, an unreadable reply.
    /// </summary>
    /// <remarks>
    /// This lives in the app rather than being the SDK's exception type so the
    /// controller's error classification does not depend on which client is
    /// underneath. <see cref="TuyaSmartPlug"/> translates.
    /// </remarks>
    public class TuyaProtocolException : Exception
    {
        public TuyaProtocolException(string message) : base(message) { }
        public TuyaProtocolException(string message, Exception inner) : base(message, inner) { }
    }
}
