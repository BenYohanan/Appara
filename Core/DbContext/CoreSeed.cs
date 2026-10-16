using Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Core.Enums.ApparcusEnum;

namespace Core.DbContext
{
    public static class CoreSeed
    {
        public static void SeedData(AppDbContext context, RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            SeedDropdowns(context).Wait();
            SeedRoles(roleManager).Wait();
            SeedUsers(userManager).Wait();
        }

        private static async Task SeedDropdowns(AppDbContext context)
        {
            var existingNames = await context.DropDowns
                .AsNoTracking()
                .Select(d => d.Name)
                .ToListAsync();

            var dropdowns = new List<DropDown>
            {
                new() { DropdownKey = DropdownEnums.Gender, Name = "Male" },
                new() { DropdownKey = DropdownEnums.Gender, Name = "Female" },
                new() { DropdownKey = DropdownEnums.Gender, Name = "Prefer not to say" }
            };

            var newDropdowns = dropdowns
                .Where(s => !existingNames.Contains(s.Name))
                .Select(d => new DropDown
                {
                    Name = d.Name,
                    DropdownKey = d.DropdownKey
                }).ToList();

            if (newDropdowns.Count != 0)
            {
                context.DropDowns.AddRange(newDropdowns);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in SeedItems.DefaultRoles())
            {
                if (!await roleManager.RoleExistsAsync(role.Name))
                {
                    await roleManager.CreateAsync(role);
                }
            }
        }

        private static async Task SeedUsers(UserManager<ApplicationUser> userManager)
        {
            foreach (var user in SeedItems.DefaultUsers())
            {
                var existingUser = await userManager.FindByEmailAsync(user.Email);
                if (existingUser == null)
                {
                    await userManager.CreateAsync(user, "11111").ConfigureAwait(false);
                    await userManager.AddToRoleAsync(user, user?.FirstName?.ToUpper()).ConfigureAwait(false);
                }
            }
        }
    }

    public static class SeedItems
    {
        public const string SuperAdminRole = "SuperAdmin";
        public const string AdminRole = "Admin";
        public const string UserRole = "User";

        public const string SuperAdminId = "7B0030007800640033006600640035003000";
        public const string SystemAdminId = "30007800360034003800300030003";
        public const string SystemUserId = "723778003600312338003000495676";

        public static string SuperAdminDashboard = "/SuperAdmin/Home";
        public static string AdminDashboard = "/Admin/Index";
        public static string UserDashboard = "/User/Index";

        public static IList<IdentityRole> DefaultRoles() => new List<IdentityRole>
        {
            new IdentityRole { Name = SuperAdminRole, NormalizedName = SuperAdminRole.ToUpper(), ConcurrencyStamp = "5002C0030007800380036002C00300078" },
            new IdentityRole { Name = AdminRole, NormalizedName = AdminRole.ToUpper(), ConcurrencyStamp = "14f4bf73-9b1a-415f-9b47-626ca87f6c0e" },
            new IdentityRole { Name = UserRole, NormalizedName = UserRole.ToUpper(), ConcurrencyStamp = "0DB45C30-2FEE-47C6-AF34-7849A62B8856" }
        };

        public static IList<ApplicationUser> DefaultUsers() => new List<ApplicationUser>
        {
            new() {
                Id = SuperAdminId,
                Email = "benyohanan@apparcus.com",
                NormalizedEmail = "BENYOHANAN@APPARCUS.COM",
                NormalizedUserName = "BENYOHANAN@APPARCUS.COM",
                UserName = "benyohanan@apparcus.com",
                PasswordHash = "AQAAAAEAACcQAAAAEO3NQwqwWgetIJ/tyYRIrobEpEcvQ47xoczshXUgLyKKSuanh+CiKz//sKDMCq+PCA==",
                FirstName = "SuperAdmin",
                PhoneNumber = "0000 000 0000",
                Deleted = false,
                DateCreated = DateTime.Now
            },
            new() {
                Id = SystemAdminId,
                Email = "admin@apparcus.com",
                NormalizedEmail = "ADMIN@APPARCUS.COM",
                NormalizedUserName = "ADMIN@APPARCUS.COM",
                UserName = "admin@apparcus.com",
                PasswordHash = "AQAAAAEAACcQAAAAEO3NQwqwWgetIJ/tyYRIrobEpEcvQ47xoczshXUgLyKKSuanh+CiKz//sKDMCq+PCA==",
                FirstName = "Admin",
                PhoneNumber = "0000 000 0000",
                Deleted = false,
                DateCreated = DateTime.Now
            },
            new() {
                Id = SystemUserId,
                Email = "user@apparcus.com",
                NormalizedEmail = "USER@APPARCUS.COM",
                NormalizedUserName = "USER@APPARCUS.COM",
                UserName = "user@apparcus.com",
                PasswordHash = "AQAAAAEAACcQAAAAEO3NQwqwWgetIJ/tyYRIrobEpEcvQ47xoczshXUgLyKKSuanh+CiKz//sKDMCq+PCA==",
                FirstName = "User",
                PhoneNumber = "0000 000 0000",
                Deleted = false,
                DateCreated = DateTime.Now
            }
        };
    }
}
