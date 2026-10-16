using Core.DbContext;
using Core.Models;
using Logic.IHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Logic.Helpers
{
    public class DropdownHelper : IDropdownHelper
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public DropdownHelper(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<List<ApplicationUser>> GetAllUsersDropdownAsync()
        {
            var common = new ApplicationUser
            {
                Id = "0000-0000",
                UserName = "-- Select --",
            };

            var users = await _userManager.Users
                .Where(u => !u.Deleted && u.FirstName != "SuperAdmin")
                .Select(u => new ApplicationUser
                {
                    Id = u.Id,
                    UserName = u.FirstName + " " + u.LastName,
                })
                .ToListAsync();

            users.Insert(0, common);

            return users;
        }
       
    }

}
