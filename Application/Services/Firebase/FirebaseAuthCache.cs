using System;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseAuthCache
    {
        public string FirebaseRefreshToken { get; set; } = string.Empty;
        public string Uid { get; set; } = string.Empty;
        public string AppClientId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

