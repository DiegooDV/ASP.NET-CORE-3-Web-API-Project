using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CompanyEmployees.ActionFilters;
using Contracts;
using Entities.DataTransferObjects;
using Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CompanyEmployees.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly ILoggerManager _logger;
        private readonly IMapper _mapper;
        private readonly UserManager<User> _userManager;
        private readonly IAuthenticationManager _authManager;
        private readonly IRepositoryManager _repository;
        public AuthenticationController(IRepositoryManager repository, ILoggerManager logger, IMapper mapper, UserManager<User> userManager, IAuthenticationManager authManager)
        {
            _logger = logger;
            _mapper = mapper;
            _userManager = userManager;
            _authManager = authManager;
            _repository = repository;
        }

        [HttpPost]
        [ServiceFilter(typeof(ValidationFilterAttribute))]
        public async Task<IActionResult> RegisterUser([FromBody] UserForRegistrationDto userForRegistration)
        {
            var user = _mapper.Map<User>(userForRegistration);
            var result = await _userManager.CreateAsync(user, userForRegistration.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.TryAddModelError(error.Code, error.Description);
                }
                return BadRequest(ModelState);
            }
            await _userManager.AddToRolesAsync(user, userForRegistration.Roles);
            return StatusCode(201);
        }

        [HttpPost("login")]
        [ServiceFilter(typeof(ValidationFilterAttribute))]
        public async Task<IActionResult> Authenticate([FromBody] UserForAuthenticationDto user)
        {
            if (!await _authManager.ValidateUser(user))
            {
                _logger.LogWarn($"{nameof(Authenticate)}: Authentication failed. Wrong user name or password.");
            return Unauthorized();
            }
            var userEntity = await _userManager.FindByNameAsync(user.UserName);
            var claims = new List<Claim>
            {
            new Claim(ClaimTypes.Name, user.UserName) 
            };
            var accessToken = _authManager.GenerateAccessToken(claims);
            var refreshToken = _authManager.GenerateRefreshToken();
            userEntity.RefreshToken = refreshToken;
            userEntity.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            await _repository.SaveAsync();
            return Ok(new
            {
                Token = accessToken,
                RefreshToken = refreshToken
            });
        }

        [HttpPost]
        [Route("refresh")]
        public async Task<IActionResult> RefreshToken(TokenApiModel tokenApiModel)
        {

            if (tokenApiModel is null)
            {
                return BadRequest("Invalid client request");
            }
            string token = tokenApiModel.Token;
            string refreshToken = tokenApiModel.RefreshToken;
            var principal = _authManager.GetPrincipalFromExpiredToken(token);
            var username = principal.Identity.Name; //this is mapped to the Name claim by default
            var user = await _userManager.FindByNameAsync(username);
            if(user == null)
            {
                return BadRequest("user nor found");
            }
            if(user.RefreshToken != refreshToken)
            {
                return BadRequest("refresh token is not the same"); 
            }
            if (user.RefreshTokenExpiryTime <= DateTime.Now)
            {
                return BadRequest("refresh token has expired");
            }

            var newAccessToken = _authManager.GenerateAccessToken(principal.Claims);
            var newRefreshToken = _authManager.GenerateRefreshToken();
            user.RefreshToken = newRefreshToken;
            await _repository.SaveAsync();
            return new ObjectResult(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken
            });
        }

        [HttpPost, Authorize]
        [Route("revoke")]
        public async Task<IActionResult> Revoke()
        {
            var username = User.Identity.Name;
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                return BadRequest();
            }
            user.RefreshToken = null;
            await _repository.SaveAsync();
            return NoContent();
        }
    }
}