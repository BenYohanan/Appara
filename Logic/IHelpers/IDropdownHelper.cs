using Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logic.IHelpers
{
    public interface IDropdownHelper
    {
        Task<List<ApplicationUser>> GetAllUsersDropdownAsync();
    }
}
