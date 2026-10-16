using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Enums
{
    public class ApparcusEnum
    {
        public enum DropdownEnums
        {
            Gender = 1
        }
		public enum PaymentStatus
		{
			Pending,
			Successful,
			Failed,
			UnderPaid,
			OverPaid,
			Expired,
			Refunded
		}
	}
}
