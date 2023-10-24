using DemoBlazorWASMCustomAuthWithRefreshToken.Server.Data;
using DemoBlazorWASMCustomAuthWithRefreshToken.Shared;
using DemoBlazorWASMCustomAuthWithRefreshToken.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DemoBlazorWASMCustomAuthWithRefreshToken.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        //Contructor with Instances.
        private readonly AppDbContext appDbContext;
        private readonly IConfiguration config;
        public AuthenticationController(AppDbContext appDbContext, IConfiguration config)
        {
            this.appDbContext = appDbContext;
            this.config = config;
        }

        // Account Registration
        [HttpPost("Register")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterUser(RegisterModel model)
        {
            //Check if user email contains admin and admin role is already occupied.
            string role = string.Empty;
            if (model.Email!.ToLower().StartsWith("admin"))
            {
                var chkRole = await appDbContext.UserRoles.FirstOrDefaultAsync(_ => _.RoleName!.ToLower().Equals("admin"));
                if (chkRole is null)
                    role = "Admin";
                else
                    return BadRequest("Admin already registered");
            }
            else
            {
                role = "User";
            }
           

            //Check if the email is already used in registration.
            var chk = await appDbContext.Registration.FirstOrDefaultAsync(_ => _.Email!.ToLower().Equals(model.Email!.ToLower()));
            if (chk is not null) return BadRequest(-1);

            var entity = appDbContext.Registration.Add(
                new Register()
                {
                    Email = model.Email,
                    Name = model.Name,
                    Password = BCrypt.Net.BCrypt.HashPassword(model.Password)
                }).Entity;
            await appDbContext.SaveChangesAsync();


            //Save the Role asigned to the database   
            appDbContext.UserRoles.Add(new AuthenticationModel.UserRole() { RoleName = role, UserId = entity.Id });
            await appDbContext.SaveChangesAsync();
            return Ok(entity.Id);
        }

        // Account login
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<UserSession>> LoginUser(Login model)
        {
            //Check if user email exist
            var chk = await appDbContext.Registration.FirstOrDefaultAsync(_ => _.Email!.ToLower().Equals(model.Email!.ToLower()));
            if (chk is null) return BadRequest(null!);

            if(BCrypt.Net.BCrypt.Verify(model.Password, chk.Password))
            {
                //Find User Role from the User-role table
                var getRole = await appDbContext.UserRoles.FirstOrDefaultAsync(_ => _.UserId == chk.Id);
                if (getRole is null) return BadRequest(null!);


                //Generate Token
                var token = GenerateToken(model.Email, getRole.RoleName);

                // Generate Refresh Token
                var refreshToken = GenerateRefreshToken();

                //Check if user has refresh token already.
                var chkUserToken = await appDbContext.TokenInfo.FirstOrDefaultAsync(_ => _.UserId == chk.Id);
                if (chkUserToken is null)
                {
                    //Save the refreshtoken to the TokenInfo table
                    appDbContext.TokenInfo.Add(new AuthenticationModel.TokenInfo()
                    { RefreshToken = refreshToken, UserId = chk.Id, TokenExpiry = DateTime.UtcNow.AddMinutes(2) });
                    await appDbContext.SaveChangesAsync();
                }
                else
                {
                    // Update the the refresh token
                    chkUserToken.RefreshToken = refreshToken;
                    chkUserToken.TokenExpiry = DateTime.Now.AddMinutes(1);
                    await appDbContext.SaveChangesAsync();
                }
                return Ok(new UserSession() { Token = token, RefreshToken = refreshToken });
            }
            return null!;  
        }



        // General Methods for token and refresh token generating.
        private static string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        private string GenerateToken(string? email, string? roleName)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var userClaims = new[]
            {
                new Claim(ClaimTypes.Name,email!),
                new Claim(ClaimTypes.Role,roleName!)
            };
            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"],
                audience: config["Jwt:Audience"],
                claims: userClaims,
                expires: DateTime.UtcNow.AddMinutes(1),
                signingCredentials: credentials
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }



        //PRIVATE API ENDPOINTS

        // Get total user in the database, ONLY Admin can do that.
        [HttpGet("total-users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTotalUsersCount()
        {
            var users = await appDbContext.Registration.ToListAsync();
            return Ok(users.Count);
        }


        // Get Current User info, ONLY user can do that.
        [HttpGet("my-info/{email}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetMyInfo(string email)
        {
            var user = await appDbContext.Registration.FirstOrDefaultAsync(_ => _.Email.ToLower().Equals(email.ToLower()));
            string info = $"Name : {user.Name}{Environment.NewLine}Email: {user.Email}";
            return Ok(info);
        }



        //PUBLIC API ENPOINT FOR GENERATING NEW REFRESH TOKEN AND AUTHENTICATION TOKEN
        [HttpPost("GetNewToken")]
        [AllowAnonymous]
        public async Task<ActionResult<UserSession>> GetNewToken(UserSession userSession)
        {
            if (userSession is null) return null!;
            var rToken = await appDbContext.TokenInfo.Where(_ => _.RefreshToken!.Equals(userSession.RefreshToken)).FirstOrDefaultAsync();

            // check if refresh token expiration date is due then.
            if (rToken is null) return null!;

            //Generate new refresh token if Due.
            string newRefreshToken = string.Empty;
            if (rToken.TokenExpiry < DateTime.Now)
                newRefreshToken = GenerateRefreshToken();

            //Generate new token by extracting the claims from the old jwt 
            var jwtToken = new JwtSecurityToken(userSession!.Token);
            var userClaims = jwtToken.Claims;

            //Get all claims from the token.
            var email = userClaims.First(c => c.Type == ClaimTypes.Name).Value;
            var role = userClaims.First(c => c.Type == ClaimTypes.Role).Value;
            string newToken = GenerateToken(email, role);

            //update refresh token in the DB
            var user = await appDbContext.Registration.FirstOrDefaultAsync(_ => _.Email.ToLower().Equals(email.ToLower()));
            var rTokenUser = await appDbContext.TokenInfo.FirstOrDefaultAsync(_ => _.UserId == user.Id);

            if (!string.IsNullOrEmpty(newRefreshToken))
            {
                rTokenUser.RefreshToken = newRefreshToken;
                rTokenUser.TokenExpiry = DateTime.Now.AddMinutes(1);
                await appDbContext.SaveChangesAsync();
            }
            return Ok(new UserSession() { Token = newToken, RefreshToken = newRefreshToken });

        }
    }
}
