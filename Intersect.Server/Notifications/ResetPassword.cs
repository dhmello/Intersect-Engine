﻿using Intersect.Server.Database.PlayerData;
using Intersect.Server.Localization;
using Intersect.Utilities;

namespace Intersect.Server.Notifications
{

    public partial class PasswordResetEmail : Notification
    {

        public PasswordResetEmail(User user) : base(user.Email)
        {
            LoadFromTemplate("PasswordReset", user.Name);
            Subject = Strings.PasswordResetNotification.Subject;
            var resetCode = GenerateResetCode(6);
            Body = Body.Replace("{{code}}", resetCode);
            Body = Body.Replace("{{expiration}}", Options.Instance.ValidPasswordResetTimeMinutes.ToString());
            user.PasswordResetCode = resetCode;
            user.PasswordResetTime = DateTime.UtcNow.AddMinutes(Options.Instance.ValidPasswordResetTimeMinutes);
            user.Save();
        }

        private string GenerateResetCode(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, length).Select(s => s[Randomization.Next(s.Length)]).ToArray());
        }

    }

}
