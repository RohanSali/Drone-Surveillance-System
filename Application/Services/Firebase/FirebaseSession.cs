using System;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public static class FirebaseSession
    {
        private static readonly object _lock = new object();
        private static FirebaseUser? _current;

        public static FirebaseUser? Current
        {
            get { lock (_lock) return _current; }
        }

        public static void Set(FirebaseUser user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            lock (_lock) _current = user;
        }

        public static void Clear()
        {
            lock (_lock) _current = null;
        }
    }
}

