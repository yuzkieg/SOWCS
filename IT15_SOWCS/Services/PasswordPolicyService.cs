using IT15_SOWCS.Models;

namespace IT15_SOWCS.Services
{
    public static class PasswordPolicyService
    {
        public const int PasswordExpiryMonths = 2;

        public static bool HasLocalPassword(Users user)
        {
            return !string.IsNullOrWhiteSpace(user?.PasswordHash);
        }

        public static DateTime GetPasswordChangedBaselineUtc(Users user)
        {
            if (user.LastPasswordChangedDateUtc.HasValue)
            {
                return DateTime.SpecifyKind(user.LastPasswordChangedDateUtc.Value, DateTimeKind.Utc);
            }

            if (user.UpdatedDate != default)
            {
                return DateTime.SpecifyKind(user.UpdatedDate, DateTimeKind.Utc);
            }

            if (user.CreatedDate != default)
            {
                return DateTime.SpecifyKind(user.CreatedDate, DateTimeKind.Utc);
            }

            return DateTime.UtcNow;
        }

        public static bool IsPasswordExpired(Users user, DateTime? nowUtc = null)
        {
            if (user == null || !HasLocalPassword(user))
            {
                return false;
            }

            var now = nowUtc ?? DateTime.UtcNow;
            return GetPasswordChangedBaselineUtc(user).AddMonths(PasswordExpiryMonths) <= now;
        }

        public static void StampPasswordChanged(Users user, DateTime? changedAtUtc = null)
        {
            var timestamp = changedAtUtc ?? DateTime.UtcNow;
            user.LastPasswordChangedDateUtc = timestamp;
            user.UpdatedDate = timestamp;
        }
    }
}
