﻿using EMailService.Service;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace OnlineDataBuilder.HostedService.Services
{
    public class NotificationEmailJob
    {
        public async static Task SendNotificationEmail(IServiceProvider _serviceProvider)
        {
            // IEMailManager _eMailManager = _serviceProvider.GetRequiredService<IEMailManager>();
            // await _eMailManager.SendMailAsync(null);
            await Task.CompletedTask;
        }
    }
}
