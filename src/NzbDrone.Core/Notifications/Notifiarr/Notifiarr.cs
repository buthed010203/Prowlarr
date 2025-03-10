using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Notifiarr
{
    public class Notifiarr : WebhookBase<NotifiarrSettings>
    {
        private readonly INotifiarrProxy _proxy;

        public Notifiarr(INotifiarrProxy proxy, IConfigFileProvider configFileProvider)
            : base(configFileProvider)
        {
            _proxy = proxy;
        }

        public override string Link => "https://notifiarr.com";
        public override string Name => "Notifiarr";

        public override void OnGrab(GrabMessage grabMessage)
        {
            _proxy.SendNotification(BuildGrabPayload(grabMessage), Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            _proxy.SendNotification(BuildHealthPayload(healthCheck), Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            _proxy.SendNotification(BuildApplicationUploadPayload(updateMessage), Settings);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(SendWebhookTest());

            return new ValidationResult(failures);
        }

        private ValidationFailure SendWebhookTest()
        {
            try
            {
                _proxy.SendNotification(BuildTestPayload(), Settings);
            }
            catch (NotifiarrException ex)
            {
                return new NzbDroneValidationFailure("APIKey", ex.Message);
            }

            return null;
        }
    }
}
