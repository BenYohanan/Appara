using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Logic.Services
{
	public class PaymentHub : Hub
	{
		public async Task JoinContributionGroup(string contributionId)
		{
			await Groups.AddToGroupAsync(
				Context.ConnectionId,
				contributionId);
		}
	}
}
